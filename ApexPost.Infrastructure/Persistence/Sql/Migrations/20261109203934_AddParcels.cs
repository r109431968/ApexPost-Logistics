using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddParcels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parcels",
                columns: table => new
                {
                    TrackingNumber = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RecipientAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientCity = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcels", x => x.TrackingNumber);
                    table.CheckConstraint("CK_Parcels_Status", "[Status] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_Parcels_Weight", "[WeightKg] >= 0.1 AND [WeightKg] <= 70");
                });

            migrationBuilder.CreateTable(
                name: "ParcelScans",
                columns: table => new
                {
                    ScanId = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TrackingNumber = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    ScanType = table.Column<int>(type: "int", nullable: false),
                    CourierId = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ScannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecipientSignatureName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelScans", x => x.ScanId);
                    table.CheckConstraint("CK_ParcelScans_ScanType", "[ScanType] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ParcelScans_Parcels_TrackingNumber",
                        column: x => x.TrackingNumber,
                        principalTable: "Parcels",
                        principalColumn: "TrackingNumber",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_RecipientCity",
                table: "Parcels",
                columns: new[] { "Status", "RecipientCity" });

            migrationBuilder.CreateIndex(
                name: "IX_ParcelScans_TrackingNumber_ScannedAtUtc",
                table: "ParcelScans",
                columns: new[] { "TrackingNumber", "ScannedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParcelScans");

            migrationBuilder.DropTable(
                name: "Parcels");
        }
    }
}
