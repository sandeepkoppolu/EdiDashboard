using EdiProcessor.Core.Models;
using EdiProcessor.Core.Parsers;
using EdiProcessor.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EdiProcessor.Infrastructure.Services;

/// <summary>
/// .NET BackgroundService that watches an inbox folder for EDI files.
///
/// Folder layout (all paths are configurable in appsettings.json):
///
///   edi-inbox/
///     ├── BCBS001/          ← sub-folder named after ISA06 (trading partner interchange ID)
///     │     ├── claim1.edi
///     │     └── ack.ta1
///     ├── AETNA01/
///     │     └── batch.x12
///     └── unknown.edi       ← root files: matched by ISA06 content, or DefaultTradingPartnerId
///
///   edi-processed/
///     └── 2024/01/15/
///           ├── claim1.edi
///           └── claim1.edi.result.json
///
///   edi-failed/
///     └── bad_file.edi
///         bad_file.edi.error.txt
///
/// Processing order per polling cycle:
///   1. Collect all matching files from inbox (sub-folders first, root last).
///   2. For each file, resolve the trading partner (sub-folder name → ISA06 → default).
///   3. Lock check — skip if file is still being written.
///   4. Parse + persist via IEdiProcessingService.
///   5. Move file to processed/ or failed/.
///   6. Write a .result.json (success) or .error.txt (failure) alongside.
/// </summary>
public class EdiFolderWatcherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EdiWatcherOptions _opts;
    private readonly ILogger<EdiFolderWatcherBackgroundService> _logger;

    // Shared state exposed to the status API
    private static DateTime? _lastScanAt;
    private static int _totalPickedUp;
    private static int _totalSucceeded;
    private static int _totalFailed;
    private static bool _fswActive;

    private FileSystemWatcher? _fsw;


    // Tracks files currently being processed to avoid double-processing
    private readonly HashSet<string> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Persists a FileProcessingLog to the database
    private async Task PersistLogAsync(FileProcessingLog log)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IFileProcessingLogRepository>();
        if (log.Id == 0)
        {
            await repo.CreateAsync(log);
        }
        else
        {
            await repo.UpdateAsync(log);
        }
    }

    public static DateTime? LastScanAt => _lastScanAt;
    public static int TotalPickedUp => _totalPickedUp;
    public static int TotalSucceeded => _totalSucceeded;
    public static int TotalFailed => _totalFailed;
    public static bool FswActive => _fswActive;

    public EdiFolderWatcherBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<EdiWatcherOptions> opts,
        ILogger<EdiFolderWatcherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _opts = opts.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        EnsureFoldersExist();
        SetupFileSystemWatcher(stoppingToken);

        _logger.LogInformation(
            "EDI Folder Watcher started. Inbox: {Inbox} | Poll: {Poll}s | FSW: {Fsw}",
            Path.GetFullPath(_opts.InboxPath), _opts.PollingIntervalSeconds, _opts.UseFileSystemWatcher);

        // Initial scan on startup
        await ScanInboxAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_opts.PollingIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ScanInboxAsync(stoppingToken);
        }
    }

    // ── Public trigger (used by the manual scan API endpoint) ──────────────
    public async Task<List<FileProcessingLog>> TriggerScanAsync(CancellationToken ct = default)
        => await ScanInboxAsync(ct);

    // ── Core scan logic ────────────────────────────────────────────────────
    private async Task<List<FileProcessingLog>> ScanInboxAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        var results = new List<FileProcessingLog>();
        try
        {
            _lastScanAt = DateTime.UtcNow;
            var files = CollectInboxFiles();

            if (files.Count == 0)
            {
                _logger.LogDebug("Inbox scan: no files found.");
                return results;
            }

            _logger.LogInformation("Inbox scan: {Count} file(s) found.", files.Count);

            foreach (var (filePath, inferredPartnerId) in files)
            {
                if (ct.IsCancellationRequested) break;
                if (_inFlight.Contains(filePath)) continue;

                _inFlight.Add(filePath);
                try
                {
                    var log = await ProcessSingleFileAsync(filePath, inferredPartnerId, ct);
                    results.Add(log);
                }
                finally
                {
                    _inFlight.Remove(filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during inbox scan.");
        }
        finally
        {
            _semaphore.Release();
        }
        return results;
    }

    // ── Collect all eligible files ─────────────────────────────────────────
    private List<(string path, int? partnerId)> CollectInboxFiles()
    {
        var result = new List<(string, int?)>();
        var inboxFull = Path.GetFullPath(_opts.InboxPath);

        if (!Directory.Exists(inboxFull)) return result;

        // Sub-folders (partner-specific) — resolve by folder name → ISA06
        foreach (var subDir in Directory.GetDirectories(inboxFull))
        {
            var folderName = Path.GetFileName(subDir);
            int? partnerId = ResolvePartnerByInterchangeId(folderName);
            foreach (var file in GetEdiFiles(subDir))
                result.Add((file, partnerId));
        }

        // Root-level files — will be resolved later by file content
        foreach (var file in GetEdiFiles(inboxFull))
            result.Add((file, null));

        return result;
    }

    private IEnumerable<string> GetEdiFiles(string folder)
        => Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(f => _opts.Extensions.Contains(
                Path.GetExtension(f).ToLowerInvariant(),
                StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => File.GetCreationTimeUtc(f)); // FIFO

    // ── Process one file ───────────────────────────────────────────────────
    private async Task<FileProcessingLog> ProcessSingleFileAsync(
        string filePath, int? hintedPartnerId, CancellationToken ct)
    {
        var fileName = Path.GetFileName(filePath);
        var fileInfo = new FileInfo(filePath);

        var log = new FileProcessingLog
        {
            FileName = fileName,
            OriginalPath = filePath,
            FileSizeBytes = fileInfo.Length,
            PickedUpAt = DateTime.UtcNow,
            Source = "FolderWatcher",
            Status = "Pending",
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Interlocked.Increment(ref _totalPickedUp);

        try
        {
            // ── Lock check ──
            if (!IsFileReady(filePath))
            {
                _logger.LogWarning("File still locked (being written): {File}. Will retry next cycle.", fileName);
                log.Status = "Skipped";
                log.ErrorMessage = "File locked — retry next cycle";
                log.FinalPath = filePath;
                await PersistLogAsync(log);
                return log;
            }

            // ── Read content ──
            var content = await File.ReadAllTextAsync(filePath, ct);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidDataException("File is empty.");
            }

            // ── Detect type ──
            var detectedType = EdiTypeDetector.Detect(content);
            log.DetectedType = detectedType;

            // ── Resolve trading partner ──
            int partnerId = await ResolvePartnerIdAsync(content, detectedType, hintedPartnerId);
            log.TradingPartnerId = partnerId;

            // ── Process via the same service used by the web API ──
            using var scope = _scopeFactory.CreateScope();
            var processingService = scope.ServiceProvider.GetRequiredService<IEdiProcessingService>();

            var result = await processingService.ProcessEdiFileAsync(content, partnerId);

            log.ControlNumber = result.ControlNumber;
            log.ClaimsProcessed = result.ClaimsProcessed;

            if (result.Success)
            {
                log.Status = "Success";
                var dest = BuildProcessedPath(fileName);
                MoveFile(filePath, dest);
                log.FinalPath = dest;
                await WriteResultJsonAsync(dest, result);
                Interlocked.Increment(ref _totalSucceeded);
                _logger.LogInformation(
                    "✓ {File} → {Type} | Partner:{PartnerId} | Claims:{Claims} | Control:{CN}",
                    fileName, detectedType, partnerId, result.ClaimsProcessed, result.ControlNumber);
            }
            else
            {
                throw new InvalidOperationException(result.Message +
                    (result.Errors.Count > 0 ? " | " + string.Join("; ", result.Errors) : ""));
            }
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.ErrorMessage = ex.Message;
            Interlocked.Increment(ref _totalFailed);
            _logger.LogError(ex, "✗ Failed to process {File}: {Msg}", fileName, ex.Message);

            try
            {
                var dest = BuildFailedPath(fileName);
                MoveFile(filePath, dest);
                log.FinalPath = dest;
                await WriteErrorFileAsync(dest, ex);
            }
            catch (Exception moveEx)
            {
                _logger.LogError(moveEx, "Could not move failed file {File}", fileName);
                log.FinalPath = filePath;
            }
        }
        finally
        {
            sw.Stop();
            log.ProcessingDuration = sw.Elapsed;
            log.CompletedAt = DateTime.UtcNow;
            await PersistLogAsync(log);
        }

        return log;
    }

    // ── Partner resolution ─────────────────────────────────────────────────

    private async Task<int> ResolvePartnerIdAsync(string content, string detectedType, int? hintedPartnerId)
    {
        // 1. Use the sub-folder hint if already resolved
        if (hintedPartnerId.HasValue && hintedPartnerId.Value > 0)
            return hintedPartnerId.Value;

        // 2. Try to resolve by ISA06/ISA08 with EDI-type awareness.
        //    For TA1/999/277CA, sender/receiver are reversed, so prefer ISA08.
        var isa06 = ExtractIsa06(content); // sender id
        var isa05 = ExtractIsa05(content); // sender qualifier
        var isa08 = ExtractIsa08(content); // receiver id
        var isa07 = ExtractIsa07(content); // receiver qualifier
        var isResponse = detectedType is "TA1" or "999" or "277CA";

        var primaryId = isResponse ? isa08 : isa06;
        var primaryQualifier = isResponse ? isa07 : isa05;
        var secondaryId = isResponse ? isa06 : isa08;
        var secondaryQualifier = isResponse ? isa05 : isa07;

        if (!string.IsNullOrWhiteSpace(primaryId))
        {
            var existingPrimary = ResolvePartnerByInterchangeId(primaryId);
            if (existingPrimary.HasValue) return existingPrimary.Value;
        }

        if (!string.IsNullOrWhiteSpace(secondaryId))
        {
            var existingSecondary = ResolvePartnerByInterchangeId(secondaryId);
            if (existingSecondary.HasValue) return existingSecondary.Value;
        }

        if (!string.IsNullOrWhiteSpace(primaryId))
            return await ResolveOrCreatePartnerIdAsync(primaryId, primaryQualifier);
        if (!string.IsNullOrWhiteSpace(secondaryId))
            return await ResolveOrCreatePartnerIdAsync(secondaryId, secondaryQualifier);

        // 3. Fall back to default
        if (_opts.DefaultTradingPartnerId > 0)
        {
            _logger.LogWarning(
                "Could not resolve trading partner from ISA06 '{Isa06}' / ISA08 '{Isa08}'. Using default ID {Default}.",
                isa06, isa08, _opts.DefaultTradingPartnerId);
            return _opts.DefaultTradingPartnerId;
        }

        throw new InvalidOperationException(
            $"Cannot determine trading partner. ISA06='{isa06}', ISA08='{isa08}'. " +
            "Set EdiWatcher:DefaultTradingPartnerId in appsettings.json or use a named sub-folder.");
    }

    private async Task<int> ResolveOrCreatePartnerIdAsync(string interchangeId, string? interchangeQualifier)
    {
        var normalizedId = interchangeId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
            throw new InvalidOperationException("Cannot resolve trading partner: empty interchange ID.");

        using var scope = _scopeFactory.CreateScope();
        var partnerSvc = scope.ServiceProvider.GetRequiredService<ITradingPartnerService>();

        var existing = await partnerSvc.GetByInterchangeIdAsync(normalizedId);
        if (existing != null)
            return existing.Id;

        var qualifier = string.IsNullOrWhiteSpace(interchangeQualifier)
            ? "ZZ"
            : interchangeQualifier.Trim();

        var created = await partnerSvc.CreateAsync(new TradingPartner
        {
            Name = $"Auto-{normalizedId}",
            InterchangeId = normalizedId,
            InterchangeQualifier = qualifier,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "Auto-created trading partner {PartnerId} for ISA06 '{InterchangeId}' (ISA05 '{Qualifier}').",
            created.Id, created.InterchangeId, created.InterchangeQualifier);

        return created.Id;
    }

    private int? ResolvePartnerByInterchangeId(string interchangeId)
    {
        // Look up from the database via a quick scope
        using var scope = _scopeFactory.CreateScope();
        var partnerSvc = scope.ServiceProvider.GetRequiredService<ITradingPartnerService>();
        var partner = partnerSvc.GetByInterchangeIdAsync(interchangeId.Trim()).GetAwaiter().GetResult();
        return partner?.Id;
    }

    private static string ExtractIsa06(string content)
    {
        // ISA is always the first segment. Element 6 (0-based index after splitting on ISA separator char at position 3).
        if (content.Length < 106) return string.Empty;
        var sep = content[3];
        var parts = content.Substring(0, 110).Split(sep);
        return parts.Length > 6 ? parts[6].Trim() : string.Empty;
    }

    private static string ExtractIsa05(string content)
    {
        // ISA05 = interchange ID qualifier for sender.
        if (content.Length < 106) return string.Empty;
        var sep = content[3];
        var parts = content.Substring(0, 110).Split(sep);
        return parts.Length > 5 ? parts[5].Trim() : string.Empty;
    }

    private static string ExtractIsa07(string content)
    {
        // ISA07 = interchange ID qualifier for receiver.
        if (content.Length < 106) return string.Empty;
        var sep = content[3];
        var parts = content.Substring(0, 110).Split(sep);
        return parts.Length > 7 ? parts[7].Trim() : string.Empty;
    }

    private static string ExtractIsa08(string content)
    {
        // ISA08 = interchange receiver ID.
        if (content.Length < 106) return string.Empty;
        var sep = content[3];
        var parts = content.Substring(0, 110).Split(sep);
        return parts.Length > 8 ? parts[8].Trim() : string.Empty;
    }

    // ── File system helpers ────────────────────────────────────────────────

    private void EnsureFoldersExist()
    {
        foreach (var path in new[] { _opts.InboxPath, _opts.ProcessedPath, _opts.FailedPath })
            Directory.CreateDirectory(Path.GetFullPath(path));

        // Create one example sub-folder per seeded trading partner
        foreach (var id in new[] { "BCBS001", "AETNA01", "UHC0001", "CIGNA01" })
            Directory.CreateDirectory(Path.Combine(Path.GetFullPath(_opts.InboxPath), id));


    }

    private string BuildProcessedPath(string fileName)
    {
        var today = DateTime.UtcNow;
        var dir = Path.Combine(
            Path.GetFullPath(_opts.ProcessedPath),
            today.Year.ToString(), today.Month.ToString("D2"), today.Day.ToString("D2"));
        Directory.CreateDirectory(dir);

        // Avoid collisions with a timestamp prefix
        var stamp = today.ToString("HHmmss_fff");
        return Path.Combine(dir, $"{stamp}_{fileName}");
    }

    private string BuildFailedPath(string fileName)
    {
        var dir = Path.GetFullPath(_opts.FailedPath);
        Directory.CreateDirectory(dir);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        return Path.Combine(dir, $"{stamp}_{fileName}");
    }

    private static void MoveFile(string src, string dst)
    {
        if (File.Exists(dst)) File.Delete(dst);
        File.Move(src, dst);
    }

    private static async Task WriteResultJsonAsync(string processedFilePath, EdiUploadResult result)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(result,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(processedFilePath + ".result.json", json);
    }

    private static async Task WriteErrorFileAsync(string failedFilePath, Exception ex)
    {
        var content = $"PROCESSING FAILED\n{DateTime.UtcNow:u}\n\n{ex.GetType().Name}: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
        await File.WriteAllTextAsync(failedFilePath + ".error.txt", content);
    }

    private static bool IsFileReady(string path)
    {
        try
        {
            using var s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException) { return false; }
    }

    // ── FileSystemWatcher (near-instant pickup) ────────────────────────────

    private void SetupFileSystemWatcher(CancellationToken ct)
    {
        if (!_opts.UseFileSystemWatcher) return;

        try
        {
            var inboxFull = Path.GetFullPath(_opts.InboxPath);
            _fsw = new FileSystemWatcher(inboxFull)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            };

            foreach (var ext in _opts.Extensions)
                _fsw.Filters.Add("*" + ext);

            _fsw.Created += (_, e) => OnFileEvent(e.FullPath, ct);
            _fsw.Renamed += (_, e) => OnFileEvent(e.FullPath, ct);
            _fswActive = true;
            _logger.LogInformation("FileSystemWatcher active on: {Path}", inboxFull);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not start FileSystemWatcher. Polling only.");
            _fswActive = false;
        }
    }

    private void OnFileEvent(string filePath, CancellationToken ct)
    {
        // Debounce: wait a moment for the write to complete, then process
        Task.Delay(TimeSpan.FromSeconds(_opts.FileLockRetrySeconds), ct)
            .ContinueWith(async _ =>
            {
                if (_inFlight.Contains(filePath)) return;
                _inFlight.Add(filePath);
                try { await ProcessSingleFileAsync(filePath, null, ct); }
                finally { _inFlight.Remove(filePath); }
            }, ct);
    }

    public override void Dispose()
    {
        _fsw?.Dispose();
        _semaphore.Dispose();
        base.Dispose();
    }
}
