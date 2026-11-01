/* Moves stock between two branches in ONE transaction: both updates succeed together or neither does.
   Run it twice: first with @Quantity = 1 (commits), then with @Quantity = 100000 (fails and rolls back). */
SET XACT_ABORT ON;   -- any run-time error aborts the batch and rolls the transaction back

DECLARE @FromBranch varchar(5) = 'BR001', @ToBranch varchar(5) = 'BR002';
DECLARE @Sku varchar(8) = 'STA-0010', @Quantity int = 100000;   -- change to 100000 for the failing run

SELECT 'BEFORE' AS Step, BranchCode, Sku, Quantity
FROM BranchStock WHERE Sku = @Sku AND BranchCode IN (@FromBranch, @ToBranch);

BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE BranchStock
    SET Quantity = Quantity - @Quantity, ModifiedAtUtc = SYSUTCDATETIME()
    WHERE BranchCode = @FromBranch AND Sku = @Sku AND Quantity >= @Quantity;   -- never go below zero

    IF @@ROWCOUNT <> 1
        THROW 50001, 'The source branch does not have enough stock (or the SKU does not exist there).', 1;

    UPDATE BranchStock
    SET Quantity = Quantity + @Quantity, ModifiedAtUtc = SYSUTCDATETIME()
    WHERE BranchCode = @ToBranch AND Sku = @Sku;

    IF @@ROWCOUNT <> 1
        THROW 50002, 'The destination branch does not stock this SKU.', 1;

    COMMIT TRANSACTION;
    PRINT 'Committed.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0      -- 1 = can commit, -1 = doomed; in both cases the only safe action is rollback
        ROLLBACK TRANSACTION;

    PRINT 'Rolled back: ' + ERROR_MESSAGE();
    THROW;                    -- re-raise the original error to the caller
END CATCH;

SELECT 'AFTER' AS Step, BranchCode, Sku, Quantity
FROM BranchStock WHERE Sku = @Sku AND BranchCode IN (@FromBranch, @ToBranch);