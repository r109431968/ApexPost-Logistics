using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Sql.Migrations
{
    /// <summary>
    /// Follow-up migration that changes only a stored procedure (no table changes, so the model snapshot stays the same).
    /// usp_AdjustStock now creates the BranchStock row when a branch receives an item it has never stocked
    /// (positive change only), the same rule the repository path in CatalogueService has. A negative change on a
    /// missing row is still rejected. It is written by hand, so the migration id and the context are declared here.
    /// </summary>
    [DbContext(typeof(ApexPostDbContext))]
    [Migration("20261101140000_AdjustStockCreatesMissingRows")]
    public class AdjustStockCreatesMissingRows : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

                        -- The WHERE clause refuses an update that would take the stock below zero.
                        UPDATE dbo.BranchStock
                        SET Quantity = Quantity + @Delta,
                            ModifiedAtUtc = SYSUTCDATETIME()
                        WHERE BranchCode = @BranchCode AND Sku = @Sku AND Quantity + @Delta >= 0;

                        IF @@ROWCOUNT = 0
                        BEGIN
                            IF EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchCode = @BranchCode AND Sku = @Sku)
                                THROW 50013, 'The adjustment would take the stock below zero.', 1;
                            IF @Delta < 0
                                THROW 50013, 'The adjustment would take the stock below zero.', 1;

                            -- First stock of this item at this branch.
                            INSERT INTO dbo.BranchStock (BranchCode, Sku, Quantity, CreatedBy)
                            VALUES (@BranchCode, @Sku, @Delta, @ChangedBy);
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to the first version of the procedure (a missing row is an error).
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
    }
}
