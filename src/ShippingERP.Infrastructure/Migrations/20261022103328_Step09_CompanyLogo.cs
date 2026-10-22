using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShippingERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Step09_CompanyLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "CompanyProfiles",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "LogoImage",
                table: "CompanyProfiles",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LogoUpdatedAtUtc",
                table: "CompanyProfiles",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "CompanyProfiles");

            migrationBuilder.DropColumn(
                name: "LogoImage",
                table: "CompanyProfiles");

            migrationBuilder.DropColumn(
                name: "LogoUpdatedAtUtc",
                table: "CompanyProfiles");
        }
    }
}
