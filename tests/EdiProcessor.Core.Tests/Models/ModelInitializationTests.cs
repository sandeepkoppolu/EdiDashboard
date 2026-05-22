using EdiProcessor.Core.Models;
using EdiProcessor.Core.Services;

namespace EdiProcessor.Core.Tests.Models;

public class ModelInitializationTests
{
    [Fact]
    public void TradingPartner_Defaults_AreInitialized()
    {
        var model = new TradingPartner();
        Assert.Equal(string.Empty, model.Name);
        Assert.Equal(string.Empty, model.InterchangeId);
        Assert.Equal(string.Empty, model.InterchangeQualifier);
        Assert.True(model.IsActive);
        Assert.NotEqual(default, model.CreatedAt);
        Assert.NotNull(model.Transactions);
    }

    [Fact]
    public void DashboardAndRelatedDtos_Defaults_AreInitialized()
    {
        var dashboard = new DashboardMetrics();
        Assert.NotNull(dashboard.ByTradingPartner);
        Assert.NotNull(dashboard.DailyVolumes);
        Assert.NotNull(dashboard.ByClaimType);
        Assert.NotNull(dashboard.RecentTransactions);

        var daily = new DailyVolume { Claims = 1, Accepted = 1, Rejected = 0 };
        var claimType = new ClaimTypeMetric { ClaimType = "Professional", Count = 2, TotalAmount = 10m };
        var recent = new RecentTransaction
        {
            Id = 1,
            TradingPartner = "Partner",
            TransactionType = "837P",
            ControlNumber = "CN1",
            Status = "Received",
            ClaimCount = 2,
            ReceivedAt = new DateTime(2024, 1, 1)
        };

        Assert.Equal(1, daily.Claims);
        Assert.Equal("Professional", claimType.ClaimType);
        Assert.Equal("Partner", recent.TradingPartner);
    }

    [Fact]
    public void UploadResult_Defaults_AreInitialized()
    {
        var upload = new EdiUploadResult();
        Assert.False(upload.Success);
        Assert.Equal(string.Empty, upload.Message);
        Assert.Equal(string.Empty, upload.TransactionType);
        Assert.Equal(string.Empty, upload.ControlNumber);
        Assert.Equal(0, upload.ClaimsProcessed);
        Assert.NotNull(upload.Errors);
    }

    [Fact]
    public void WatcherModels_Defaults_AreInitialized()
    {
        var options = new EdiWatcherOptions();
        Assert.Equal("EdiWatcher", EdiWatcherOptions.Section);
        Assert.Equal("edi-inbox", options.InboxPath);
        Assert.Equal("edi-processed", options.ProcessedPath);
        Assert.Equal("edi-failed", options.FailedPath);
        Assert.True(options.UseFileSystemWatcher);
        Assert.True(options.Extensions.Length > 0);

        var log = new FileProcessingLog();
        Assert.Equal("Pending", log.Status);
        Assert.Equal("FolderWatcher", log.Source);
        Assert.NotEqual(default, log.PickedUpAt);

        var status = new WatcherStatus();
        Assert.False(status.IsRunning);
        Assert.NotNull(status.WatchedExtensions);
        Assert.NotNull(status.PartnerFolders);
        Assert.NotNull(status.RecentLogs);

        var partnerFolder = new PartnerFolderInfo { FolderName = "BCBS001", MappedPartnerName = "BlueCross", FilesWaiting = 2 };
        Assert.Equal("BCBS001", partnerFolder.FolderName);
        Assert.Equal("BlueCross", partnerFolder.MappedPartnerName);
        Assert.Equal(2, partnerFolder.FilesWaiting);

        var summary = new WatcherSummaryStats { TotalFiles = 5, Succeeded = 3, Failed = 1, Skipped = 1, LastFileAt = DateTime.UtcNow };
        Assert.Equal(5, summary.TotalFiles);
        Assert.Equal(3, summary.Succeeded);
    }
}
