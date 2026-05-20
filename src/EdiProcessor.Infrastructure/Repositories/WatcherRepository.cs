using EdiProcessor.Core.Models;
using EdiProcessor.Core.Services;
using EdiProcessor.Infrastructure.Data;
using EdiProcessor.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EdiProcessor.Infrastructure.Repositories;

public class FileProcessingLogRepository : IFileProcessingLogRepository
{
    private readonly EdiDbContext _db;
    public FileProcessingLogRepository(EdiDbContext db) => _db = db;

    public async Task<List<FileProcessingLog>> GetRecentAsync(int count = 50, int? tradingPartnerId = null)
    {
        var q = _db.FileProcessingLogs.AsQueryable();
        if (tradingPartnerId.HasValue)
            q = q.Where(l => l.TradingPartnerId == tradingPartnerId.Value);
        return await q.OrderByDescending(l => l.PickedUpAt).Take(count).ToListAsync();
    }

    public async Task<FileProcessingLog> CreateAsync(FileProcessingLog log)
    {
        _db.FileProcessingLogs.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }

    public async Task UpdateAsync(FileProcessingLog log)
    {
        _db.FileProcessingLogs.Update(log);
        await _db.SaveChangesAsync();
    }

    public async Task<WatcherSummaryStats> GetSummaryStatsAsync()
    {
        var stats = await _db.FileProcessingLogs
            .GroupBy(_ => 1)
            .Select(g => new WatcherSummaryStats
            {
                TotalFiles  = g.Count(),
                Succeeded   = g.Count(x => x.Status == "Success"),
                Failed      = g.Count(x => x.Status == "Failed"),
                Skipped     = g.Count(x => x.Status == "Skipped"),
                LastFileAt  = g.Max(x => (DateTime?)x.PickedUpAt),
            })
            .FirstOrDefaultAsync();

        return stats ?? new WatcherSummaryStats();
    }
}

public class EdiFolderWatcherService : IEdiFolderWatcherService
{
    private readonly EdiWatcherOptions _opts;
    private readonly IFileProcessingLogRepository _logRepo;
    private readonly EdiFolderWatcherBackgroundService _bgService;
    private readonly ITradingPartnerService _partnerService;

    public EdiFolderWatcherService(
        IOptions<EdiWatcherOptions> opts,
        IFileProcessingLogRepository logRepo,
        EdiFolderWatcherBackgroundService bgService,
        ITradingPartnerService partnerService)
    {
        _opts         = opts.Value;
        _logRepo      = logRepo;
        _bgService    = bgService;
        _partnerService = partnerService;
    }

    public async Task<WatcherStatus> GetStatusAsync()
    {
        var inbox    = Path.GetFullPath(_opts.InboxPath);
        var partners = await _partnerService.GetAllAsync();

        // Count files currently waiting in inbox
        var inboxFiles = CountEdiFiles(inbox);

        // Per sub-folder breakdown
        var partnerFolders = new List<PartnerFolderInfo>();
        if (Directory.Exists(inbox))
        {
            foreach (var dir in Directory.GetDirectories(inbox))
            {
                var folderName = Path.GetFileName(dir);
                var mapped = partners.FirstOrDefault(p =>
                    string.Equals(p.InterchangeId, folderName, StringComparison.OrdinalIgnoreCase));
                partnerFolders.Add(new PartnerFolderInfo
                {
                    FolderName       = folderName,
                    MappedPartnerName = mapped?.Name,
                    FilesWaiting     = CountEdiFiles(dir),
                });
            }
        }

        var stats   = await _logRepo.GetSummaryStatsAsync();
        var recents = await _logRepo.GetRecentAsync(25);

        return new WatcherStatus
        {
            IsRunning               = true, // Always running as a BackgroundService
            InboxPath               = inbox,
            ProcessedPath           = Path.GetFullPath(_opts.ProcessedPath),
            FailedPath              = Path.GetFullPath(_opts.FailedPath),
            PollingIntervalSeconds  = _opts.PollingIntervalSeconds,
            FileSystemWatcherActive = EdiFolderWatcherBackgroundService.FswActive,
            LastScanAt              = EdiFolderWatcherBackgroundService.LastScanAt,
            FilesPickedUpTotal      = EdiFolderWatcherBackgroundService.TotalPickedUp,
            FilesSucceededTotal     = EdiFolderWatcherBackgroundService.TotalSucceeded,
            FilesFailedTotal        = EdiFolderWatcherBackgroundService.TotalFailed,
            FilesInInboxNow         = inboxFiles,
            WatchedExtensions       = _opts.Extensions.ToList(),
            PartnerFolders          = partnerFolders,
            RecentLogs              = recents,
        };
    }

    public async Task<List<FileProcessingLog>> ScanNowAsync()
        => await _bgService.TriggerScanAsync();

    private int CountEdiFiles(string folder)
    {
        if (!Directory.Exists(folder)) return 0;
        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Count(f => _opts.Extensions.Contains(
                Path.GetExtension(f).ToLowerInvariant(),
                StringComparer.OrdinalIgnoreCase));
    }
}
