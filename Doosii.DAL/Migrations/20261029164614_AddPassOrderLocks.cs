using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPassOrderLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PassOrderLocks",
                schema: "forum",
                columns: table => new
                {
                    PostId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassOrderLocks", x => x.PostId);
                    table.ForeignKey(
                        name: "FK_PassOrderLocks_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PassOrderLocks_PassPostInfos_PostId",
                        column: x => x.PostId,
                        principalSchema: "forum",
                        principalTable: "PassPostInfos",
                        principalColumn: "PostId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PassOrderLocks_OrderId",
                schema: "forum",
                table: "PassOrderLocks",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [forum].[PassOrderLocks]) THROW 51022, 'Cannot discard active PASS order locks.', 1;");
            migrationBuilder.DropTable(
                name: "PassOrderLocks",
                schema: "forum");
        }
    }
}
