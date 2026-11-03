/* Records ONE counter sale atomically: the transaction, its two lines, the payment and the stock decrement.
   Either every statement succeeds or none of them does.

   Uses: BEGIN TRAN, TRY...CATCH, SET XACT_ABORT ON, XACT_STATE() and THROW.

   How to run:
     1. Run it as it is (@Quantity = 1): the sale commits. The AFTER result shows stock down by 1 on both items.
     2. Change @Quantity to 100000 and run again: the stock guard fails, THROW 50013 is raised, and the
        CATCH block rolls everything back. The AFTER result shows the same stock as before, and no new transaction.
     3. The demo rows are tagged CreatedBy = 'sql-demo-sale'. The last block removes them (optional).

   Stock rows are updated in SKU order, the same order the application uses, so two sales cannot deadlock each other
   (see sql/04_deadlock_avoidance.md).
   Tax: the application calculates tax. This demo records TaxTotal = 0 and says so, rather than copying a tax rule. */
USE ApexPostDb;
GO
SET XACT_ABORT ON;   -- a run-time error aborts the batch and rolls the transaction back
SET NOCOUNT ON;

DECLARE @Branch   varchar(5) = 'BR001';
DECLARE @Quantity int        = 1;               -- change to 100000 for the failing run
DECLARE @TxId     uniqueidentifier = NEWID();
DECLARE @Now      datetime2 = SYSUTCDATETIME();

-- The sale needs a counter and an active clerk of this branch, and two products with enough stock.
DECLARE @CounterId uniqueidentifier = (SELECT TOP (1) Id FROM Counters WHERE BranchCode = @Branch ORDER BY Label);
DECLARE @ClerkId   uniqueidentifier =
(
    SELECT TOP (1) s.Id
    FROM Staff AS s JOIN Roles AS r ON r.Id = s.RoleId
    WHERE s.HomeBranchCode = @Branch AND s.IsActive = 1 AND r.Name = N'CounterClerk'
    ORDER BY s.Email
);

DECLARE @Items TABLE (Pos int PRIMARY KEY, Sku varchar(8), Name nvarchar(100), UnitPrice decimal(18,2), TaxCategory int);

INSERT INTO @Items (Pos, Sku, Name, UnitPrice, TaxCategory)
SELECT ROW_NUMBER() OVER (ORDER BY p.Sku), p.Sku, p.Name, p.UnitPrice, p.TaxCategory
FROM (SELECT TOP (2) p2.Sku, p2.Name, p2.UnitPrice, p2.TaxCategory
      FROM Products AS p2
      JOIN BranchStock AS bs ON bs.Sku = p2.Sku AND bs.BranchCode = @Branch
      WHERE bs.Quantity >= 1
      ORDER BY p2.Sku) AS p;

DECLARE @Sku1 varchar(8) = (SELECT Sku FROM @Items WHERE Pos = 1);
DECLARE @Sku2 varchar(8) = (SELECT Sku FROM @Items WHERE Pos = 2);

SELECT 'BEFORE' AS Step, BranchCode, Sku, Quantity
FROM BranchStock WHERE BranchCode = @Branch AND Sku IN (@Sku1, @Sku2) ORDER BY Sku;

BEGIN TRY
    IF @CounterId IS NULL OR @ClerkId IS NULL OR @Sku2 IS NULL
        THROW 50001, 'Demo data missing: the branch needs a counter, an active counter clerk and two products in stock.', 1;

    BEGIN TRANSACTION;

    -- 1. Header (a paid sale, so Status = 2).
    INSERT INTO CounterTransactions (Id, BranchCode, CounterId, ClerkId, OpenedAtUtc, Status, TaxTotal, CreatedBy)
    VALUES (@TxId, @Branch, @CounterId, @ClerkId, @Now, 2, 0, N'sql-demo-sale');

    -- 2. Lines (retail lines need Sku and Description: CK_TransactionLines_RetailFields).
    INSERT INTO TransactionLines (TransactionId, LineNumber, LineType, Description, Quantity, UnitPrice, TaxCategory, Sku, CreatedBy)
    SELECT @TxId, i.Pos, 'Retail', i.Name, @Quantity, i.UnitPrice, i.TaxCategory, i.Sku, N'sql-demo-sale'
    FROM @Items AS i;

    -- 3. Payment, equal to the lines total (cash).
    INSERT INTO Payments (TransactionId, Method, Amount, CreatedBy)
    SELECT @TxId, 1, SUM(@Quantity * i.UnitPrice), N'sql-demo-sale' FROM @Items AS i;

    -- 4. Stock decrement, one SKU at a time in SKU order. The guard (Quantity >= @Quantity) lives in the UPDATE itself,
    --    so a concurrent sale cannot drive the stock below zero between a check and the update.
    UPDATE BranchStock SET Quantity = Quantity - @Quantity, ModifiedAtUtc = SYSUTCDATETIME()
    WHERE BranchCode = @Branch AND Sku = @Sku1 AND Quantity >= @Quantity;
    IF @@ROWCOUNT = 0 THROW 50013, 'Insufficient stock for the first item. The whole sale is rolled back.', 1;

    UPDATE BranchStock SET Quantity = Quantity - @Quantity, ModifiedAtUtc = SYSUTCDATETIME()
    WHERE BranchCode = @Branch AND Sku = @Sku2 AND Quantity >= @Quantity;
    IF @@ROWCOUNT = 0 THROW 50013, 'Insufficient stock for the second item. The whole sale is rolled back.', 1;

    COMMIT TRANSACTION;
    SELECT 'COMMITTED' AS Outcome, @TxId AS TransactionId;
END TRY
BEGIN CATCH
    -- XACT_STATE: 1 = open and committable, -1 = doomed (only ROLLBACK is allowed), 0 = no transaction.
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    SELECT 'ROLLED BACK' AS Outcome, ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;
    THROW;   -- re-raise the original error to the caller
END CATCH;
GO

/* Check after each run. The failing run must leave the stock unchanged and add no transaction. */
SELECT 'AFTER' AS Step, BranchCode, Sku, Quantity
FROM BranchStock
WHERE BranchCode = 'BR001' AND Sku IN (SELECT TOP (2) Sku FROM BranchStock WHERE BranchCode = 'BR001' ORDER BY Sku)
ORDER BY Sku;

SELECT COUNT(*) AS DemoTransactions FROM CounterTransactions WHERE CreatedBy = N'sql-demo-sale';
GO

/* OPTIONAL clean-up: removes the demo sales (lines and payment first, then the header). The stock stays decremented;
   restore it with usp_AdjustStock if you want the original quantities.
DELETE FROM Payments          WHERE CreatedBy = N'sql-demo-sale';
DELETE FROM TransactionLines  WHERE CreatedBy = N'sql-demo-sale';
DELETE FROM CounterTransactions WHERE CreatedBy = N'sql-demo-sale';
*/