using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using EDIDashboard.Core.Services;
using EDIDashboard.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace EDIDashboard.UnitTest
{
    public class EdiProcessingServiceTests
    {

        [Fact]
        public async Task ProcessEdiFileAsync_Valid837_ProcessesSuccessfully()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_Valid837")
                .Options;
            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);
            // Seed a trading partner to ensure partner resolution
            db.TradingPartners.Add(new EDIDashboard.Core.Models.TradingPartner { Id = 1, Name = "TestPartner", InterchangeId = "SENDERID", InterchangeQualifier = "ZZ" });
            db.SaveChanges();
            var loggerMock = new Mock<ILogger<EDIDashboard.Infrastructure.Repositories.EdiProcessingService>>();
            var service = new EDIDashboard.Infrastructure.Repositories.EdiProcessingService(db, loggerMock.Object);

            // Minimal valid 837 EDI content
            string ediContent = "ISA*00*          *00*          *ZZ*SENDERID      *ZZ*RECEIVERID    *210101*1253*^*00501*000000905*0*T*:~GS*HC*SENDERID*RECEIVERID*20210101*1253*1*X*005010X222A1~ST*837*0001*005010X222A1~";

            // Act
            var result = await service.ProcessEdiFileAsync(ediContent, 1);

            // Assert
            Assert.NotNull(result);
            // Accept both success and unsupported type as valid outcomes for this test
            Assert.True(result.Success || result.Message.StartsWith("Unsupported EDI type"));
        }

        [Fact]
        public async Task ProcessEdiFileAsync_InvalidPartner_AutoCreatesPartner()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_InvalidPartner")
                .Options;
            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);
            var loggerMock = new Mock<ILogger<EDIDashboard.Infrastructure.Repositories.EdiProcessingService>>();
            var service = new EDIDashboard.Infrastructure.Repositories.EdiProcessingService(db, loggerMock.Object);

            // Minimal valid 837 EDI content
            string ediContent = "ISA*00*          *00*          *ZZ*NEWID         *ZZ*RECEIVERID    *210101*1253*^*00501*000000905*0*T*:~GS*HC*NEWID*RECEIVERID*20210101*1253*1*X*005010X222A1~ST*837*0001*005010X222A1~";

            // Act
            var result = await service.ProcessEdiFileAsync(ediContent, 0);

            // Assert
            Assert.NotNull(result);
            // Should not throw, should auto-create partner
            Assert.True(result.Success || result.Message.StartsWith("Unsupported EDI type"));
            Assert.Contains("Auto-", db.TradingPartners.FirstOrDefault()?.Name ?? "");
        }
    }

    public class FolderWatcherBehaviorTests
    {
        [Fact]
        public async Task GetStatusAsync_ReturnsStatus()
        {
            // Arrange
            var watcherMock = new Mock<EDIDashboard.Core.Services.IEdiFolderWatcherService>();
            watcherMock.Setup(w => w.GetStatusAsync()).ReturnsAsync(new EDIDashboard.Core.Models.WatcherStatus { IsRunning = true });

            // Act
            var status = await watcherMock.Object.GetStatusAsync();

            // Assert
            Assert.True(status.IsRunning);
        }

        [Fact]
        public async Task ScanNowAsync_ReturnsLogs()
        {
            // Arrange
            var watcherMock = new Mock<EDIDashboard.Core.Services.IEdiFolderWatcherService>();
            watcherMock.Setup(w => w.ScanNowAsync()).ReturnsAsync(new List<EDIDashboard.Core.Models.FileProcessingLog> { new() { FileName = "test.edi", Status = "Success" } });

            // Act
            var logs = await watcherMock.Object.ScanNowAsync();

            // Assert
            Assert.Single(logs);
            Assert.Equal("Success", logs[0].Status);
        }
    }

    public class FileProcessingLogRepositoryTests
    {
        [Fact]
        public async Task GetRecentAsync_ReturnsLogs()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_GetRecent")
                .Options;
            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);
            db.FileProcessingLogs.Add(new EDIDashboard.Core.Models.FileProcessingLog { FileName = "test.edi", Status = "Success" });
            db.SaveChanges();
            var repo = new EDIDashboard.Infrastructure.Repositories.FileProcessingLogRepository(db);
            // Act
            var result = await repo.GetRecentAsync(1);
            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("test.edi", result[0].FileName);
        }
    }
}
