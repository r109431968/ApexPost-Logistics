using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ApexPost.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Role = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    BranchCode = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    Controller = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RouteValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    BranchCode = table.Column<string>(type: "varchar(5)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    ClosesAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.BranchCode);
                    table.CheckConstraint("CK_Branches_Hours", "[ClosesAt] > [OpensAt]");
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShippingZones",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingZones", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Counters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchCode = table.Column<string>(type: "varchar(5)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Counters_Branches_BranchCode",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Sku = table.Column<string>(type: "varchar(8)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxCategory = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Sku);
                    table.CheckConstraint("CK_Products_Price", "[UnitPrice] >= 0");
                    table.CheckConstraint("CK_Products_TaxCategory", "[TaxCategory] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Products_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "varchar(254)", unicode: false, maxLength: 254, nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    HomeBranchCode = table.Column<string>(type: "varchar(5)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Staff_Branches_HomeBranchCode",
                        column: x => x.HomeBranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Staff_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShippingRates",
                columns: table => new
                {
                    ZoneCode = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    BaseRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PerKgRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingRates", x => x.ZoneCode);
                    table.CheckConstraint("CK_ShippingRates_Base", "[BaseRate] >= 0");
                    table.CheckConstraint("CK_ShippingRates_PerKg", "[PerKgRate] >= 0");
                    table.ForeignKey(
                        name: "FK_ShippingRates_ShippingZones_ZoneCode",
                        column: x => x.ZoneCode,
                        principalTable: "ShippingZones",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BranchStock",
                columns: table => new
                {
                    BranchCode = table.Column<string>(type: "varchar(5)", nullable: false),
                    Sku = table.Column<string>(type: "varchar(8)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchStock", x => new { x.BranchCode, x.Sku });
                    table.CheckConstraint("CK_BranchStock_Quantity", "[Quantity] >= 0");
                    table.ForeignKey(
                        name: "FK_BranchStock_Branches_BranchCode",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchStock_Products_Sku",
                        column: x => x.Sku,
                        principalTable: "Products",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchCode = table.Column<string>(type: "varchar(5)", nullable: false),
                    Sku = table.Column<string>(type: "varchar(8)", nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    QuantityAfter = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustments", x => x.Id);
                    table.CheckConstraint("CK_StockAdjustments_After", "[QuantityAfter] >= 0");
                    table.CheckConstraint("CK_StockAdjustments_Delta", "[Delta] <> 0");
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Branches_BranchCode",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Products_Sku",
                        column: x => x.Sku,
                        principalTable: "Products",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CounterTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchCode = table.Column<string>(type: "varchar(5)", nullable: false),
                    CounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClerkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReversalReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CounterTransactions", x => x.Id);
                    table.CheckConstraint("CK_CounterTransactions_Status", "[Status] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_CounterTransactions_Tax", "[TaxTotal] >= 0");
                    table.ForeignKey(
                        name: "FK_CounterTransactions_Branches_BranchCode",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CounterTransactions_Counters_CounterId",
                        column: x => x.CounterId,
                        principalTable: "Counters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CounterTransactions_Staff_ClerkId",
                        column: x => x.ClerkId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdentityVerifications",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    MaskedNumber = table.Column<string>(type: "varchar(8)", unicode: false, maxLength: 8, nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityVerifications", x => x.TransactionId);
                    table.CheckConstraint("CK_IdentityVerifications_Document", "[DocumentType] IN (1, 2, 3)");
                    table.CheckConstraint("CK_IdentityVerifications_Masked", "[MaskedNumber] LIKE '****____' AND LEN([MaskedNumber]) = 8");
                    table.CheckConstraint("CK_IdentityVerifications_Result", "[Result] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_IdentityVerifications_CounterTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.TransactionId);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Payments_Method", "[Method] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Payments_CounterTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransactionLines",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    LineType = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxCategory = table.Column<int>(type: "int", nullable: false),
                    Sku = table.Column<string>(type: "varchar(8)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionLines", x => new { x.TransactionId, x.LineNumber });
                    table.CheckConstraint("CK_TransactionLines_Number", "[LineNumber] > 0");
                    table.CheckConstraint("CK_TransactionLines_Price", "[UnitPrice] >= 0");
                    table.CheckConstraint("CK_TransactionLines_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_TransactionLines_RetailFields", "[LineType] <> 'Retail' OR ([Sku] IS NOT NULL AND [Description] IS NOT NULL)");
                    table.CheckConstraint("CK_TransactionLines_Tax", "[TaxCategory] IN (1, 2, 3)");
                    table.CheckConstraint("CK_TransactionLines_Type", "[LineType] IN ('Retail', 'Shipping', 'MoneyOrder')");
                    table.ForeignKey(
                        name: "FK_TransactionLines_CounterTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionLines_Products_Sku",
                        column: x => x.Sku,
                        principalTable: "Products",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MoneyOrders",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Sender = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyOrders", x => new { x.TransactionId, x.LineNumber });
                    table.CheckConstraint("CK_MoneyOrders_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_MoneyOrders_Fee", "[Fee] >= 0");
                    table.ForeignKey(
                        name: "FK_MoneyOrders_TransactionLines_TransactionId_LineNumber",
                        columns: x => new { x.TransactionId, x.LineNumber },
                        principalTable: "TransactionLines",
                        principalColumns: new[] { "TransactionId", "LineNumber" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ShipmentType = table.Column<int>(type: "int", nullable: false),
                    WeightGrams = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    DestinationZone = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    ServiceLevel = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => new { x.TransactionId, x.LineNumber });
                    table.CheckConstraint("CK_Shipments_Service", "[ServiceLevel] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Shipments_Type", "[ShipmentType] IN (1, 2)");
                    table.CheckConstraint("CK_Shipments_Weight", "[WeightGrams] > 0");
                    table.ForeignKey(
                        name: "FK_Shipments_ShippingZones_DestinationZone",
                        column: x => x.DestinationZone,
                        principalTable: "ShippingZones",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shipments_TransactionLines_TransactionId_LineNumber",
                        columns: x => new { x.TransactionId, x.LineNumber },
                        principalTable: "TransactionLines",
                        principalColumns: new[] { "TransactionId", "LineNumber" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ProductCategories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Packaging" },
                    { 2, "Stationery" },
                    { 3, "OfficeSupplies" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "CounterClerk" },
                    { 2, "BranchManager" },
                    { 3, "Auditor" },
                    { 4, "Administrator" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchStock_Sku",
                table: "BranchStock",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_Counters_BranchCode_Label",
                table: "Counters",
                columns: new[] { "BranchCode", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_BranchCode",
                table: "CounterTransactions",
                column: "BranchCode");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_ClerkId",
                table: "CounterTransactions",
                column: "ClerkId");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_CounterId",
                table: "CounterTransactions",
                column: "CounterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_Name",
                table: "ProductCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_DestinationZone",
                table: "Shipments",
                column: "DestinationZone");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_Email",
                table: "Staff",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_HomeBranchCode",
                table: "Staff",
                column: "HomeBranchCode");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_RoleId",
                table: "Staff",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_BranchCode",
                table: "StockAdjustments",
                column: "BranchCode");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Sku",
                table: "StockAdjustments",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLines_Sku",
                table: "TransactionLines",
                column: "Sku");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "BranchStock");

            migrationBuilder.DropTable(
                name: "IdentityVerifications");

            migrationBuilder.DropTable(
                name: "MoneyOrders");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropTable(
                name: "ShippingRates");

            migrationBuilder.DropTable(
                name: "StockAdjustments");

            migrationBuilder.DropTable(
                name: "TransactionLines");

            migrationBuilder.DropTable(
                name: "ShippingZones");

            migrationBuilder.DropTable(
                name: "CounterTransactions");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Counters");

            migrationBuilder.DropTable(
                name: "Staff");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
