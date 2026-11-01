CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
    @Day        date,
    @BranchCode varchar(5) = NULL      -- optional: NULL means all branches
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
    WHERE t.Status = 2                                   -- Paid
      AND t.OpenedAtUtc >= @Day
      AND t.OpenedAtUtc <  DATEADD(DAY, 1, @Day)
      AND (@BranchCode IS NULL OR t.BranchCode = @BranchCode)
    GROUP BY t.BranchCode
    ORDER BY t.BranchCode;
END;


EXEC dbo.usp_DailyBranchSales @Day = '2026-10-31';
EXEC dbo.usp_DailyBranchSales @Day = '2026-10-31', @BranchCode = 'BR001';