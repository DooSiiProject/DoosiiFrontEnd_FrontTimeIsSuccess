using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddShopRelocationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShopRelocationRequests",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CurrentLatitude = table.Column<double>(type: "float", nullable: true),
                    CurrentLongitude = table.Column<double>(type: "float", nullable: true),
                    ProposedAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProposedLatitude = table.Column<double>(type: "float", nullable: false),
                    ProposedLongitude = table.Column<double>(type: "float", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopRelocationRequests", x => x.Id);
                    table.CheckConstraint("CK_ShopRelocationRequests_Coordinates", "[ProposedLatitude] >= -90 AND [ProposedLatitude] <= 90 AND [ProposedLongitude] >= -180 AND [ProposedLongitude] <= 180");
                    table.CheckConstraint("CK_ShopRelocationRequests_Status", "[Status] IN (N'PENDING', N'CANCELLED', N'APPROVED', N'REJECTED')");
                    table.ForeignKey(
                        name: "FK_ShopRelocationRequests_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "store",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopRelocationRequests_OnePendingPerStore",
                schema: "store",
                table: "ShopRelocationRequests",
                column: "StoreId",
                unique: true,
                filter: "[Status] = N'PENDING'");

            migrationBuilder.CreateIndex(
                name: "IX_ShopRelocationRequests_StoreId_ClientRequestId",
                schema: "store",
                table: "ShopRelocationRequests",
                columns: new[] { "StoreId", "ClientRequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopRelocationRequests",
                schema: "store");
        }
    }
}
