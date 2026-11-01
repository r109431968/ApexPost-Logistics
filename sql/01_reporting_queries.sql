/* ApexPost reporting queries (SQL Server). Revenue = Quantity * UnitPrice of the lines, tax excluded.
   Services = shipping lines + money order FEES (the money order amount itself is not revenue, as in the application).
   Only Paid transactions (Status = 2) count. Status: 1 Open, 2 Paid, 3 Voided, 4 Refunded.
   Run each query on its own (select it, then F5). */
USE ApexPostDb;
GO

/* 1. Daily branch sales: retail vs services (shipping + money order fees), last 14 days.
      HAVING drops branch-days without revenue. */
DECLARE @To   date = CAST(SYSUTCDATETIME() AS date);
DECLARE @From date = DATEADD(DAY, -13, @To);

SELECT t.BranchCode,
       CAST(t.OpenedAtUtc AS date) AS SaleDate,
       SUM(CASE WHEN l.LineType = 'Retail' THEN l.Quantity * l.UnitPrice ELSE 0 END) AS RetailSales,
       SUM(CASE WHEN l.LineType = 'Shipping' THEN l.Quantity * l.UnitPrice
                WHEN l.LineType = 'MoneyOrder' THEN mo.Fee ELSE 0 END) AS ServiceSales,
       COUNT(DISTINCT t.Id) AS Transactions
FROM CounterTransactions AS t
JOIN TransactionLines AS l ON l.TransactionId = t.Id
LEFT JOIN MoneyOrders AS mo ON mo.TransactionId = l.TransactionId AND mo.LineNumber = l.LineNumber
WHERE t.Status = 2
  AND t.OpenedAtUtc >= @From
  AND t.OpenedAtUtc < DATEADD(DAY, 1, @To)
GROUP BY t.BranchCode, CAST(t.OpenedAtUtc AS date)
HAVING SUM(CASE WHEN l.LineType = 'Retail' THEN l.Quantity * l.UnitPrice ELSE 0 END) > 0
    OR SUM(CASE WHEN l.LineType = 'Shipping' THEN l.Quantity * l.UnitPrice
                WHEN l.LineType = 'MoneyOrder' THEN mo.Fee ELSE 0 END) > 0
ORDER BY SaleDate DESC, t.BranchCode;