using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDIDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEdiTransactionFileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "EdiTransactions",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileName",
                table: "EdiTransactions");
        }
    }
}
