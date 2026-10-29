using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [dbo].[OrderItems] i LEFT JOIN [store].[Products] p ON p.Id=i.ProductId WHERE p.Id IS NULL)
                    THROW 51020, 'OrderItems contains missing products; repair source references before upgrading.', 1;
                """);
            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "OrderItems",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "PassPostId",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerNameSnapshot",
                table: "OrderItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotOrigin",
                table: "OrderItems",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "AT_ORDER");

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "OrderItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "SHOP_PRODUCT");

            migrationBuilder.AddColumn<byte[]>(
                name: "SourceVersionSnapshot",
                table: "OrderItems",
                type: "varbinary(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoreIdSnapshot",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreNameSnapshot",
                table: "OrderItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            // Existing product/name/price/image snapshots are deliberately left untouched.
            // Current shop/user names cannot be represented as historical names at order time.
            migrationBuilder.Sql("""
                UPDATE i SET SellerNameSnapshot=u.FullName,
                    StoreIdSnapshot=CASE WHEN s.OwnerId=o.SellerId AND LEN(LTRIM(RTRIM(s.Name)))>0 THEN s.Id END,
                    StoreNameSnapshot=CASE WHEN s.OwnerId=o.SellerId AND LEN(LTRIM(RTRIM(s.Name)))>0 THEN s.Name END,
                    SnapshotOrigin=CASE WHEN s.OwnerId=o.SellerId AND LEN(LTRIM(RTRIM(s.Name)))>0 THEN 'LEGACY_BACKFILL' ELSE 'LEGACY_UNKNOWN' END
                FROM [dbo].[OrderItems] i
                JOIN [dbo].[Orders] o ON o.Id=i.OrderId
                JOIN [auth].[Users] u ON u.Id=o.SellerId
                JOIN [store].[Products] p ON p.Id=i.ProductId
                LEFT JOIN [store].[Stores] s ON s.Id=p.StoreId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_PassPostId",
                table: "OrderItems",
                column: "PassPostId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderItems_ShopSnapshot",
                table: "OrderItems",
                sql: "([StoreIdSnapshot] IS NULL AND [StoreNameSnapshot] IS NULL) OR ([SourceType] = 'SHOP_PRODUCT' AND [StoreIdSnapshot] IS NOT NULL AND [StoreIdSnapshot] > 0 AND [StoreNameSnapshot] IS NOT NULL AND LEN(LTRIM(RTRIM([StoreNameSnapshot]))) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderItems_SnapshotOrigin",
                table: "OrderItems",
                sql: "[SnapshotOrigin] IN ('AT_ORDER','LEGACY_BACKFILL','LEGACY_UNKNOWN')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderItems_Source",
                table: "OrderItems",
                sql: "([SourceType] = 'SHOP_PRODUCT' AND [ProductId] IS NOT NULL AND [PassPostId] IS NULL) OR ([SourceType] = 'PASS_POST' AND [ProductId] IS NULL AND [PassPostId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_PassPostInfos_PassPostId",
                table: "OrderItems",
                column: "PassPostId",
                principalSchema: "forum",
                principalTable: "PassPostInfos",
                principalColumn: "PostId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalSchema: "store",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [dbo].[OrderItems] WHERE SourceType <> 'SHOP_PRODUCT' OR PassPostId IS NOT NULL OR ProductId IS NULL)
                    THROW 51021, 'Cannot roll back order sources while PASS order items exist.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_PassPostInfos_PassPostId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_PassPostId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderItems_ShopSnapshot",
                table: "OrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderItems_SnapshotOrigin",
                table: "OrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderItems_Source",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PassPostId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SellerNameSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SnapshotOrigin",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SourceVersionSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "StoreIdSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "StoreNameSnapshot",
                table: "OrderItems");

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "OrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
