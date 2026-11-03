using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Sql.Migrations
{
    /// <summary>
    /// Follow-up migration that changes only two stored procedures (no table changes, so the model snapshot stays the
    /// same). usp_DailyBranchSales and usp_LowStockReport now check their parameters and raise a clear error with THROW
    /// instead of silently returning an empty result:
    ///   50020 the day is NULL, 50021 the branch code is not like BR001,
    ///   50022 the threshold is NULL or negative, 50023 the product category does not exist.
    /// The application never sends such values (BranchCode is a value object and SqlReportService answers a negative
    /// threshold itself), so these checks protect callers that use the procedures directly. The query part of each
    /// procedure is unchanged. It is written by hand, so the migration id and the context are declared here.
    /// </summary>
    [DbContext(typeof(ApexPostDbContext))]
    [Migration("20261103120000_ReportProceduresValidateParameters")]
    public class ReportProceduresValidateParameters : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
                    @Day        date,
                    @BranchCode varchar(5) = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @Day IS NULL
                        THROW 50020, 'The day is required.', 1;
                    IF @BranchCode IS NOT NULL AND @BranchCode NOT LIKE 'BR[0-9][0-9][0-9]'
                        THROW 50021, 'The branch code must look like BR001.', 1;

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

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
                    @BranchCode varchar(5) = NULL,
                    @CategoryId int        = NULL,
                    @Threshold  int        = 10
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @Threshold IS NULL OR @Threshold < 0
                        THROW 50022, 'The threshold must be zero or more.', 1;
                    IF @BranchCode IS NOT NULL AND @BranchCode NOT LIKE 'BR[0-9][0-9][0-9]'
                        THROW 50021, 'The branch code must look like BR001.', 1;
                    IF @CategoryId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.ProductCategories WHERE Id = @CategoryId)
                        THROW 50023, 'The product category does not exist.', 1;

                    SELECT s.BranchCode, s.Sku, p.Name AS Product, c.Name AS Category, s.Quantity
                    FROM BranchStock AS s
                    JOIN Products AS p ON p.Sku = s.Sku
                    JOIN ProductCategories AS c ON c.Id = p.CategoryId
                    WHERE s.Quantity <= @Threshold
                      AND (@BranchCode IS NULL OR s.BranchCode = @BranchCode)
                      AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
                    ORDER BY s.Quantity, s.BranchCode, s.Sku
                    OPTION (RECOMPILE);
                END;
                """);
        }

        /// <summary>Puts back the versions without the parameter checks.</summary>
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

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
                    @BranchCode varchar(5) = NULL,
                    @CategoryId int        = NULL,
                    @Threshold  int        = 10
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT s.BranchCode, s.Sku, p.Name AS Product, c.Name AS Category, s.Quantity
                    FROM BranchStock AS s
                    JOIN Products AS p ON p.Sku = s.Sku
                    JOIN ProductCategories AS c ON c.Id = p.CategoryId
                    WHERE s.Quantity <= @Threshold
                      AND (@BranchCode IS NULL OR s.BranchCode = @BranchCode)
                      AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
                    ORDER BY s.Quantity, s.BranchCode, s.Sku
                    OPTION (RECOMPILE);
                END;
                """);
        }
    }
}
