using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class BranchAddressOwnedType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address_City",
                table: "Branches",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address_Country",
                table: "Branches",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address_Line1",
                table: "Branches",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address_Postcode",
                table: "Branches",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address_City",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Address_Country",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Address_Line1",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Address_Postcode",
                table: "Branches");
        }
    }
}
