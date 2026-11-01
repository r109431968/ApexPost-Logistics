/* 09. Extra data for the performance work (Part C). Run ONCE, on ApexPostDb only (never on a shared or real database).
   The seeded 240 transactions are too few for an index to matter, so this script adds about 200 000 more.
   Every row it adds is tagged CreatedBy = 'perf-load', so script 10 can remove exactly these rows and nothing else.
   The data is deliberately SKEWED: about 70 % of the transactions belong to BR001 and the rest are spread over the other branches.
   The skew is what makes the "estimated vs actual rows" investigation in script 08 possible.
   Takes about 1 to 3 minutes on LocalDB. It uses only set-based INSERT ... SELECT statements (no loops). */
USE ApexPostDb;
GO
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM CounterTransactions WHERE CreatedBy = N'perf-load')
BEGIN
    PRINT 'perf-load rows already exist. Run script 10 first if you want to load them again.';
    RETURN;
END;

DECLARE @Rows int = 200000;

-- One counter and one clerk per branch (the foreign keys need real values).
SELECT ROW_NUMBER() OVER (ORDER BY b.BranchCode) AS Rn,
       b.BranchCode,
       (SELECT TOP (1) c.Id FROM Counters AS c WHERE c.BranchCode = b.BranchCode ORDER BY c.Id) AS CounterId,
       (SELECT TOP (1) s.Id FROM Staff AS s WHERE s.HomeBranchCode = b.BranchCode ORDER BY s.RoleId, s.Id) AS ClerkId
INTO #BranchPick
FROM Branches AS b
WHERE EXISTS (SELECT 1 FROM Counters AS c WHERE c.BranchCode = b.BranchCode)
  AND EXISTS (SELECT 1 FROM Staff AS s WHERE s.HomeBranchCode = b.BranchCode);

SELECT ROW_NUMBER() OVER (ORDER BY p.Sku) AS Rn, p.Sku, p.Name, p.UnitPrice, p.TaxCategory
INTO #ProductPick
FROM Products AS p;

DECLARE @BranchCount int = (SELECT COUNT(*) FROM #BranchPick);
DECLARE @ProductCount int = (SELECT COUNT(*) FROM #ProductPick);

-- A list of numbers 1..@Rows, and a new Id for every future transaction.
SELECT TOP (@Rows)
       ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N,
       NEWID() AS TransactionId
INTO #Numbers
FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b;

BEGIN TRANSACTION;

INSERT INTO CounterTransactions (Id, BranchCode, CounterId, ClerkId, OpenedAtUtc, Status, TaxTotal, CreatedBy)
SELECT n.TransactionId,
       bp.BranchCode,
       bp.CounterId,
       bp.ClerkId,
       DATEADD(MINUTE, -(n.N % (60 * 24 * 180)), SYSUTCDATETIME()),          -- spread over the last 180 days
       CASE WHEN n.N % 20 = 0 THEN 3 WHEN n.N % 25 = 0 THEN 4 ELSE 2 END,     -- mostly Paid (2), a few Voided and Refunded
       CAST(1 + (n.N % 7) AS decimal(18,2)),
       N'perf-load'
FROM #Numbers AS n
JOIN #BranchPick AS bp
  ON bp.Rn = CASE WHEN n.N % 10 < 7 THEN 1                                   -- 70 % go to the first branch (BR001)
                  ELSE 1 + (n.N % @BranchCount) END;

INSERT INTO TransactionLines (TransactionId, LineNumber, LineType, Description, Quantity, UnitPrice, TaxCategory, Sku, CreatedBy)
SELECT n.TransactionId, 1, 'Retail', pp.Name, 1 + (n.N % 3), pp.UnitPrice, pp.TaxCategory, pp.Sku, N'perf-load'
FROM #Numbers AS n
JOIN #ProductPick AS pp ON pp.Rn = 1 + (n.N % @ProductCount);

INSERT INTO TransactionLines (TransactionId, LineNumber, LineType, Description, Quantity, UnitPrice, TaxCategory, Sku, CreatedBy)
SELECT n.TransactionId, 2, 'Retail', pp.Name, 1, pp.UnitPrice, pp.TaxCategory, pp.Sku, N'perf-load'
FROM #Numbers AS n
JOIN #ProductPick AS pp ON pp.Rn = 1 + ((n.N * 7) % @ProductCount)
WHERE n.N % 2 = 0;                                                            -- half of the transactions have a second line

-- Audit rows for the "latest audit entries" workload.
INSERT INTO AuditEntries (Id, TimestampUtc, Role, BranchCode, Controller, Action, Operation, RouteValuesJson, DetailsJson, StatusCode, DurationMs, CorrelationId, CreatedBy)
SELECT NEWID(),
       DATEADD(SECOND, -(n.N * 7), SYSUTCDATETIME()),
       'CounterClerk', 'BR001', N'Transactions', N'Pay', N'TransactionPaid', N'{}', N'{}', 302, n.N % 90, 'perf-' + CAST(n.N AS varchar(10)), N'perf-load'
FROM (SELECT TOP (100000) N FROM #Numbers ORDER BY N) AS n;

COMMIT TRANSACTION;

DROP TABLE #Numbers, #BranchPick, #ProductPick;

SELECT 'CounterTransactions' AS TableName, COUNT(*) AS PerfLoadRows FROM CounterTransactions WHERE CreatedBy = N'perf-load'
UNION ALL SELECT 'TransactionLines', COUNT(*) FROM TransactionLines WHERE CreatedBy = N'perf-load'
UNION ALL SELECT 'AuditEntries', COUNT(*) FROM AuditEntries WHERE CreatedBy = N'perf-load';

-- The skew that script 08 relies on:
SELECT BranchCode, COUNT(*) AS Transactions FROM CounterTransactions GROUP BY BranchCode ORDER BY Transactions DESC;
GO
