using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMerchantProfileLeaderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressType",
                schema: "auth",
                table: "MerchantProfiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "auth",
                table: "MerchantProfiles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                schema: "auth",
                table: "MerchantProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstablishedDate",
                schema: "auth",
                table: "MerchantProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShopMediaUrls",
                schema: "auth",
                table: "MerchantProfiles",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                schema: "auth",
                table: "MerchantProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressType",
                schema: "auth",
                table: "MerchantProfiles");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "auth",
                table: "MerchantProfiles");

            migrationBuilder.DropColumn(
                name: "ContactName",
                schema: "auth",
                table: "MerchantProfiles");

            migrationBuilder.DropColumn(
                name: "EstablishedDate",
                schema: "auth",
                table: "MerchantProfiles");

            migrationBuilder.DropColumn(
                name: "ShopMediaUrls",
                schema: "auth",
                table: "MerchantProfiles");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                schema: "auth",
                table: "MerchantProfiles");
        }
    }
}
