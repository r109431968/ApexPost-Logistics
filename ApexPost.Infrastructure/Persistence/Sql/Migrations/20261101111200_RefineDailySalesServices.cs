using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class RefineDailySalesServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Services = shipping lines + the FEE of money orders (the order amount itself is not revenue).
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
                    @Day        date,
                    @BranchCode varchar(5) = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT t.BranchCode,
                           @Day AS SaleDate,
                           COALESCE(SUM(CASE WHEN l.LineType = 'Retail' THEN l.Quantity * l.UnitPrice END), 0) AS RetailSales,
                           COALESCE(SUM(CASE WHEN l.LineType = 'Shipping' THEN l.Quantity * l.UnitPrice
                                             WHEN l.LineType = 'MoneyOrder' THEN mo.Fee END), 0) AS ServiceSales,
                           COUNT(DISTINCT t.Id) AS Transactions
                    FROM CounterTransactions AS t
                    JOIN TransactionLines AS l ON l.TransactionId = t.Id
                    LEFT JOIN MoneyOrders AS mo ON mo.TransactionId = l.TransactionId AND mo.LineNumber = l.LineNumber
                    WHERE t.Status = 2
                      AND t.OpenedAtUtc >= @Day
                      AND t.OpenedAtUtc <  DATEADD(DAY, 1, @Day)
                      AND (@BranchCode IS NULL OR t.BranchCode = @BranchCode)
                    GROUP BY t.BranchCode
                    ORDER BY t.BranchCode;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
                    @Day        date,
                    @BranchCode varchar(5) = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT t.BranchCode,
                           @Day AS SaleDate,
                           COALESCE(SUM(CASE WHEN l.LineType = 'Retail' THEN l.Quantity * l.UnitPrice END), 0) AS RetailSales,
                           COALESCE(SUM(CASE WHEN l.LineType IN ('Shipping', 'MoneyOrder') THEN l.Quantity * l.UnitPrice END), 0) AS ServiceSales,
                           COALESCE(SUM(l.Quantity * l.UnitPrice), 0) AS NetSales,
                           COUNT(DISTINCT t.Id) AS Transactions
                    FROM CounterTransactions AS t
                    JOIN TransactionLines AS l ON l.TransactionId = t.Id
                    WHERE t.Status = 2
                      AND t.OpenedAtUtc >= @Day
                      AND t.OpenedAtUtc <  DATEADD(DAY, 1, @Day)
                      AND (@BranchCode IS NULL OR t.BranchCode = @BranchCode)
                    GROUP BY t.BranchCode
                    ORDER BY t.BranchCode;
                END;
                """);
        }
    }
}
