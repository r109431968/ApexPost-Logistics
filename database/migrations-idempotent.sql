IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] uniqueidentifier NOT NULL,
        [TimestampUtc] datetime2 NOT NULL,
        [StaffId] uniqueidentifier NULL,
        [Role] varchar(30) NULL,
        [BranchCode] varchar(5) NULL,
        [Controller] nvarchar(60) NOT NULL,
        [Action] nvarchar(60) NOT NULL,
        [Operation] nvarchar(100) NULL,
        [RouteValuesJson] nvarchar(max) NOT NULL,
        [DetailsJson] nvarchar(max) NOT NULL,
        [StatusCode] int NOT NULL,
        [DurationMs] bigint NOT NULL,
        [CorrelationId] varchar(64) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [Branches] (
        [BranchCode] varchar(5) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Region] nvarchar(60) NOT NULL,
        [OpensAt] time NOT NULL,
        [ClosesAt] time NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([BranchCode]),
        CONSTRAINT [CK_Branches_Hours] CHECK ([ClosesAt] > [OpensAt])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductCategories] (
        [Id] int NOT NULL,
        [Name] nvarchar(40) NOT NULL,
        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] int NOT NULL,
        [Name] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingZones] (
        [Code] varchar(10) NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ShippingZones] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [Counters] (
        [Id] uniqueidentifier NOT NULL,
        [BranchCode] varchar(5) NOT NULL,
        [Label] nvarchar(50) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Counters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Counters_Branches_BranchCode] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Sku] varchar(8) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [CategoryId] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxCategory] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Sku]),
        CONSTRAINT [CK_Products_Price] CHECK ([UnitPrice] >= 0),
        CONSTRAINT [CK_Products_TaxCategory] CHECK ([TaxCategory] IN (1, 2, 3)),
        CONSTRAINT [FK_Products_ProductCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [ProductCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [Staff] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Email] varchar(254) NOT NULL,
        [RoleId] int NOT NULL,
        [HomeBranchCode] varchar(5) NOT NULL,
        [IsActive] bit NOT NULL,
        [PasswordHash] varchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Staff] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Staff_Branches_HomeBranchCode] FOREIGN KEY ([HomeBranchCode]) REFERENCES [Branches] ([BranchCode]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Staff_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingRates] (
        [ZoneCode] varchar(10) NOT NULL,
        [BaseRate] decimal(18,2) NOT NULL,
        [PerKgRate] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ShippingRates] PRIMARY KEY ([ZoneCode]),
        CONSTRAINT [CK_ShippingRates_Base] CHECK ([BaseRate] >= 0),
        CONSTRAINT [CK_ShippingRates_PerKg] CHECK ([PerKgRate] >= 0),
        CONSTRAINT [FK_ShippingRates_ShippingZones_ZoneCode] FOREIGN KEY ([ZoneCode]) REFERENCES [ShippingZones] ([Code]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [BranchStock] (
        [BranchCode] varchar(5) NOT NULL,
        [Sku] varchar(8) NOT NULL,
        [Quantity] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_BranchStock] PRIMARY KEY ([BranchCode], [Sku]),
        CONSTRAINT [CK_BranchStock_Quantity] CHECK ([Quantity] >= 0),
        CONSTRAINT [FK_BranchStock_Branches_BranchCode] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BranchStock_Products_Sku] FOREIGN KEY ([Sku]) REFERENCES [Products] ([Sku]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [StockAdjustments] (
        [Id] bigint NOT NULL IDENTITY,
        [BranchCode] varchar(5) NOT NULL,
        [Sku] varchar(8) NOT NULL,
        [Delta] int NOT NULL,
        [QuantityAfter] int NOT NULL,
        [Reason] nvarchar(200) NOT NULL,
        [ChangedBy] nvarchar(100) NOT NULL,
        [ChangedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_StockAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockAdjustments_After] CHECK ([QuantityAfter] >= 0),
        CONSTRAINT [CK_StockAdjustments_Delta] CHECK ([Delta] <> 0),
        CONSTRAINT [FK_StockAdjustments_Branches_BranchCode] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockAdjustments_Products_Sku] FOREIGN KEY ([Sku]) REFERENCES [Products] ([Sku]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [CounterTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [BranchCode] varchar(5) NOT NULL,
        [CounterId] uniqueidentifier NOT NULL,
        [ClerkId] uniqueidentifier NOT NULL,
        [OpenedAtUtc] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [TaxTotal] decimal(18,2) NOT NULL,
        [ReversalReason] nvarchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_CounterTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CounterTransactions_Status] CHECK ([Status] IN (1, 2, 3, 4)),
        CONSTRAINT [CK_CounterTransactions_Tax] CHECK ([TaxTotal] >= 0),
        CONSTRAINT [FK_CounterTransactions_Branches_BranchCode] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CounterTransactions_Counters_CounterId] FOREIGN KEY ([CounterId]) REFERENCES [Counters] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CounterTransactions_Staff_ClerkId] FOREIGN KEY ([ClerkId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [IdentityVerifications] (
        [TransactionId] uniqueidentifier NOT NULL,
        [DocumentType] int NOT NULL,
        [MaskedNumber] varchar(8) NOT NULL,
        [Result] int NOT NULL,
        [VerifiedAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_IdentityVerifications] PRIMARY KEY ([TransactionId]),
        CONSTRAINT [CK_IdentityVerifications_Document] CHECK ([DocumentType] IN (1, 2, 3)),
        CONSTRAINT [CK_IdentityVerifications_Masked] CHECK ([MaskedNumber] LIKE '****____' AND LEN([MaskedNumber]) = 8),
        CONSTRAINT [CK_IdentityVerifications_Result] CHECK ([Result] IN (1, 2, 3)),
        CONSTRAINT [FK_IdentityVerifications_CounterTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [TransactionId] uniqueidentifier NOT NULL,
        [Method] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([TransactionId]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] >= 0),
        CONSTRAINT [CK_Payments_Method] CHECK ([Method] IN (1, 2, 3)),
        CONSTRAINT [FK_Payments_CounterTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [TransactionLines] (
        [TransactionId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [LineType] varchar(10) NOT NULL,
        [Description] nvarchar(200) NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxCategory] int NOT NULL,
        [Sku] varchar(8) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_TransactionLines] PRIMARY KEY ([TransactionId], [LineNumber]),
        CONSTRAINT [CK_TransactionLines_Number] CHECK ([LineNumber] > 0),
        CONSTRAINT [CK_TransactionLines_Price] CHECK ([UnitPrice] >= 0),
        CONSTRAINT [CK_TransactionLines_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_TransactionLines_RetailFields] CHECK ([LineType] <> 'Retail' OR ([Sku] IS NOT NULL AND [Description] IS NOT NULL)),
        CONSTRAINT [CK_TransactionLines_Tax] CHECK ([TaxCategory] IN (1, 2, 3)),
        CONSTRAINT [CK_TransactionLines_Type] CHECK ([LineType] IN ('Retail', 'Shipping', 'MoneyOrder')),
        CONSTRAINT [FK_TransactionLines_CounterTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TransactionLines_Products_Sku] FOREIGN KEY ([Sku]) REFERENCES [Products] ([Sku]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [MoneyOrders] (
        [TransactionId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Fee] decimal(18,2) NOT NULL,
        [Sender] nvarchar(80) NOT NULL,
        [Recipient] nvarchar(80) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_MoneyOrders] PRIMARY KEY ([TransactionId], [LineNumber]),
        CONSTRAINT [CK_MoneyOrders_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_MoneyOrders_Fee] CHECK ([Fee] >= 0),
        CONSTRAINT [FK_MoneyOrders_TransactionLines_TransactionId_LineNumber] FOREIGN KEY ([TransactionId], [LineNumber]) REFERENCES [TransactionLines] ([TransactionId], [LineNumber]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE TABLE [Shipments] (
        [TransactionId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [ShipmentType] int NOT NULL,
        [WeightGrams] decimal(18,3) NOT NULL,
        [DestinationZone] varchar(10) NOT NULL,
        [ServiceLevel] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Shipments] PRIMARY KEY ([TransactionId], [LineNumber]),
        CONSTRAINT [CK_Shipments_Service] CHECK ([ServiceLevel] IN (1, 2, 3)),
        CONSTRAINT [CK_Shipments_Type] CHECK ([ShipmentType] IN (1, 2)),
        CONSTRAINT [CK_Shipments_Weight] CHECK ([WeightGrams] > 0),
        CONSTRAINT [FK_Shipments_ShippingZones_DestinationZone] FOREIGN KEY ([DestinationZone]) REFERENCES [ShippingZones] ([Code]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Shipments_TransactionLines_TransactionId_LineNumber] FOREIGN KEY ([TransactionId], [LineNumber]) REFERENCES [TransactionLines] ([TransactionId], [LineNumber]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[ProductCategories]'))
        SET IDENTITY_INSERT [ProductCategories] ON;
    EXEC(N'INSERT INTO [ProductCategories] ([Id], [Name])
    VALUES (1, N''Packaging''),
    (2, N''Stationery''),
    (3, N''OfficeSupplies'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[ProductCategories]'))
        SET IDENTITY_INSERT [ProductCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Id], [Name])
    VALUES (1, N''CounterClerk''),
    (2, N''BranchManager''),
    (3, N''Auditor''),
    (4, N''Administrator'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BranchStock_Sku] ON [BranchStock] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Counters_BranchCode_Label] ON [Counters] ([BranchCode], [Label]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_BranchCode] ON [CounterTransactions] ([BranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_ClerkId] ON [CounterTransactions] ([ClerkId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_CounterId] ON [CounterTransactions] ([CounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductCategories_Name] ON [ProductCategories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_DestinationZone] ON [Shipments] ([DestinationZone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Staff_Email] ON [Staff] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Staff_HomeBranchCode] ON [Staff] ([HomeBranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Staff_RoleId] ON [Staff] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_BranchCode] ON [StockAdjustments] ([BranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_Sku] ON [StockAdjustments] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TransactionLines_Sku] ON [TransactionLines] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030122704_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030122704_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101110147_AddStoredProcedures'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
        @Day        date,
        @BranchCode varchar(5) = NULL
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
        WHERE t.Status = 2
          AND t.OpenedAtUtc >= @Day
          AND t.OpenedAtUtc <  DATEADD(DAY, 1, @Day)
          AND (@BranchCode IS NULL OR t.BranchCode = @BranchCode)
        GROUP BY t.BranchCode
        ORDER BY t.BranchCode;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101110147_AddStoredProcedures'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
        @BranchCode varchar(5) = NULL,
        @CategoryId int        = NULL,
        @Threshold  int        = 10
    AS
    BEGIN
        SET NOCOUNT ON;

        SELECT s.BranchCode, s.Sku, p.Name AS Product, c.Name AS Category, s.Quantity
        FROM BranchStock AS s
        JOIN Products AS p ON p.Sku = s.Sku
        JOIN ProductCategories AS c ON c.Id = p.CategoryId
        WHERE s.Quantity <= @Threshold
          AND (@BranchCode IS NULL OR s.BranchCode = @BranchCode)
          AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
        ORDER BY s.Quantity, s.BranchCode, s.Sku
        OPTION (RECOMPILE);
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101110147_AddStoredProcedures'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
        @BranchCode    varchar(5),
        @Sku           varchar(8),
        @Delta         int,
        @Reason        nvarchar(200),
        @ChangedBy     nvarchar(100),
        @QuantityAfter int OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF @Delta = 0
            THROW 50010, 'The adjustment must not be zero.', 1;
        IF LEN(LTRIM(RTRIM(COALESCE(@Reason, N'')))) = 0
            THROW 50011, 'A reason is required.', 1;

        BEGIN TRY
            BEGIN TRANSACTION;

            UPDATE dbo.BranchStock
            SET Quantity = Quantity + @Delta,
                ModifiedAtUtc = SYSUTCDATETIME()
            WHERE BranchCode = @BranchCode AND Sku = @Sku AND Quantity + @Delta >= 0;

            IF @@ROWCOUNT = 0
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchCode = @BranchCode AND Sku = @Sku)
                    THROW 50012, 'This branch does not stock that item.', 1;
                THROW 50013, 'The adjustment would take the stock below zero.', 1;
            END;

            SELECT @QuantityAfter = Quantity
            FROM dbo.BranchStock
            WHERE BranchCode = @BranchCode AND Sku = @Sku;

            INSERT INTO dbo.StockAdjustments (BranchCode, Sku, Delta, QuantityAfter, Reason, ChangedBy, CreatedBy)
            VALUES (@BranchCode, @Sku, @Delta, @QuantityAfter, @Reason, @ChangedBy, @ChangedBy);

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0
                ROLLBACK TRANSACTION;
            THROW;
        END CATCH;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101110147_AddStoredProcedures'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101110147_AddStoredProcedures', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101111200_RefineDailySalesServices'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
        @Day        date,
        @BranchCode varchar(5) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;

        SELECT t.BranchCode,
               @Day AS SaleDate,
               COALESCE(SUM(CASE WHEN l.LineType = 'Retail' THEN l.Quantity * l.UnitPrice END), 0) AS RetailSales,
               COALESCE(SUM(CASE WHEN l.LineType = 'Shipping' THEN l.Quantity * l.UnitPrice
                                 WHEN l.LineType = 'MoneyOrder' THEN mo.Fee END), 0) AS ServiceSales,
               COUNT(DISTINCT t.Id) AS Transactions
        FROM CounterTransactions AS t
        JOIN TransactionLines AS l ON l.TransactionId = t.Id
        LEFT JOIN MoneyOrders AS mo ON mo.TransactionId = l.TransactionId AND mo.LineNumber = l.LineNumber
        WHERE t.Status = 2
          AND t.OpenedAtUtc >= @Day
          AND t.OpenedAtUtc <  DATEADD(DAY, 1, @Day)
          AND (@BranchCode IS NULL OR t.BranchCode = @BranchCode)
        GROUP BY t.BranchCode
        ORDER BY t.BranchCode;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101111200_RefineDailySalesServices'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101111200_RefineDailySalesServices', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101135451_AddReportingIndexes'
)
BEGIN
    DROP INDEX [IX_CounterTransactions_BranchCode] ON [CounterTransactions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101135451_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_TransactionLines_LineType_Sku] ON [TransactionLines] ([LineType], [Sku]) INCLUDE ([Quantity], [UnitPrice]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101135451_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_BranchCode_OpenedAtUtc] ON [CounterTransactions] ([BranchCode], [OpenedAtUtc] DESC) INCLUDE ([Status], [TaxTotal]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101135451_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_BranchCode_Status_OpenedAtUtc] ON [CounterTransactions] ([BranchCode], [Status], [OpenedAtUtc]) INCLUDE ([TaxTotal]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101135451_AddReportingIndexes'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_TimestampUtc] ON [AuditEntries] ([TimestampUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101135451_AddReportingIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101135451_AddReportingIndexes', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101140000_AdjustStockCreatesMissingRows'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
        @BranchCode    varchar(5),
        @Sku           varchar(8),
        @Delta         int,
        @Reason        nvarchar(200),
        @ChangedBy     nvarchar(100),
        @QuantityAfter int OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF @Delta = 0
            THROW 50010, 'The adjustment must not be zero.', 1;
        IF LEN(LTRIM(RTRIM(COALESCE(@Reason, N'')))) = 0
            THROW 50011, 'A reason is required.', 1;

        BEGIN TRY
            BEGIN TRANSACTION;

            -- The WHERE clause refuses an update that would take the stock below zero.
            UPDATE dbo.BranchStock
            SET Quantity = Quantity + @Delta,
                ModifiedAtUtc = SYSUTCDATETIME()
            WHERE BranchCode = @BranchCode AND Sku = @Sku AND Quantity + @Delta >= 0;

            IF @@ROWCOUNT = 0
            BEGIN
                IF EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchCode = @BranchCode AND Sku = @Sku)
                    THROW 50013, 'The adjustment would take the stock below zero.', 1;
                IF @Delta < 0
                    THROW 50013, 'The adjustment would take the stock below zero.', 1;

                -- First stock of this item at this branch.
                INSERT INTO dbo.BranchStock (BranchCode, Sku, Quantity, CreatedBy)
                VALUES (@BranchCode, @Sku, @Delta, @ChangedBy);
            END;

            SELECT @QuantityAfter = Quantity
            FROM dbo.BranchStock
            WHERE BranchCode = @BranchCode AND Sku = @Sku;

            INSERT INTO dbo.StockAdjustments (BranchCode, Sku, Delta, QuantityAfter, Reason, ChangedBy, CreatedBy)
            VALUES (@BranchCode, @Sku, @Delta, @QuantityAfter, @Reason, @ChangedBy, @ChangedBy);

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0
                ROLLBACK TRANSACTION;
            THROW;
        END CATCH;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101140000_AdjustStockCreatesMissingRows'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101140000_AdjustStockCreatesMissingRows', N'10.0.14');
END;

COMMIT;
GO

