using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EDIDashboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add277CASupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Claims277CA",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EdiTransactionId = table.Column<int>(type: "int", nullable: false),
                    Linked837TransactionId = table.Column<int>(type: "int", nullable: true),
                    SubmitterClaimId = table.Column<string>(type: "nvarchar(38)", maxLength: 38, nullable: false),
                    PayerClaimNumber = table.Column<string>(type: "nvarchar(38)", maxLength: 38, nullable: true),
                    StatusCategoryCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    StatusCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    StatusDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    StatusDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    TotalClaimChargeAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    PaymentAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    PatientName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Claims277CA");
        }
    }
}
