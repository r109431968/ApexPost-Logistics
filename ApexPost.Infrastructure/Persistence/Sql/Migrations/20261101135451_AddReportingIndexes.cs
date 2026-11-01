using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_BranchCode",
                table: "CounterTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLines_LineType_Sku",
                table: "TransactionLines",
                columns: new[] { "LineType", "Sku" })
                .Annotation("SqlServer:Include", new[] { "Quantity", "UnitPrice" });

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_BranchCode_OpenedAtUtc",
                table: "CounterTransactions",
                columns: new[] { "BranchCode", "OpenedAtUtc" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "Status", "TaxTotal" });

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_BranchCode_Status_OpenedAtUtc",
                table: "CounterTransactions",
                columns: new[] { "BranchCode", "Status", "OpenedAtUtc" })
                .Annotation("SqlServer:Include", new[] { "TaxTotal" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TimestampUtc",
                table: "AuditEntries",
                column: "TimestampUtc",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionLines_LineType_Sku",
                table: "TransactionLines");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_BranchCode_OpenedAtUtc",
                table: "CounterTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_BranchCode_Status_OpenedAtUtc",
                table: "CounterTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_TimestampUtc",
                table: "AuditEntries");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_BranchCode",
                table: "CounterTransactions",
                column: "BranchCode");
        }
    }
}
