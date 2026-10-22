using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Step09_Consignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConsignmentNumber",
                table: "Shipments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ConsignmentNumber",
                table: "Shipments",
                column: "ConsignmentNumber",
                filter: "[ConsignmentNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_ConsignmentNumber",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ConsignmentNumber",
                table: "Shipments");
        }
    }
}
