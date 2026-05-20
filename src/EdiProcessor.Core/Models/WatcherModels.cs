namespace EdiProcessor.Core.Models;

// ─── Folder Watcher Configuration ────────────────────────────────────────────

/// <summary>
/// Loaded from appsettings.json → "EdiWatcher" section.
/// Controls where the background service looks for EDI files and where it
/// moves them after processing.
/// </summary>
public class EdiWatcherOptions
{
    public const string Section = "EdiWatcher";

    /// <summary>Root inbox folder. Sub-folders named after trading partner
    /// Interchange IDs are checked first; files in the root are matched
    /// automatically by ISA06 content.</summary>
    public string InboxPath { get; set; } = "edi-inbox";

    /// <summary>Processed files are archived here, under a YYYY/MM/DD tree.</summary>
    public string ProcessedPath { get; set; } = "edi-processed";

    /// <summary>Files that fail parsing or processing are moved here with an
    /// accompanying .error.txt file containing the exception.</summary>
    public string FailedPath { get; set; } = "edi-failed";

    /// <summary>How often the watcher scans the inbox (seconds). Default 30.</summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>File extensions to pick up (case-insensitive).</summary>
    public string[] Extensions { get; set; } = [".edi", ".txt", ".x12", ".837", ".999", ".ta1", ".270", ".835"];

    /// <summary>If true, use FileSystemWatcher for near-instant pickup in
    /// addition to the polling fallback.</summary>
    public bool UseFileSystemWatcher { get; set; } = true;

    /// <summary>If a file cannot be exclusively locked (still being written),
    /// retry after this many seconds before skipping for this cycle.</summary>
    public int FileLockRetrySeconds { get; set; } = 5;

    /// <summary>When no sub-folder partner match is found, fall back to this
    /// trading partner ID. 0 = skip and move to failed.</summary>
    public int DefaultTradingPartnerId { get; set; } = 0;
}

// ─── File Processing Log ─────────────────────────────────────────────────────

/// <summary>
/// Persisted record of every file the watcher attempted to process.
/// Stored in the FileProcessingLogs table.
/// </summary>
public class FileProcessingLog
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string FinalPath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? DetectedType { get; set; }       // 837P, TA1, 999, UNKNOWN …
    public int? TradingPartnerId { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed, Skipped
    public string? ErrorMessage { get; set; }
    public string? ControlNumber { get; set; }
    public int ClaimsProcessed { get; set; }
    public DateTime PickedUpAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? ProcessingDuration { get; set; }
    public string Source { get; set; } = "FolderWatcher"; // FolderWatcher | API | Upload
}

// ─── Watcher Status DTO ──────────────────────────────────────────────────────

public class WatcherStatus
{
    public bool IsRunning { get; set; }
    public string InboxPath { get; set; } = string.Empty;
    public string ProcessedPath { get; set; } = string.Empty;
    public string FailedPath { get; set; } = string.Empty;
    public int PollingIntervalSeconds { get; set; }
    public bool FileSystemWatcherActive { get; set; }
    public DateTime? LastScanAt { get; set; }
    public int FilesPickedUpTotal { get; set; }
    public int FilesSucceededTotal { get; set; }
    public int FilesFailedTotal { get; set; }
    public int FilesInInboxNow { get; set; }
    public List<string> WatchedExtensions { get; set; } = new();
    public List<PartnerFolderInfo> PartnerFolders { get; set; } = new();
    public List<FileProcessingLog> RecentLogs { get; set; } = new();
}

public class PartnerFolderInfo
{
    public string FolderName { get; set; } = string.Empty;
    public string? MappedPartnerName { get; set; }
    public int FilesWaiting { get; set; }
}
