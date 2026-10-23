using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyTrainingScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Holding_Amounts",
                table: "Holdings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Holding_EmptyCost",
                table: "Holdings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transaction_Shape",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Investments");

            migrationBuilder.DropColumn(
                name: "PriceUpdatedAt",
                table: "Investments");

            migrationBuilder.DropColumn(
                name: "RemainingCost",
                table: "Holdings");

            migrationBuilder.DropColumn(
                name: "RealizedGain",
                table: "FinancialTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Holding_Amounts",
                table: "Holdings",
                sql: "[Quantity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transaction_Shape",
                table: "FinancialTransactions",
                sql: "([Type] IN ('Deposit', 'Withdrawal') AND [PortfolioId] IS NULL AND [InvestmentId] IS NULL AND [Quantity] IS NULL AND [UnitPrice] IS NULL) OR ([Type] IN ('Buy', 'Sell') AND [PortfolioId] IS NOT NULL AND [InvestmentId] IS NOT NULL AND [Quantity] IS NOT NULL AND [Quantity] > 0 AND [UnitPrice] IS NOT NULL AND [UnitPrice] > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Restore a pre-migration database backup to return to the previous financial reporting schema.");
        }
    }
}
