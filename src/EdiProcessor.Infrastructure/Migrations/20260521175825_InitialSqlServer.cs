using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EdiProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileProcessingLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    OriginalPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FinalPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DetectedType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ControlNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ClaimsProcessed = table.Column<int>(type: "int", nullable: false),
                    PickedUpAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingDuration = table.Column<TimeSpan>(type: "time", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileProcessingLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingPartners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InterchangeId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    InterchangeQualifier = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingPartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EdiTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingPartnerId = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ControlNumber = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    RawContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorDescription = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdiTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EdiTransactions_TradingPartners_TradingPartnerId",
                        column: x => x.TradingPartnerId,
                        principalTable: "TradingPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Acknowledgments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EdiTransactionId = table.Column<int>(type: "int", nullable: false),
                    AckType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ControlNumber = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    AcknowledgmentCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    NoteCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FunctionalGroupControlNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionSetControlNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acknowledgments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acknowledgments_EdiTransactions_EdiTransactionId",
                        column: x => x.EdiTransactionId,
                        principalTable: "EdiTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EdiTransactionId = table.Column<int>(type: "int", nullable: false),
                    ClaimNumber = table.Column<string>(type: "nvarchar(38)", maxLength: 38, nullable: false),
                    PatientName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PatientControlNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PayerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PayerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    ServiceDateFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceDateTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims_EdiTransactions_EdiTransactionId",
                        column: x => x.EdiTransactionId,
                        principalTable: "EdiTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Claim837Id = table.Column<int>(type: "int", nullable: false),
                    ProcedureCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Modifier = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ChargedAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Units = table.Column<int>(type: "int", nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DiagnosisPointers = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLines_Claims_Claim837Id",
                        column: x => x.Claim837Id,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "TradingPartners",
                columns: new[] { "Id", "CreatedAt", "InterchangeId", "InterchangeQualifier", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "BCBS001", "ZZ", true, "Blue Cross Blue Shield" },
                    { 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "AETNA01", "ZZ", true, "Aetna Health" },
                    { 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "UHC0001", "ZZ", true, "UnitedHealthcare" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acknowledgments_EdiTransactionId",
                table: "Acknowledgments",
                column: "EdiTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_CreatedAt",
                table: "Claims",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_EdiTransactionId",
                table: "Claims",
                column: "EdiTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Status",
                table: "Claims",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EdiTransactions_ControlNumber",
                table: "EdiTransactions",
                column: "ControlNumber");

            migrationBuilder.CreateIndex(
                name: "IX_EdiTransactions_ReceivedAt",
                table: "EdiTransactions",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EdiTransactions_TradingPartnerId",
                table: "EdiTransactions",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_FileProcessingLogs_PickedUpAt",
                table: "FileProcessingLogs",
                column: "PickedUpAt");

            migrationBuilder.CreateIndex(
                name: "IX_FileProcessingLogs_Status",
                table: "FileProcessingLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FileProcessingLogs_TradingPartnerId",
                table: "FileProcessingLogs",
                column: "TradingPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLines_Claim837Id",
                table: "ServiceLines",
                column: "Claim837Id");

            migrationBuilder.CreateIndex(
                name: "IX_TradingPartners_InterchangeId",
                table: "TradingPartners",
                column: "InterchangeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acknowledgments");

            migrationBuilder.DropTable(
                name: "FileProcessingLogs");

            migrationBuilder.DropTable(
                name: "ServiceLines");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "EdiTransactions");

            migrationBuilder.DropTable(
                name: "TradingPartners");
        }
    }
}
