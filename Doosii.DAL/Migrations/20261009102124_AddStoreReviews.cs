using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoreReviews",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ImagesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreReviews", x => x.Id);
                    table.CheckConstraint("CK_StoreReviews_Rating", "[Rating] >= 1 AND [Rating] <= 5");
                    table.ForeignKey(
                        name: "FK_StoreReviews_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StoreReviews_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "store",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoreReviews_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreReviews_OrderId",
                schema: "store",
                table: "StoreReviews",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreReviews_StoreId_UserId",
                schema: "store",
                table: "StoreReviews",
                columns: new[] { "StoreId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreReviews_UserId",
                schema: "store",
                table: "StoreReviews",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreReviews",
                schema: "store");
        }
    }
}
