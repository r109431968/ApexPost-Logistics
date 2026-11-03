/* Same text as the migration AdjustStockCreatesMissingRows. */
CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
    @BranchCode    varchar(5),
    @Sku           varchar(8),
    @Delta         int,                 -- positive adds stock, negative removes it
    @Reason        nvarchar(200),
    @ChangedBy     nvarchar(100),
    @QuantityAfter int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;                  -- any run-time error rolls the whole transaction back

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

            -- First stock of this item at this branch (positive change only).
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
        THROW;                          -- re-raise the original error to the caller
    END CATCH;
END;
GO

DECLARE @q int;
EXEC dbo.usp_AdjustStock @BranchCode = 'BR001', @Sku = 'STA-0010', @Delta = 5,
     @Reason = N'Delivery received', @ChangedBy = N'manager.br001@apexpost.test', @QuantityAfter = @q OUTPUT;
SELECT @q AS QuantityAfter;


DECLARE @q int;
EXEC dbo.usp_AdjustStock @BranchCode = 'BR001', @Sku = 'STA-0010', @Delta = -5,
     @Reason = N'Correction', @ChangedBy = N'manager.br001@apexpost.test', @QuantityAfter = @q OUTPUT;
SELECT @q AS QuantityAfter;

DECLARE @q int;
EXEC dbo.usp_AdjustStock @BranchCode = 'BR001', @Sku = 'STA-0010', @Delta = -100000,
     @Reason = N'Should fail', @ChangedBy = N'manager.br001@apexpost.test', @QuantityAfter = @q OUTPUT;

     SELECT TOP 5 Id, BranchCode, Sku, Delta, QuantityAfter, Reason, ChangedBy FROM StockAdjustments ORDER BY Id DESC;
SELECT BranchCode, Sku, Quantity FROM BranchStock WHERE BranchCode = 'BR001' AND Sku = 'STA-0010';