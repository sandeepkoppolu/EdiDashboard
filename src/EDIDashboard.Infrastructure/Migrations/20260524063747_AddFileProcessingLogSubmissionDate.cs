using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDIDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileProcessingLogSubmissionDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubmissionDate",
                table: "FileProcessingLogs",
                type: "datetime2",
                nullable: true);

                        migrationBuilder.Sql(@"
UPDATE FileProcessingLogs
SET SubmissionDate = TRY_CONVERT(date, STUFF(STUFF(LEFT(FileName, 8), 3, 0, '/'), 6, 0, '/'), 101)
WHERE SubmissionDate IS NULL
    AND LEN(FileName) >= 8;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmissionDate",
                table: "FileProcessingLogs");
        }
    }
}
