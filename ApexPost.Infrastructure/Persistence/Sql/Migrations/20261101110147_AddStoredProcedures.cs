using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CREATE OR ALTER is safe to run on a database that already has the procedures.
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

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
                    @BranchCode    varchar(5),
                    @Sku           varchar(8),
                    @Delta         int,
                    @Reason        nvarchar(200),
                    @ChangedBy     nvarchar(100),
                    @QuantityAfter int OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    IF @Delta = 0
                        THROW 50010, 'The adjustment must not be zero.', 1;
                    IF LEN(LTRIM(RTRIM(COALESCE(@Reason, N'')))) = 0
                        THROW 50011, 'A reason is required.', 1;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        UPDATE dbo.BranchStock
                        SET Quantity = Quantity + @Delta,
                            ModifiedAtUtc = SYSUTCDATETIME()
                        WHERE BranchCode = @BranchCode AND Sku = @Sku AND Quantity + @Delta >= 0;

                        IF @@ROWCOUNT = 0
                        BEGIN
                            IF NOT EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchCode = @BranchCode AND Sku = @Sku)
                                THROW 50012, 'This branch does not stock that item.', 1;
                            THROW 50013, 'The adjustment would take the stock below zero.', 1;
                        END;

                        SELECT @QuantityAfter = Quantity
                        FROM dbo.BranchStock
                        WHERE BranchCode = @BranchCode AND Sku = @Sku;

                        INSERT INTO dbo.StockAdjustments (BranchCode, Sku, Delta, QuantityAfter, Reason, ChangedBy, CreatedBy)
                        VALUES (@BranchCode, @Sku, @Delta, @QuantityAfter, @Reason, @ChangedBy, @ChangedBy);

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF XACT_STATE() <> 0
                            ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_AdjustStock;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_LowStockReport;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_DailyBranchSales;");
        }
    }
}
