using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EdiProcessor.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileProcessingLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    OriginalPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FinalPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DetectedType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ControlNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ClaimsProcessed = table.Column<int>(type: "integer", nullable: false),
                    PickedUpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingDuration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileProcessingLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingPartners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InterchangeId = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    InterchangeQualifier = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingPartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EdiTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradingPartnerId = table.Column<int>(type: "integer", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ControlNumber = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    RawContent = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorDescription = table.Column<string>(type: "text", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EdiTransactionId = table.Column<int>(type: "integer", nullable: false),
                    AckType = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ControlNumber = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    AcknowledgmentCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    NoteCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FunctionalGroupControlNumber = table.Column<string>(type: "text", nullable: true),
                    TransactionSetControlNumber = table.Column<string>(type: "text", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EdiTransactionId = table.Column<int>(type: "integer", nullable: false),
                    ClaimNumber = table.Column<string>(type: "character varying(38)", maxLength: 38, nullable: false),
                    PatientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PatientControlNumber = table.Column<string>(type: "text", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PayerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PayerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ServiceDateFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ServiceDateTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "Claims277CA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EdiTransactionId = table.Column<int>(type: "integer", nullable: false),
                    Linked837TransactionId = table.Column<int>(type: "integer", nullable: true),
                    SubmitterClaimId = table.Column<string>(type: "character varying(38)", maxLength: 38, nullable: false),
                    PayerClaimNumber = table.Column<string>(type: "character varying(38)", maxLength: 38, nullable: true),
                    StatusCategoryCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    StatusCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    StatusDescription = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    StatusDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActionCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    TotalClaimChargeAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    PaymentAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    PatientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims277CA", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims277CA_EdiTransactions_EdiTransactionId",
                        column: x => x.EdiTransactionId,
                        principalTable: "EdiTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Claims277CA_EdiTransactions_Linked837TransactionId",
                        column: x => x.Linked837TransactionId,
                        principalTable: "EdiTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Claim837Id = table.Column<int>(type: "integer", nullable: false),
                    ProcedureCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Modifier = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ChargedAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Units = table.Column<int>(type: "integer", nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DiagnosisPointers = table.Column<string>(type: "text", nullable: false)
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
                name: "IX_Claims277CA_EdiTransactionId",
                table: "Claims277CA",
                column: "EdiTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims277CA_Linked837TransactionId",
                table: "Claims277CA",
                column: "Linked837TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims277CA_ReceivedAt",
                table: "Claims277CA",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Claims277CA_SubmitterClaimId",
                table: "Claims277CA",
                column: "SubmitterClaimId");

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
                name: "Claims277CA");

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
