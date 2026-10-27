using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddProductGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupPosition",
                schema: "store",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductGroupId",
                schema: "store",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductGroups",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductGroups", x => x.Id);
                    table.UniqueConstraint("AK_ProductGroups_Id_StoreId", x => new { x.Id, x.StoreId });
                    table.ForeignKey(
                        name: "FK_ProductGroups_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "store",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductGroupImages",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductGroupId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductGroupImages", x => x.Id);
                    table.CheckConstraint("CK_ProductGroupImages_Position", "[DisplayOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_ProductGroupImages_ProductGroups_ProductGroupId",
                        column: x => x.ProductGroupId,
                        principalSchema: "store",
                        principalTable: "ProductGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductGroupMutations",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ProductGroupId = table.Column<int>(type: "int", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PayloadHash = table.Column<string>(type: "char(64)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductGroupMutations", x => x.Id);
                    table.CheckConstraint("CK_ProductGroupMutations_Operation", "[Operation] IN (N'CREATE', N'UPDATE')");
                    table.ForeignKey(
                        name: "FK_ProductGroupMutations_ProductGroups_ProductGroupId_StoreId",
                        columns: x => new { x.ProductGroupId, x.StoreId },
                        principalSchema: "store",
                        principalTable: "ProductGroups",
                        principalColumns: new[] { "Id", "StoreId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductGroupId_GroupPosition_Id",
                schema: "store",
                table: "Products",
                columns: new[] { "ProductGroupId", "GroupPosition", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductGroupId_StoreId",
                schema: "store",
                table: "Products",
                columns: new[] { "ProductGroupId", "StoreId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_GroupPosition",
                schema: "store",
                table: "Products",
                sql: "([ProductGroupId] IS NULL AND [GroupPosition] IS NULL) OR ([ProductGroupId] IS NOT NULL AND [GroupPosition] IS NOT NULL AND [GroupPosition] >= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_ProductGroupImages_ProductGroupId_DisplayOrder_Id",
                schema: "store",
                table: "ProductGroupImages",
                columns: new[] { "ProductGroupId", "DisplayOrder", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductGroupMutations_ProductGroupId_StoreId",
                schema: "store",
                table: "ProductGroupMutations",
                columns: new[] { "ProductGroupId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductGroupMutations_StoreId_ClientRequestId",
                schema: "store",
                table: "ProductGroupMutations",
                columns: new[] { "StoreId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductGroups_StoreId_CreatedAt_Id",
                schema: "store",
                table: "ProductGroups",
                columns: new[] { "StoreId", "CreatedAt", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ProductGroups_ProductGroupId_StoreId",
                schema: "store",
                table: "Products",
                columns: new[] { "ProductGroupId", "StoreId" },
                principalSchema: "store",
                principalTable: "ProductGroups",
                principalColumns: new[] { "Id", "StoreId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_ProductGroups_ProductGroupId_StoreId",
                schema: "store",
                table: "Products");

            migrationBuilder.DropTable(
                name: "ProductGroupImages",
                schema: "store");

            migrationBuilder.DropTable(
                name: "ProductGroupMutations",
                schema: "store");

            migrationBuilder.DropTable(
                name: "ProductGroups",
                schema: "store");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProductGroupId_GroupPosition_Id",
                schema: "store",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProductGroupId_StoreId",
                schema: "store",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_GroupPosition",
                schema: "store",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "GroupPosition",
                schema: "store",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductGroupId",
                schema: "store",
                table: "Products");
        }
    }
}
