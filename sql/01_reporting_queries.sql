/* ApexPost reporting queries (SQL Server), Part B.
   Revenue = Quantity * UnitPrice of the lines, tax excluded.
   Services = shipping lines + money order FEES (the money order amount itself is not revenue, as in the application).
   Only Paid transactions (Status = 2) count unless a query says otherwise.
   Status: 1 Open, 2 Paid, 3 Voided, 4 Refunded.  ServiceLevel: 1 Standard, 2 Express, 3 Registered.
   Verification result: 1 Pending, 2 Passed, 3 Failed.
   Run each query on its own (select it, then F5). */
USE ApexPostDb;
GO

/* ---------------------------------------------------------------------------------------------
   1. Daily branch sales: retail vs services, last 14 days.   INNER JOIN + LEFT JOIN + GROUP BY + HAVING
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
GO

/* ---------------------------------------------------------------------------------------------
   2. Branches with NO sales today.   LEFT JOIN anti-join case.
      The date range and the Paid filter are in the ON clause, not in WHERE. A filter on t in WHERE would
      turn the LEFT JOIN into an INNER JOIN and the branches without sales would disappear.
      The rows we keep are the ones where the join found nothing (t.Id IS NULL). */
DECLARE @Today date = CAST(SYSUTCDATETIME() AS date);

SELECT b.BranchCode, b.Name, b.Region
FROM Branches AS b
LEFT JOIN CounterTransactions AS t
       ON t.BranchCode = b.BranchCode
      AND t.Status = 2
      AND t.OpenedAtUtc >= @Today
      AND t.OpenedAtUtc <  DATEADD(DAY, 1, @Today)
WHERE b.IsActive = 1
  AND t.Id IS NULL
ORDER BY b.Region, b.BranchCode;
GO

/* ---------------------------------------------------------------------------------------------
   3. Top 10 products by revenue per region.   INNER JOINs + GROUP BY + HAVING + ROW_NUMBER per region
      Retail lines only (they carry the SKU). Revenue is summed per region and product first,
      then ranked inside each region. */
WITH ProductRevenue AS
(
    SELECT br.Region,
           p.Sku,
           p.Name AS ProductName,
           SUM(l.Quantity)                AS UnitsSold,
           SUM(l.Quantity * l.UnitPrice)  AS Revenue
    FROM TransactionLines AS l
    JOIN CounterTransactions AS t  ON t.Id = l.TransactionId
    JOIN Branches            AS br ON br.BranchCode = t.BranchCode
    JOIN Products            AS p  ON p.Sku = l.Sku
    WHERE t.Status = 2
      AND l.LineType = 'Retail'
    GROUP BY br.Region, p.Sku, p.Name
    HAVING SUM(l.Quantity * l.UnitPrice) > 0
),
Ranked AS
(
    SELECT Region, Sku, ProductName, UnitsSold, Revenue,
           ROW_NUMBER() OVER (PARTITION BY Region ORDER BY Revenue DESC, Sku) AS RankInRegion
    FROM ProductRevenue
)
SELECT Region, RankInRegion, Sku, ProductName, UnitsSold, Revenue
FROM Ranked
WHERE RankInRegion <= 10
ORDER BY Region, RankInRegion;
GO

/* ---------------------------------------------------------------------------------------------
   4. Shipping volume by destination zone and service level.   INNER JOINs + GROUP BY + HAVING
      Volume = number of shipments, total weight in kg, and shipping revenue of the paid transactions. */
SELECT z.Code AS ZoneCode,
       z.Name AS ZoneName,
       CASE s.ServiceLevel WHEN 1 THEN 'Standard' WHEN 2 THEN 'Express' WHEN 3 THEN 'Registered' END AS ServiceLevel,
       COUNT(*)                          AS Shipments,
       CAST(SUM(s.WeightGrams) / 1000.0 AS decimal(18,3)) AS TotalKg,
       SUM(l.Quantity * l.UnitPrice)     AS ShippingRevenue
FROM Shipments AS s
JOIN TransactionLines    AS l ON l.TransactionId = s.TransactionId AND l.LineNumber = s.LineNumber
JOIN CounterTransactions AS t ON t.Id = s.TransactionId
JOIN ShippingZones       AS z ON z.Code = s.DestinationZone
WHERE t.Status = 2
GROUP BY z.Code, z.Name, s.ServiceLevel
HAVING COUNT(*) >= 1
ORDER BY z.Code, s.ServiceLevel;
GO

/* ---------------------------------------------------------------------------------------------
   5. Money orders and identity verifications per branch.   LEFT JOINs to aggregated derived tables
      Every branch is listed (LEFT JOIN), also a branch with none. The two counts are aggregated
      separately first, so the money order rows do not multiply the verification rows (and the reverse).
      All transaction statuses are counted: a verification or money order exists even if the sale was voided later. */
SELECT b.BranchCode,
       b.Name,
       COALESCE(m.MoneyOrders, 0)        AS MoneyOrders,
       COALESCE(m.TotalAmount, 0)        AS MoneyOrderAmount,
       COALESCE(m.TotalFees, 0)          AS MoneyOrderFees,
       COALESCE(v.Verifications, 0)      AS Verifications,
       COALESCE(v.Passed, 0)             AS Passed,
       COALESCE(v.Failed, 0)             AS Failed,
       COALESCE(v.Pending, 0)            AS Pending
FROM Branches AS b
LEFT JOIN
(
    SELECT t.BranchCode,
           COUNT(*)       AS MoneyOrders,
           SUM(mo.Amount) AS TotalAmount,
           SUM(mo.Fee)    AS TotalFees
    FROM MoneyOrders AS mo
    JOIN CounterTransactions AS t ON t.Id = mo.TransactionId
    GROUP BY t.BranchCode
) AS m ON m.BranchCode = b.BranchCode
LEFT JOIN
(
    SELECT t.BranchCode,
           COUNT(*) AS Verifications,
           SUM(CASE WHEN iv.Result = 2 THEN 1 ELSE 0 END) AS Passed,
           SUM(CASE WHEN iv.Result = 3 THEN 1 ELSE 0 END) AS Failed,
           SUM(CASE WHEN iv.Result = 1 THEN 1 ELSE 0 END) AS Pending
    FROM IdentityVerifications AS iv
    JOIN CounterTransactions AS t ON t.Id = iv.TransactionId
    GROUP BY t.BranchCode
) AS v ON v.BranchCode = b.BranchCode
ORDER BY b.BranchCode;
GO