using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddProductListingDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_StoreId",
                schema: "store",
                table: "Products");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                schema: "store",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailsJson",
                schema: "store",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                schema: "store",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "store",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Products_StoreId_ClientRequestId",
                schema: "store",
                table: "Products",
                columns: new[] { "StoreId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_StoreId_ClientRequestId",
                schema: "store",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                schema: "store",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DetailsJson",
                schema: "store",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                schema: "store",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "store",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_StoreId",
                schema: "store",
                table: "Products",
                column: "StoreId");
        }
    }
}
