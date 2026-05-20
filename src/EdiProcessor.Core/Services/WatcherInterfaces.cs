using EdiProcessor.Core.Models;

namespace EdiProcessor.Core.Services;

public interface IEdiFolderWatcherService
{
    /// <summary>Returns the current watcher status and recent log entries.</summary>
    Task<WatcherStatus> GetStatusAsync();

    /// <summary>Manually triggers a one-time scan of the inbox right now,
    /// regardless of the polling timer.</summary>
    Task<List<FileProcessingLog>> ScanNowAsync();
}

public interface IFileProcessingLogRepository
{
    Task<List<FileProcessingLog>> GetRecentAsync(int count = 50, int? tradingPartnerId = null);
    Task<FileProcessingLog> CreateAsync(FileProcessingLog log);
    Task UpdateAsync(FileProcessingLog log);
    Task<WatcherSummaryStats> GetSummaryStatsAsync();
}

public class WatcherSummaryStats
{
    public int TotalFiles { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public DateTime? LastFileAt { get; set; }
}
