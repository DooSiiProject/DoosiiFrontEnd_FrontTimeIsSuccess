using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddShopRelocationReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                schema: "store",
                table: "ShopRelocationRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewRequestId",
                schema: "store",
                table: "ShopRelocationRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                schema: "store",
                table: "ShopRelocationRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByName",
                schema: "store",
                table: "ShopRelocationRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserId",
                schema: "store",
                table: "ShopRelocationRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopRelocationRequests_ReviewRequestId",
                schema: "store",
                table: "ShopRelocationRequests",
                column: "ReviewRequestId",
                unique: true,
                filter: "[ReviewRequestId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShopRelocationRequests_ReviewRequestId",
                schema: "store",
                table: "ShopRelocationRequests");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                schema: "store",
                table: "ShopRelocationRequests");

            migrationBuilder.DropColumn(
                name: "ReviewRequestId",
                schema: "store",
                table: "ShopRelocationRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                schema: "store",
                table: "ShopRelocationRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedByName",
                schema: "store",
                table: "ShopRelocationRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                schema: "store",
                table: "ShopRelocationRequests");
        }
    }
}
