using EdiProcessor.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EdiProcessor.Infrastructure.Data;

public class EdiDbContext : DbContext
{
    public EdiDbContext(DbContextOptions<EdiDbContext> options) : base(options) { }

    public DbSet<TradingPartner> TradingPartners => Set<TradingPartner>();
    public DbSet<EdiTransaction> EdiTransactions => Set<EdiTransaction>();
    public DbSet<Claim837> Claims => Set<Claim837>();
    public DbSet<ServiceLine> ServiceLines => Set<ServiceLine>();
    public DbSet<AcknowledgmentRecord> Acknowledgments => Set<AcknowledgmentRecord>();
    public DbSet<FileProcessingLog> FileProcessingLogs => Set<FileProcessingLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TradingPartner
        modelBuilder.Entity<TradingPartner>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.InterchangeId).HasMaxLength(15);
            e.Property(x => x.InterchangeQualifier).HasMaxLength(2);
            e.HasIndex(x => x.InterchangeId);
        });

        // EdiTransaction
        modelBuilder.Entity<EdiTransaction>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TransactionType).IsRequired().HasMaxLength(10);
            e.Property(x => x.ControlNumber).HasMaxLength(9);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.RawContent).IsRequired();
            e.HasOne(x => x.TradingPartner)
             .WithMany(x => x.Transactions)
             .HasForeignKey(x => x.TradingPartnerId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ControlNumber);
            e.HasIndex(x => x.ReceivedAt);
            e.HasIndex(x => x.TradingPartnerId);
        });

        // Claim837
        modelBuilder.Entity<Claim837>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ClaimNumber).HasMaxLength(38);
            e.Property(x => x.PatientName).HasMaxLength(100);
            e.Property(x => x.ProviderName).HasMaxLength(100);
            e.Property(x => x.ProviderId).HasMaxLength(20);
            e.Property(x => x.PayerId).HasMaxLength(50);
            e.Property(x => x.PayerName).HasMaxLength(100);
            e.Property(x => x.TotalAmount).HasPrecision(12, 2);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.ClaimType).HasMaxLength(20);
            e.HasOne(x => x.EdiTransaction)
             .WithMany(x => x.Claims)
             .HasForeignKey(x => x.EdiTransactionId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
        });

        // ServiceLine
        modelBuilder.Entity<ServiceLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ProcedureCode).HasMaxLength(10);
            e.Property(x => x.Modifier).HasMaxLength(10);
            e.Property(x => x.ChargedAmount).HasPrecision(12, 2);
            e.HasOne(x => x.Claim837)
             .WithMany(x => x.ServiceLines)
             .HasForeignKey(x => x.Claim837Id)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // AcknowledgmentRecord
        modelBuilder.Entity<AcknowledgmentRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AckType).HasMaxLength(5);
            e.Property(x => x.ControlNumber).HasMaxLength(9);
            e.Property(x => x.AcknowledgmentCode).HasMaxLength(5);
            e.Property(x => x.NoteCode).HasMaxLength(10);
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasOne(x => x.EdiTransaction)
             .WithMany(x => x.Acknowledgments)
             .HasForeignKey(x => x.EdiTransactionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // FileProcessingLog
        modelBuilder.Entity<FileProcessingLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).IsRequired().HasMaxLength(260);
            e.Property(x => x.OriginalPath).HasMaxLength(500);
            e.Property(x => x.FinalPath).HasMaxLength(500);
            e.Property(x => x.DetectedType).HasMaxLength(20);
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.Property(x => x.ControlNumber).HasMaxLength(20);
            e.Property(x => x.Source).IsRequired().HasMaxLength(30);
            e.HasIndex(x => x.PickedUpAt);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.TradingPartnerId);
        });

        // Seed data
        modelBuilder.Entity<TradingPartner>().HasData(
            new TradingPartner { Id = 1, Name = "Blue Cross Blue Shield", InterchangeId = "BCBS001", InterchangeQualifier = "ZZ", IsActive = true, CreatedAt = new DateTime(2024, 1, 1) },
            new TradingPartner { Id = 2, Name = "Aetna Health", InterchangeId = "AETNA01", InterchangeQualifier = "ZZ", IsActive = true, CreatedAt = new DateTime(2024, 1, 1) },
            new TradingPartner { Id = 3, Name = "UnitedHealthcare", InterchangeId = "UHC0001", InterchangeQualifier = "ZZ", IsActive = true, CreatedAt = new DateTime(2024, 1, 1) }
        );
    }
}
