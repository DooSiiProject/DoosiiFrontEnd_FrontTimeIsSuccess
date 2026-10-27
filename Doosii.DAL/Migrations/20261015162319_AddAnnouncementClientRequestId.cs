using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncementClientRequestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BaleAnnouncements_SellerId",
                schema: "store",
                table: "BaleAnnouncements");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                schema: "store",
                table: "BaleAnnouncements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BaleAnnouncements_SellerId_ClientRequestId",
                schema: "store",
                table: "BaleAnnouncements",
                columns: new[] { "SellerId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BaleAnnouncements_SellerId_ClientRequestId",
                schema: "store",
                table: "BaleAnnouncements");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                schema: "store",
                table: "BaleAnnouncements");

            migrationBuilder.CreateIndex(
                name: "IX_BaleAnnouncements_SellerId",
                schema: "store",
                table: "BaleAnnouncements",
                column: "SellerId");
        }
    }
}
