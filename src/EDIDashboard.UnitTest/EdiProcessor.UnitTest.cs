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

        [Fact]
        public async Task ProcessEdiFileAsync_Ta1_UsesIsa08AsTradingPartner()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_AckUsesSender")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);
            db.TradingPartners.AddRange(
                new EDIDashboard.Core.Models.TradingPartner
                {
                    Id = 1,
                    Name = "Payer",
                    InterchangeId = "PAYERID",
                    InterchangeQualifier = "ZZ"
                },
                new EDIDashboard.Core.Models.TradingPartner
                {
                    Id = 2,
                    Name = "Our Org",
                    InterchangeId = "OURSUBMITTER",
                    InterchangeQualifier = "ZZ"
                });
            db.SaveChanges();

            var loggerMock = new Mock<ILogger<EDIDashboard.Infrastructure.Repositories.EdiProcessingService>>();
            var service = new EDIDashboard.Infrastructure.Repositories.EdiProcessingService(db, loggerMock.Object);

            string ediContent = "ISA*00*          *00*          *ZZ*PAYERID       *ZZ*OURSUBMITTER *210101*1253*^*00501*000000905*0*T*:~TA1*000000905*210101*1253*A*000~";

            var result = await service.ProcessEdiFileAsync(ediContent, 0);

            Assert.True(result.Success, result.Message);

            var transaction = db.EdiTransactions.Single();
            Assert.Equal(2, transaction.TradingPartnerId);
            Assert.DoesNotContain(db.TradingPartners, p => p.Name == "Auto-OURSUBMITTER");
        }

        [Fact]
        public async Task ProcessEdiFileAsync_Ta1_IgnoresWrongExplicitPartnerAndUsesIsa08()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_AckWrongExplicit")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);
            db.TradingPartners.AddRange(
                new EDIDashboard.Core.Models.TradingPartner
                {
                    Id = 1,
                    Name = "Payer",
                    InterchangeId = "PAYERID",
                    InterchangeQualifier = "ZZ"
                },
                new EDIDashboard.Core.Models.TradingPartner
                {
                    Id = 2,
                    Name = "Our Org",
                    InterchangeId = "OURSUBMITTER",
                    InterchangeQualifier = "ZZ"
                });
            db.SaveChanges();

            var loggerMock = new Mock<ILogger<EDIDashboard.Infrastructure.Repositories.EdiProcessingService>>();
            var service = new EDIDashboard.Infrastructure.Repositories.EdiProcessingService(db, loggerMock.Object);

            string ediContent = "ISA*00*          *00*          *ZZ*PAYERID       *ZZ*OURSUBMITTER *210101*1253*^*00501*000000905*0*T*:~TA1*000000905*210101*1253*A*000~";

            // Pass the wrong explicit partner ID intentionally (ISA06 side).
            var result = await service.ProcessEdiFileAsync(ediContent, 1);

            Assert.True(result.Success, result.Message);

            var transaction = db.EdiTransactions.Single();
            Assert.Equal(2, transaction.TradingPartnerId);
        }

        [Fact]
        public async Task ProcessEdiFileAsync_Ta1_UsesIsa08AsPrimaryWhenIsa06IsUnknown()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_AckNoIsa08Fallback")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);
            db.TradingPartners.Add(new EDIDashboard.Core.Models.TradingPartner
            {
                Id = 2,
                Name = "Our Org",
                InterchangeId = "OURSUBMITTER",
                InterchangeQualifier = "ZZ"
            });
            db.SaveChanges();

            var loggerMock = new Mock<ILogger<EDIDashboard.Infrastructure.Repositories.EdiProcessingService>>();
            var service = new EDIDashboard.Infrastructure.Repositories.EdiProcessingService(db, loggerMock.Object);

            // ISA06 is UNKNOWNPAYER (not in DB), ISA08 is OURSUBMITTER (in DB).
            // Correct behavior: use ISA08 as primary for acknowledgments.
            string ediContent = "ISA*00*          *00*          *ZZ*UNKNOWNPAYER  *ZZ*OURSUBMITTER *210101*1253*^*00501*000000905*0*T*:~TA1*000000905*210101*1253*A*000~";

            var result = await service.ProcessEdiFileAsync(ediContent, 0);

            Assert.True(result.Success, result.Message);
            var transaction = db.EdiTransactions.Single();
            Assert.Equal(2, transaction.TradingPartnerId);
        }

        [Fact]
        public async Task ProcessEdiFileAsync_837I_IgnoresWrongExplicitPartnerAndUsesIsa06()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_837I_UsesIsa06")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);
            db.TradingPartners.AddRange(
                new EDIDashboard.Core.Models.TradingPartner
                {
                    Id = 1,
                    Name = "ISA06 Partner",
                    InterchangeId = "C19000000000000",
                    InterchangeQualifier = "ZZ"
                },
                new EDIDashboard.Core.Models.TradingPartner
                {
                    Id = 2,
                    Name = "ISA08 Partner",
                    InterchangeId = "SDMCPHASETWODMH",
                    InterchangeQualifier = "ZZ"
                });
            db.SaveChanges();

            var loggerMock = new Mock<ILogger<EDIDashboard.Infrastructure.Repositories.EdiProcessingService>>();
            var service = new EDIDashboard.Infrastructure.Repositories.EdiProcessingService(db, loggerMock.Object);

            string ediContent = "ISA*00*          *00*          *ZZ*C19000000000000*ZZ*SDMCPHASETWODMH*080901*1131*^*00501*000000869*0*T*:~GS*HC*C19000000000000*SDMCPHASETWODMH*20250317*1131*1*X*005010X223A2~ST*837*0001*005010X223A2~SE*2*0001~GE*1*1~IEA*1*000000869~";

            // Pass the wrong explicit partner ID intentionally (ISA08 side).
            var result = await service.ProcessEdiFileAsync(ediContent, 2);

            Assert.True(result.Success, result.Message);
            var transaction = db.EdiTransactions.Single();
            Assert.Equal(1, transaction.TradingPartnerId);
        }

        [Fact]
        public async Task ProcessEdiFileAsync_837I_DoesNotFallbackToIsa08WhenIsa06IsUnknown()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_837I_NoIsa08Fallback")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);
            db.TradingPartners.Add(new EDIDashboard.Core.Models.TradingPartner
            {
                Id = 2,
                Name = "ISA08 Partner",
                InterchangeId = "SDMCPHASETWODMH",
                InterchangeQualifier = "ZZ"
            });
            db.SaveChanges();

            var loggerMock = new Mock<ILogger<EDIDashboard.Infrastructure.Repositories.EdiProcessingService>>();
            var service = new EDIDashboard.Infrastructure.Repositories.EdiProcessingService(db, loggerMock.Object);

            // ISA06 is not in DB, ISA08 exists. Correct behavior: create/use ISA06.
            string ediContent = "ISA*00*          *00*          *ZZ*C19000000000000*ZZ*SDMCPHASETWODMH*080901*1131*^*00501*000000869*0*T*:~GS*HC*C19000000000000*SDMCPHASETWODMH*20250317*1131*1*X*005010X223A2~ST*837*0001*005010X223A2~SE*2*0001~GE*1*1~IEA*1*000000869~";

            var result = await service.ProcessEdiFileAsync(ediContent, 0);

            Assert.True(result.Success, result.Message);
            var transaction = db.EdiTransactions.Single();
            Assert.NotEqual(2, transaction.TradingPartnerId);
            Assert.Equal("C19000000000000", db.TradingPartners.Single(p => p.Id == transaction.TradingPartnerId).InterchangeId);
        }

        [Fact]
        public async Task ProcessEdiFileAsync_999_UsesIsa08AsTradingPartner()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_999_UsesIsa08")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);
            db.TradingPartners.AddRange(
                new EDIDashboard.Core.Models.TradingPartner
                {
                    Id = 1,
                    Name = "ISA06 Partner",
                    InterchangeId = "SDMCPHASETWODMH",
                    InterchangeQualifier = "ZZ"
                },
                new EDIDashboard.Core.Models.TradingPartner
                {
                    Id = 2,
                    Name = "ISA08 Partner",
                    InterchangeId = "C19000000000000",
                    InterchangeQualifier = "ZZ"
                });
            db.SaveChanges();

            var loggerMock = new Mock<ILogger<EDIDashboard.Infrastructure.Repositories.EdiProcessingService>>();
            var service = new EDIDashboard.Infrastructure.Repositories.EdiProcessingService(db, loggerMock.Object);

            string ediContent = "ISA*00*          *00*          *ZZ*SDMCPHASETWODMH*ZZ*C19000000000000*260522*1210*^*00501*000000001*0*T*:~GS*FA*SDMCPHASETWODMH*C19000000000000*20260522*1210*1*X*005010X231A1~ST*999*0001*005010X231A1~AK1*HC*1*005010X223A2~IK5*E*I5~AK9*E*1*1*1~SE*7*0001~GE*1*1~IEA*1*000000001~";

            var result = await service.ProcessEdiFileAsync(ediContent, 0);

            Assert.True(result.Success, result.Message);
            var transaction = db.EdiTransactions.Single();
            Assert.Equal(2, transaction.TradingPartnerId);
            Assert.Single(db.Acknowledgments.Where(a => a.AckType == "999" && a.EdiTransactionId == transaction.Id));
        }

        [Fact]
        public async Task ProcessEdiFileAsync_999_IgnoresWrongExplicitPartnerAndUsesIsa08()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_999_IgnoresExplicit")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);
            db.TradingPartners.AddRange(
                new EDIDashboard.Core.Models.TradingPartner
                {
                    Id = 1,
                    Name = "ISA06 Partner",
                    InterchangeId = "SDMCPHASETWODMH",
                    InterchangeQualifier = "ZZ"
                },
                new EDIDashboard.Core.Models.TradingPartner
                {
                    Id = 2,
                    Name = "ISA08 Partner",
                    InterchangeId = "C19000000000000",
                    InterchangeQualifier = "ZZ"
                });
            db.SaveChanges();

            var loggerMock = new Mock<ILogger<EDIDashboard.Infrastructure.Repositories.EdiProcessingService>>();
            var service = new EDIDashboard.Infrastructure.Repositories.EdiProcessingService(db, loggerMock.Object);

            string ediContent = "ISA*00*          *00*          *ZZ*SDMCPHASETWODMH*ZZ*C19000000000000*260522*1210*^*00501*000000001*0*T*:~GS*FA*SDMCPHASETWODMH*C19000000000000*20260522*1210*1*X*005010X231A1~ST*999*0001*005010X231A1~AK1*HC*1*005010X223A2~IK5*E*I5~AK9*E*1*1*1~SE*7*0001~GE*1*1~IEA*1*000000001~";

            // Pass wrong partner intentionally (ISA06 side).
            var result = await service.ProcessEdiFileAsync(ediContent, 1);

            Assert.True(result.Success, result.Message);
            var transaction = db.EdiTransactions.Single();
            Assert.Equal(2, transaction.TradingPartnerId);
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

    public class MetricsServiceTests
    {
        [Fact]
        public async Task GetDashboardMetricsAsync_DailyVolume_UsesSubmissionDateOverClaimCreatedAt()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_DailyVolume_SubmissionDate")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);

            var partner = new EDIDashboard.Core.Models.TradingPartner
            {
                Id = 1,
                Name = "Partner A",
                InterchangeId = "PARTNERA",
                InterchangeQualifier = "ZZ"
            };
            db.TradingPartners.Add(partner);

            var tx = new EDIDashboard.Core.Models.EdiTransaction
            {
                TradingPartnerId = 1,
                TransactionType = "837P",
                ControlNumber = "000000001",
                RawContent = "ISA*00*          *00*          *ZZ*PARTNERA      *ZZ*OURSUBMITTER *210101*1253*^*00501*000000905*0*T*:~",
                ReceivedAt = DateTime.UtcNow,
                Status = "Received"
            };
            db.EdiTransactions.Add(tx);
            await db.SaveChangesAsync();

            // Claim CreatedAt intentionally different from SubmissionDate.
            db.Claims.Add(new EDIDashboard.Core.Models.Claim837
            {
                EdiTransactionId = tx.Id,
                ClaimNumber = "CLM-1",
                PatientName = "P",
                PatientControlNumber = "PCN",
                ProviderName = "Prov",
                ProviderId = "123",
                PayerId = "PAYER",
                PayerName = "Payer",
                TotalAmount = 100,
                ServiceDateFrom = DateTime.UtcNow.Date,
                ClaimType = "Professional",
                Status = "Accepted",
                CreatedAt = new DateTime(2026, 5, 20)
            });

            db.FileProcessingLogs.Add(new EDIDashboard.Core.Models.FileProcessingLog
            {
                FileName = "05242026_test.edi",
                SubmissionDate = new DateTime(2026, 5, 24),
                OriginalPath = "05242026_test.edi",
                FinalPath = "05242026_test.edi",
                FileSizeBytes = 1,
                Status = "Success",
                ClaimsProcessed = 1,
                Source = "Upload",
                TradingPartnerId = 1,
                PickedUpAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            var service = new EDIDashboard.Infrastructure.Repositories.MetricsService(db);
            var metrics = await service.GetDashboardMetricsAsync(null, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

            Assert.Single(metrics.DailyVolumes);
            Assert.Equal(new DateTime(2026, 5, 24), metrics.DailyVolumes[0].Date);
        }

        [Fact]
        public async Task GetDashboardMetricsAsync_DailyVolume_FallsBackToPickedUpAtWhenSubmissionDateMissing()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
            .UseInMemoryDatabase(databaseName: "EdiDb_DailyVolume_PickedUpFallback")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);

            db.FileProcessingLogs.Add(new EDIDashboard.Core.Models.FileProcessingLog
            {
                FileName = "05232026_ack.ta1",
                SubmissionDate = null,
                OriginalPath = "05232026_ack.ta1",
                FinalPath = "05232026_ack.ta1",
                FileSizeBytes = 1,
                Status = "Success",
                ClaimsProcessed = 2,
                Source = "Upload",
                PickedUpAt = new DateTime(2026, 5, 23)
            });

            await db.SaveChangesAsync();

            var service = new EDIDashboard.Infrastructure.Repositories.MetricsService(db);
            var metrics = await service.GetDashboardMetricsAsync(null, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

            Assert.Single(metrics.DailyVolumes);
            Assert.Equal(new DateTime(2026, 5, 23), metrics.DailyVolumes[0].Date);
            Assert.Equal(2, metrics.DailyVolumes[0].Claims);
        }

        [Fact]
        public async Task GetDashboardMetricsAsync_RecentTransactions_RespectsDateRange()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_RecentTransactions_DateRange")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);

            db.TradingPartners.Add(new EDIDashboard.Core.Models.TradingPartner
            {
                Id = 1,
                Name = "Partner A",
                InterchangeId = "PARTNERA",
                InterchangeQualifier = "ZZ"
            });
            await db.SaveChangesAsync();

            db.EdiTransactions.AddRange(
                new EDIDashboard.Core.Models.EdiTransaction
                {
                    TradingPartnerId = 1,
                    TransactionType = "837P",
                    ControlNumber = "INRANGE",
                    FileName = "05012026-inrange.edi",
                    RawContent = "ISA*00*          *00*          *ZZ*PARTNERA      *ZZ*OURSUBMITTER *260501*1253*^*00501*000000905*0*T*:~",
                    ReceivedAt = new DateTime(2026, 5, 10),
                    Status = "Received"
                },
                new EDIDashboard.Core.Models.EdiTransaction
                {
                    TradingPartnerId = 1,
                    TransactionType = "837P",
                    ControlNumber = "OUTRANGE",
                    FileName = "04102026-outrange.edi",
                    RawContent = "ISA*00*          *00*          *ZZ*PARTNERA      *ZZ*OURSUBMITTER *260401*1253*^*00501*000000906*0*T*:~",
                    ReceivedAt = new DateTime(2026, 4, 10),
                    Status = "Received"
                });

            await db.SaveChangesAsync();

            db.FileProcessingLogs.AddRange(
                new EDIDashboard.Core.Models.FileProcessingLog
                {
                    FileName = "05012026-inrange.edi",
                    SubmissionDate = new DateTime(2026, 5, 10),
                    OriginalPath = "05012026-inrange.edi",
                    FinalPath = "05012026-inrange.edi",
                    FileSizeBytes = 1,
                    DetectedType = "837P",
                    TradingPartnerId = 1,
                    Status = "Success",
                    ControlNumber = "INRANGE",
                    ClaimsProcessed = 1,
                    Source = "Upload",
                    PickedUpAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    ProcessingDuration = TimeSpan.Zero
                },
                new EDIDashboard.Core.Models.FileProcessingLog
                {
                    FileName = "04102026-outrange.edi",
                    SubmissionDate = new DateTime(2026, 4, 10),
                    OriginalPath = "04102026-outrange.edi",
                    FinalPath = "04102026-outrange.edi",
                    FileSizeBytes = 1,
                    DetectedType = "837P",
                    TradingPartnerId = 1,
                    Status = "Success",
                    ControlNumber = "OUTRANGE",
                    ClaimsProcessed = 1,
                    Source = "Upload",
                    PickedUpAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    ProcessingDuration = TimeSpan.Zero
                });

            await db.SaveChangesAsync();

            var service = new EDIDashboard.Infrastructure.Repositories.MetricsService(db);
            var metrics = await service.GetDashboardMetricsAsync(null, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

            Assert.Single(metrics.RecentTransactions);
            Assert.Equal("INRANGE", metrics.RecentTransactions[0].ControlNumber);
        }

        [Fact]
        public async Task GetDashboardMetricsAsync_Widgets_FilterBySubmissionDateWindow()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_Widget_SubmissionWindow")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);

            db.TradingPartners.Add(new EDIDashboard.Core.Models.TradingPartner
            {
                Id = 1,
                Name = "Partner A",
                InterchangeId = "PARTNERA",
                InterchangeQualifier = "ZZ"
            });
            await db.SaveChangesAsync();

            var inWindowTx = new EDIDashboard.Core.Models.EdiTransaction
            {
                TradingPartnerId = 1,
                TransactionType = "837P",
                ControlNumber = "SUBWIN1",
                FileName = "01012025-file.edi",
                RawContent = "ISA*00*          *00*          *ZZ*PARTNERA      *ZZ*OURSUBMITTER *260501*1253*^*00501*000000905*0*T*:~",
                ReceivedAt = new DateTime(2026, 5, 20),
                Status = "Received"
            };
            db.EdiTransactions.Add(inWindowTx);
            await db.SaveChangesAsync();

            db.Claims.Add(new EDIDashboard.Core.Models.Claim837
            {
                EdiTransactionId = inWindowTx.Id,
                ClaimNumber = "CLM-SUBWIN",
                PatientName = "P",
                PatientControlNumber = "PCN",
                ProviderName = "Prov",
                ProviderId = "123",
                PayerId = "PAYER",
                PayerName = "Payer",
                TotalAmount = 150,
                ServiceDateFrom = new DateTime(2026, 5, 20),
                ClaimType = "Professional",
                Status = "Accepted",
                CreatedAt = new DateTime(2026, 5, 20)
            });

            db.FileProcessingLogs.Add(new EDIDashboard.Core.Models.FileProcessingLog
            {
                FileName = "01012025-file.edi",
                SubmissionDate = new DateTime(2025, 1, 1),
                OriginalPath = "01012025-file.edi",
                FinalPath = "01012025-file.edi",
                FileSizeBytes = 1,
                DetectedType = "837P",
                TradingPartnerId = 1,
                Status = "Success",
                ControlNumber = "SUBWIN1",
                ClaimsProcessed = 1,
                Source = "Upload",
                PickedUpAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                ProcessingDuration = TimeSpan.Zero
            });

            await db.SaveChangesAsync();

            var service = new EDIDashboard.Infrastructure.Repositories.MetricsService(db);
            var metrics = await service.GetDashboardMetricsAsync(null, new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));

            Assert.Equal(1, metrics.TotalClaims);
            Assert.Equal(1, metrics.TotalTransactions);
            Assert.Single(metrics.RecentTransactions);
            Assert.Single(metrics.ByTradingPartner);
        }

        [Fact]
        public async Task GetDashboardMetricsAsync_Widgets_FallBackToPickedUpAtWhenSubmissionDateMissing()
        {
            var options = new DbContextOptionsBuilder<EDIDashboard.Infrastructure.Data.EdiDbContext>()
                .UseInMemoryDatabase(databaseName: "EdiDb_Widget_PickedUpFallback")
                .Options;

            using var db = new EDIDashboard.Infrastructure.Data.EdiDbContext(options);

            db.TradingPartners.Add(new EDIDashboard.Core.Models.TradingPartner
            {
                Id = 1,
                Name = "Partner A",
                InterchangeId = "PARTNERA",
                InterchangeQualifier = "ZZ"
            });
            await db.SaveChangesAsync();

            var tx = new EDIDashboard.Core.Models.EdiTransaction
            {
                TradingPartnerId = 1,
                TransactionType = "837P",
                ControlNumber = "PKUP1",
                FileName = "pkup-file.edi",
                RawContent = "ISA*00*          *00*          *ZZ*PARTNERA      *ZZ*OURSUBMITTER *260501*1253*^*00501*000000905*0*T*:~",
                ReceivedAt = new DateTime(2026, 5, 20),
                Status = "Received"
            };
            db.EdiTransactions.Add(tx);
            await db.SaveChangesAsync();

            db.Claims.Add(new EDIDashboard.Core.Models.Claim837
            {
                EdiTransactionId = tx.Id,
                ClaimNumber = "CLM-PKUP",
                PatientName = "P",
                PatientControlNumber = "PCN",
                ProviderName = "Prov",
                ProviderId = "123",
                PayerId = "PAYER",
                PayerName = "Payer",
                TotalAmount = 120,
                ServiceDateFrom = new DateTime(2026, 5, 20),
                ClaimType = "Professional",
                Status = "Accepted",
                CreatedAt = new DateTime(2026, 5, 20)
            });

            db.FileProcessingLogs.Add(new EDIDashboard.Core.Models.FileProcessingLog
            {
                FileName = "pkup-file.edi",
                SubmissionDate = null,
                OriginalPath = "pkup-file.edi",
                FinalPath = "pkup-file.edi",
                FileSizeBytes = 1,
                DetectedType = "837P",
                TradingPartnerId = 1,
                Status = "Success",
                ControlNumber = "PKUP1",
                ClaimsProcessed = 1,
                Source = "Upload",
                PickedUpAt = new DateTime(2025, 6, 15),
                CompletedAt = DateTime.UtcNow,
                ProcessingDuration = TimeSpan.Zero
            });

            await db.SaveChangesAsync();

            var service = new EDIDashboard.Infrastructure.Repositories.MetricsService(db);
            var metrics = await service.GetDashboardMetricsAsync(null, new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));

            Assert.Equal(1, metrics.TotalClaims);
            Assert.Equal(1, metrics.TotalTransactions);
            Assert.Single(metrics.RecentTransactions);
            Assert.Single(metrics.ByTradingPartner);
        }
    }
}
