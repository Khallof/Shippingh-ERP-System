using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Step09_ExtraFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExtraFee",
                table: "Shipments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtraFee",
                table: "ShipmentPricingSnapshots",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraFee",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ExtraFee",
                table: "ShipmentPricingSnapshots");
        }
    }
}
