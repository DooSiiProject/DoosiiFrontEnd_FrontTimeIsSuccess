using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doosii.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPickupNotificationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PickupNotificationOutbox",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationId = table.Column<int>(type: "int", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvailableAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickupNotificationOutbox", x => x.Id);
                    table.CheckConstraint("CK_PickupOutbox_Attempts", "[Attempts] >= 0");
                    table.ForeignKey(
                        name: "FK_PickupNotificationOutbox_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "notification",
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PickupNotificationOutbox_AvailableAt_Id",
                schema: "notification",
                table: "PickupNotificationOutbox",
                columns: new[] { "AvailableAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PickupNotificationOutbox_EventKey",
                schema: "notification",
                table: "PickupNotificationOutbox",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickupNotificationOutbox_NotificationId",
                schema: "notification",
                table: "PickupNotificationOutbox",
                column: "NotificationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [notification].[PickupNotificationOutbox]) THROW 51011, 'Cannot discard pending pickup notifications.', 1;");
            migrationBuilder.DropTable(
                name: "PickupNotificationOutbox",
                schema: "notification");
        }
    }
}
