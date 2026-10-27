using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class ConnectEscrowCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<int>(
                name: "ReservedOrderId",
                schema: "store",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingStatus",
                table: "PaymentLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LEGACY");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentDetailsJson",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentLinkId",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Orders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<string>(
                name: "ThumbnailSnapshot",
                table: "OrderItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProductNameSnapshot",
                table: "OrderItems",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentLogs_Gateway_TransactionCode",
                table: "PaymentLogs",
                columns: new[] { "Gateway", "TransactionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BuyerId_ClientRequestId",
                table: "Orders",
                columns: new[] { "BuyerId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");
            migrationBuilder.Sql(@"
UPDATE p SET [ReservedOrderId] = history.[OrderId]
FROM [store].[Products] p
CROSS APPLY (SELECT TOP (1) oi.[OrderId] FROM [dbo].[OrderItems] oi JOIN [dbo].[Orders] o ON o.[Id] = oi.[OrderId]
JOIN [store].[Stores] s ON s.[Id] = p.[StoreId]
WHERE oi.[ProductId] = p.[Id] AND o.[SellerId] = s.[OwnerId]
AND o.[Status] IN ('PENDING_PAYMENT','ESCROW_HOLDING','IN_TRANSIT','DISPUTED','COMPLETED_RELEASED') ORDER BY o.[Id] DESC) history
WHERE p.[Status] IN ('LOCKED','SOLD') AND p.[ReservedOrderId] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentLogs_Gateway_TransactionCode",
                table: "PaymentLogs");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BuyerId_ClientRequestId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReservedOrderId",
                schema: "store",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                table: "PaymentLogs");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentDetailsJson",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentLinkId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "ThumbnailSnapshot",
                table: "OrderItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProductNameSnapshot",
                table: "OrderItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

        }
    }
}
