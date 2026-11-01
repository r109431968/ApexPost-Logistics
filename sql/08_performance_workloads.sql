/* 08. Performance workloads (Part C). Run AFTER script 09 (extra data) and BEFORE the index migration for the "before" measurement,
   then run the same statements again AFTER the index migration for the "after" measurement.

   HOW TO MEASURE (for every workload below):
     1. In SSMS press Ctrl+M (Include Actual Execution Plan).
     2. Run the three SET lines in the next block once per query window.
     3. Select ONE workload and press F5. Look at the Execution plan tab (operators, estimated vs actual rows)
        and at the Messages tab (logical reads, CPU time, elapsed time).
     4. Copy the numbers you see into PERFORMANCE.md. Do not type numbers you did not measure.
   Between runs of the same workload use  DBCC FREEPROCCACHE  only on LocalDB (never on a shared server). */
USE ApexPostDb;
GO
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
GO

/* ===== WORKLOAD 1: the newest 20 transactions of a branch (the Transactions list page).
   Before: IX_CounterTransactions_BranchCode finds the branch rows, then SQL Server must SORT them by OpenedAtUtc (and do key lookups).
   Expected change after the index: seek + no Sort operator, because the index is already in the right order. */
SELECT TOP (20) t.Id, t.OpenedAtUtc, t.Status, t.TaxTotal
FROM CounterTransactions AS t
WHERE t.BranchCode = 'BR001'
ORDER BY t.OpenedAtUtc DESC;
GO

/* ===== WORKLOAD 2: sales of one branch on one day, SARGable form (a range, not a function on the column). */
DECLARE @Branch varchar(5) = 'BR001';
DECLARE @Day date = CAST(DATEADD(DAY, -10, SYSUTCDATETIME()) AS date);

SELECT SUM(l.Quantity * l.UnitPrice) AS NetSales, COUNT(DISTINCT t.Id) AS Transactions
FROM CounterTransactions AS t
JOIN TransactionLines AS l ON l.TransactionId = t.Id
WHERE t.BranchCode = @Branch
  AND t.Status = 2
  AND t.OpenedAtUtc >= @Day
  AND t.OpenedAtUtc <  DATEADD(DAY, 1, @Day);
GO

/* ===== WORKLOAD 3: top retail products (units sold) across all branches.
   Before: a scan of TransactionLines. After: the index on (LineType, Sku) INCLUDE (Quantity, UnitPrice) answers it without touching the table. */
SELECT TOP (10) l.Sku, SUM(l.Quantity) AS UnitsSold, SUM(l.Quantity * l.UnitPrice) AS Revenue
FROM TransactionLines AS l
WHERE l.LineType = 'Retail'
GROUP BY l.Sku
ORDER BY Revenue DESC;
GO

/* ===== WORKLOAD 4: the latest 50 audit entries (the Audit page). Before: scan + sort of the whole table. After: read 50 rows from the index. */
SELECT TOP (50) a.TimestampUtc, a.Controller, a.Action, a.Operation, a.StatusCode, a.CorrelationId
FROM AuditEntries AS a
ORDER BY a.TimestampUtc DESC;
GO

/* ===== NON-SARGable predicate and its fix (Part C requirement).
   A function around the column (CONVERT / CAST / YEAR ...) stops SQL Server from using an index SEEK on that column:
   it has to calculate the function for every row. Compare the plans and the logical reads of A and B. */
DECLARE @Branch varchar(5) = 'BR001';
DECLARE @Day date = CAST(DATEADD(DAY, -10, SYSUTCDATETIME()) AS date);

-- A. NON-SARGable: function on the column.
SELECT COUNT(*) AS Transactions
FROM CounterTransactions AS t
WHERE t.BranchCode = @Branch
  AND CONVERT(date, t.OpenedAtUtc) = @Day;

-- B. SARGable: the same answer with a range on the bare column. This is also how the application and usp_DailyBranchSales are written.
SELECT COUNT(*) AS Transactions
FROM CounterTransactions AS t
WHERE t.BranchCode = @Branch
  AND t.OpenedAtUtc >= @Day
  AND t.OpenedAtUtc <  DATEADD(DAY, 1, @Day);
GO

/* ===== ESTIMATED vs ACTUAL ROWS: parameter sniffing on skewed data (Part C requirement).
   About 70 % of the rows belong to BR001, only a few percent to every other branch.
   Step 1 (clear the plan, then compile the plan with a SMALL branch):  the optimiser sees a few rows and chooses seek + key lookup.
   Step 2 (reuse the same plan with the BIG branch):  the plan expects a few rows, but BR001 returns very many.
   In the actual plan of step 2 hover over the Index Seek / Key Lookup: "Estimated Number of Rows" is far below "Actual Number of Rows".
   The extra key lookups show up as high logical reads in the Messages tab. */
DBCC FREEPROCCACHE;   -- LocalDB only
GO
EXEC sys.sp_executesql
     N'SELECT t.Id, t.OpenedAtUtc, t.TaxTotal FROM CounterTransactions AS t WHERE t.BranchCode = @Branch',
     N'@Branch varchar(5)', @Branch = 'BR005';        -- step 1: pick the branch with the FEWEST rows (check the last result of script 09)
GO
EXEC sys.sp_executesql
     N'SELECT t.Id, t.OpenedAtUtc, t.TaxTotal FROM CounterTransactions AS t WHERE t.BranchCode = @Branch',
     N'@Branch varchar(5)', @Branch = 'BR001';        -- step 2: same text, so the cached plan is reused for the BIG branch
GO

/* FIX options (run and compare the plan and the reads of step 2 again):
   a) OPTION (RECOMPILE)  - a fresh plan for every value. Costs a compile each time, so use it for rare, expensive reports
      (usp_LowStockReport already does this). */
EXEC sys.sp_executesql
     N'SELECT t.Id, t.OpenedAtUtc, t.TaxTotal FROM CounterTransactions AS t WHERE t.BranchCode = @Branch OPTION (RECOMPILE)',
     N'@Branch varchar(5)', @Branch = 'BR001';
GO
/* b) OPTIMIZE FOR UNKNOWN - the plan uses the average density instead of the first value. */
EXEC sys.sp_executesql
     N'SELECT t.Id, t.OpenedAtUtc, t.TaxTotal FROM CounterTransactions AS t WHERE t.BranchCode = @Branch OPTION (OPTIMIZE FOR UNKNOWN)',
     N'@Branch varchar(5)', @Branch = 'BR001';
GO

/* ===== STATISTICS (to see what the optimiser knew): run once and copy the histogram for BR001 / BR005 into PERFORMANCE.md. */
DBCC SHOW_STATISTICS ('dbo.CounterTransactions', 'IX_CounterTransactions_BranchCode') WITH HISTOGRAM;
GO

/* ===== HOW MUCH SPACE THE EXTRA INDEXES COST (run after the index migration): */
SELECT OBJECT_NAME(i.object_id) AS TableName, i.name AS IndexName, SUM(ps.used_page_count) * 8 AS UsedKB
FROM sys.indexes AS i
JOIN sys.dm_db_partition_stats AS ps ON ps.object_id = i.object_id AND ps.index_id = i.index_id
WHERE OBJECT_NAME(i.object_id) IN ('CounterTransactions', 'TransactionLines', 'AuditEntries')
GROUP BY i.object_id, i.name
ORDER BY TableName, IndexName;
GO
