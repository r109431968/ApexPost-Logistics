/* 10. Removes everything that script 09 added (and nothing else). Safe to run more than once.
   TransactionLines are removed automatically: their foreign key to CounterTransactions is ON DELETE CASCADE. */
USE ApexPostDb;
GO
SET NOCOUNT ON;

DELETE FROM CounterTransactions WHERE CreatedBy = N'perf-load';
DELETE FROM AuditEntries        WHERE CreatedBy = N'perf-load';

SELECT 'CounterTransactions' AS TableName, COUNT(*) AS RowsLeft FROM CounterTransactions
UNION ALL SELECT 'TransactionLines', COUNT(*) FROM TransactionLines
UNION ALL SELECT 'AuditEntries', COUNT(*) FROM AuditEntries;
GO
