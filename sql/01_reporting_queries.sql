/* ApexPost reporting queries (SQL Server). Revenue = Quantity * UnitPrice of the lines, tax excluded.
   Only Paid transactions (Status = 2) count. Status: 1 Open, 2 Paid, 3 Voided, 4 Refunded.
   Run each query on its own (select it, then F5). */
USE ApexPostDb;
GO

/* 1. Daily branch sales: retail vs services (shipping + money orders), last 14 days. HAVING drops branch-days without revenue. */DECLARE @To   date = CAST(SYSUTCDATETIME() AS date);
DECLARE @From date = DATEADD(DAY, -13, @To);

SELECT t.BranchCode,
       CAST(t.OpenedAtUtc AS date) AS SaleDate,
       SUM(CASE WHEN l.LineType = 'Retail' THEN l.Quantity * l.UnitPrice ELSE 0 END) AS RetailSales,
       SUM(CASE WHEN l.LineType IN ('Shipping', 'MoneyOrder') THEN l.Quantity * l.UnitPrice ELSE 0 END) AS ServiceSales,
       SUM(l.Quantity * l.UnitPrice) AS NetSales,
       COUNT(DISTINCT t.Id) AS Transactions
FROM CounterTransactions AS t
JOIN TransactionLines AS l ON l.TransactionId = t.Id
WHERE t.Status = 2
  AND t.OpenedAtUtc >= @From
  AND t.OpenedAtUtc < DATEADD(DAY, 1, @To)
GROUP BY t.BranchCode, CAST(t.OpenedAtUtc AS date)
HAVING SUM(l.Quantity * l.UnitPrice) > 0
ORDER BY SaleDate DESC, t.BranchCode;


GO
/* 2. Active branches with NO paid sale today. LEFT JOIN: the date condition is in ON, not in WHERE. */
DECLARE @Today date = CAST(SYSUTCDATETIME() AS date);

SELECT b.BranchCode, b.Name, b.Region
FROM Branches AS b
LEFT JOIN CounterTransactions AS t
       ON t.BranchCode = b.BranchCode
      AND t.Status = 2
      AND t.OpenedAtUtc >= @Today
      AND t.OpenedAtUtc < DATEADD(DAY, 1, @Today)
WHERE b.IsActive = 1
  AND t.Id IS NULL
ORDER BY b.Region, b.BranchCode;

GO
/* 3. Top 10 products by retail revenue in each region (window function). */
WITH ProductRevenue AS
(
    SELECT b.Region, l.Sku, SUM(l.Quantity) AS UnitsSold, SUM(l.Quantity * l.UnitPrice) AS Revenue
    FROM TransactionLines AS l
    JOIN CounterTransactions AS t ON t.Id = l.TransactionId
    JOIN Branches AS b ON b.BranchCode = t.BranchCode
    WHERE l.LineType = 'Retail' AND t.Status = 2
    GROUP BY b.Region, l.Sku
),
Ranked AS
(
    SELECT pr.*, ROW_NUMBER() OVER (PARTITION BY pr.Region ORDER BY pr.Revenue DESC, pr.Sku) AS Rnk
    FROM ProductRevenue AS pr
)
SELECT r.Region, r.Rnk, r.Sku, p.Name AS Product, c.Name AS Category, r.UnitsSold, r.Revenue
FROM Ranked AS r
JOIN Products AS p ON p.Sku = r.Sku
JOIN ProductCategories AS c ON c.Id = p.CategoryId
WHERE r.Rnk <= 10
ORDER BY r.Region, r.Rnk;


GO
/* 4. Shipping volume by destination zone and service level (ServiceLevel is the enum number). HAVING keeps groups with at least 2 shipments. */
SELECT s.DestinationZone, z.Name AS ZoneName, s.ServiceLevel,
       COUNT(*) AS Shipments,
       CAST(SUM(s.WeightGrams) / 1000.0 AS decimal(18,3)) AS TotalKg,
       SUM(l.Quantity * l.UnitPrice) AS Revenue
FROM Shipments AS s
JOIN TransactionLines AS l ON l.TransactionId = s.TransactionId AND l.LineNumber = s.LineNumber
JOIN CounterTransactions AS t ON t.Id = s.TransactionId
JOIN ShippingZones AS z ON z.Code = s.DestinationZone
WHERE t.Status = 2
GROUP BY s.DestinationZone, z.Name, s.ServiceLevel
HAVING COUNT(*) >= 2
ORDER BY s.DestinationZone, s.ServiceLevel;

GO
/* 5. Money orders and identity verifications per branch.
      Two separate aggregates joined to Branches, so nothing is counted twice. Identity Result: 2 Passed, 3 Failed. */
WITH Orders AS
(
    SELECT t.BranchCode, COUNT(*) AS MoneyOrders, SUM(m.Amount) AS OrderTotal, SUM(m.Fee) AS FeeTotal
    FROM MoneyOrders AS m
    JOIN CounterTransactions AS t ON t.Id = m.TransactionId
    WHERE t.Status = 2
    GROUP BY t.BranchCode
),
Verifications AS
(
    SELECT t.BranchCode,
           COUNT(*) AS Verifications,
           SUM(CASE WHEN v.Result = 2 THEN 1 ELSE 0 END) AS Passed,
           SUM(CASE WHEN v.Result = 3 THEN 1 ELSE 0 END) AS Failed
    FROM IdentityVerifications AS v
    JOIN CounterTransactions AS t ON t.Id = v.TransactionId
    GROUP BY t.BranchCode
)
SELECT b.BranchCode, b.Name,
       COALESCE(o.MoneyOrders, 0) AS MoneyOrders,
       COALESCE(o.OrderTotal, 0) AS OrderTotal,
       COALESCE(o.FeeTotal, 0) AS FeeTotal,
       COALESCE(v.Verifications, 0) AS Verifications,
       COALESCE(v.Passed, 0) AS Passed,
       COALESCE(v.Failed, 0) AS Failed
FROM Branches AS b
LEFT JOIN Orders AS o ON o.BranchCode = b.BranchCode
LEFT JOIN Verifications AS v ON v.BranchCode = b.BranchCode
ORDER BY b.BranchCode;