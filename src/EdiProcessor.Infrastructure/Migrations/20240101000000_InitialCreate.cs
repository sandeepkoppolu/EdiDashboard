using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace EdiProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradingPartners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    InterchangeId = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    InterchangeQualifier = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_TradingPartners", x => x.Id); });

            migrationBuilder.CreateTable(
                name: "EdiTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TradingPartnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ControlNumber = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    RawContent = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ErrorDescription = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdiTransactions", x => x.Id);
                    table.ForeignKey("FK_EdiTransactions_TradingPartners_TradingPartnerId",
                        x => x.TradingPartnerId, "TradingPartners", "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EdiTransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaimNumber = table.Column<string>(type: "TEXT", maxLength: 38, nullable: false),
                    PatientName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PatientControlNumber = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProviderId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PayerId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PayerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    ServiceDateFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ServiceDateTo = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClaimType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey("FK_Claims_EdiTransactions_EdiTransactionId",
                        x => x.EdiTransactionId, "EdiTransactions", "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Claim837Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcedureCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Modifier = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ChargedAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Units = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DiagnosisPointers = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLines", x => x.Id);
                    table.ForeignKey("FK_ServiceLines_Claims_Claim837Id",
                        x => x.Claim837Id, "Claims", "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acknowledgments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EdiTransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AckType = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    ControlNumber = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    AcknowledgmentCode = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    NoteCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FunctionalGroupControlNumber = table.Column<string>(type: "TEXT", nullable: true),
                    TransactionSetControlNumber = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acknowledgments", x => x.Id);
                    table.ForeignKey("FK_Acknowledgments_EdiTransactions_EdiTransactionId",
                        x => x.EdiTransactionId, "EdiTransactions", "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Indexes
            migrationBuilder.CreateIndex("IX_TradingPartners_InterchangeId", "TradingPartners", "InterchangeId");
            migrationBuilder.CreateIndex("IX_EdiTransactions_ControlNumber", "EdiTransactions", "ControlNumber");
            migrationBuilder.CreateIndex("IX_EdiTransactions_ReceivedAt", "EdiTransactions", "ReceivedAt");
            migrationBuilder.CreateIndex("IX_EdiTransactions_TradingPartnerId", "EdiTransactions", "TradingPartnerId");
            migrationBuilder.CreateIndex("IX_Claims_Status", "Claims", "Status");
            migrationBuilder.CreateIndex("IX_Claims_CreatedAt", "Claims", "CreatedAt");
            migrationBuilder.CreateIndex("IX_Claims_EdiTransactionId", "Claims", "EdiTransactionId");
            migrationBuilder.CreateIndex("IX_ServiceLines_Claim837Id", "ServiceLines", "Claim837Id");
            migrationBuilder.CreateIndex("IX_Acknowledgments_EdiTransactionId", "Acknowledgments", "EdiTransactionId");

            // Seed
            migrationBuilder.InsertData("TradingPartners",
                new[] { "Id", "Name", "InterchangeId", "InterchangeQualifier", "IsActive", "CreatedAt" },
                new object[] { 1, "Blue Cross Blue Shield", "BCBS001", "ZZ", true, new DateTime(2024, 1, 1) });
            migrationBuilder.InsertData("TradingPartners",
                new[] { "Id", "Name", "InterchangeId", "InterchangeQualifier", "IsActive", "CreatedAt" },
                new object[] { 2, "Aetna Health", "AETNA01", "ZZ", true, new DateTime(2024, 1, 1) });
            migrationBuilder.InsertData("TradingPartners",
                new[] { "Id", "Name", "InterchangeId", "InterchangeQualifier", "IsActive", "CreatedAt" },
                new object[] { 3, "UnitedHealthcare", "UHC0001", "ZZ", true, new DateTime(2024, 1, 1) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Acknowledgments");
            migrationBuilder.DropTable("ServiceLines");
            migrationBuilder.DropTable("Claims");
            migrationBuilder.DropTable("EdiTransactions");
            migrationBuilder.DropTable("TradingPartners");
        }
    }
}
