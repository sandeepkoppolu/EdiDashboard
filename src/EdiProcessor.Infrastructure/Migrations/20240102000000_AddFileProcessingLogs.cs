using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdiProcessor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileProcessingLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileProcessingLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    OriginalPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FinalPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    DetectedType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    TradingPartnerId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ControlNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ClaimsProcessed = table.Column<int>(type: "INTEGER", nullable: false),
                    PickedUpAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProcessingDuration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_FileProcessingLogs", x => x.Id); });

            migrationBuilder.CreateIndex("IX_FileProcessingLogs_PickedUpAt",  "FileProcessingLogs", "PickedUpAt");
            migrationBuilder.CreateIndex("IX_FileProcessingLogs_Status",       "FileProcessingLogs", "Status");
            migrationBuilder.CreateIndex("IX_FileProcessingLogs_TradingPartner","FileProcessingLogs", "TradingPartnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("FileProcessingLogs");
        }
    }
}
