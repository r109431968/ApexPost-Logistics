/* 11. Least-privilege set-up (documentation script, Part D). Run it on a copy of the database if you want to try it.
   Idea: the web application must NOT connect as db_owner. It connects as a user that belongs to a role with only the rights it needs.
   No password is created or stored here: the user maps to a Windows login (or to a managed identity / secret in production). */
USE ApexPostDb;
GO

/* 1. The role the web application runs as. */
IF DATABASE_PRINCIPAL_ID('apexpost_app') IS NULL CREATE ROLE apexpost_app;
GO

/* 2. Stored procedures: the application and the reporting users may only EXECUTE them.
      Because the procedures and the tables have the same owner (dbo), ownership chaining lets the procedure read and write
      the tables even though the caller has no right on the tables. */
GRANT EXECUTE ON OBJECT::dbo.usp_DailyBranchSales TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_LowStockReport   TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_AdjustStock      TO apexpost_app;
GO

/* 3. Tables the application reads and writes directly through EF Core. */
GRANT SELECT, INSERT, UPDATE ON dbo.CounterTransactions    TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON dbo.TransactionLines       TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON dbo.Payments               TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON dbo.Shipments              TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON dbo.MoneyOrders            TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON dbo.IdentityVerifications  TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON dbo.BranchStock            TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON dbo.Staff                  TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON dbo.Branches               TO apexpost_app;
GRANT SELECT, INSERT, UPDATE ON dbo.Counters               TO apexpost_app;
GRANT SELECT, UPDATE         ON dbo.Products               TO apexpost_app;   -- price revision uses UPDATE
GRANT SELECT                 ON dbo.ProductCategories      TO apexpost_app;
GRANT SELECT                 ON dbo.ShippingZones          TO apexpost_app;
GRANT SELECT                 ON dbo.ShippingRates          TO apexpost_app;
GRANT SELECT                 ON dbo.Roles                  TO apexpost_app;
GRANT SELECT                 ON dbo.StockAdjustments       TO apexpost_app;   -- written only by usp_AdjustStock
GO

/* 4. History tables are append-only for the application: INSERT and SELECT, never UPDATE or DELETE. */
GRANT SELECT, INSERT ON dbo.AuditEntries TO apexpost_app;
GO

/* 5. Explicit DENYs make the intention visible and win over any other grant. */
DENY DELETE ON dbo.AuditEntries      TO apexpost_app;
DENY UPDATE ON dbo.AuditEntries      TO apexpost_app;
DENY DELETE ON dbo.StockAdjustments  TO apexpost_app;
DENY UPDATE ON dbo.StockAdjustments  TO apexpost_app;
DENY ALTER  ON SCHEMA::dbo           TO apexpost_app;   -- no schema changes: migrations use a different login
GO

/* 6. A reporting-only role: can run the two report procedures and nothing else (no table rights at all). */
IF DATABASE_PRINCIPAL_ID('apexpost_reporting') IS NULL CREATE ROLE apexpost_reporting;
GRANT EXECUTE ON OBJECT::dbo.usp_DailyBranchSales TO apexpost_reporting;
GRANT EXECUTE ON OBJECT::dbo.usp_LowStockReport   TO apexpost_reporting;
GO

/* 7. Mapping a user to the role (replace the name with a real Windows account; do not put passwords in scripts):
      CREATE USER [DOMAIN\ApexPostService] FOR LOGIN [DOMAIN\ApexPostService];
      ALTER ROLE apexpost_app ADD MEMBER [DOMAIN\ApexPostService];

   How to check what a role can do:
      SELECT dp.name AS Principal, p.permission_name, p.state_desc, OBJECT_NAME(p.major_id) AS ObjectName
      FROM sys.database_permissions AS p JOIN sys.database_principals AS dp ON dp.principal_id = p.grantee_principal_id
      WHERE dp.name IN ('apexpost_app', 'apexpost_reporting') ORDER BY dp.name, ObjectName;

   Note: the sale of a product also updates stock. Direct EF Core updates on BranchStock are allowed above; if you want ONLY
   usp_AdjustStock to change stock, remove UPDATE on BranchStock and move the sale's stock change into a procedure as well. */
