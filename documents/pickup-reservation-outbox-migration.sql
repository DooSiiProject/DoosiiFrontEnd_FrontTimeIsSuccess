BEGIN TRANSACTION;
CREATE TABLE [notification].[PickupNotificationOutbox] (
    [Id] bigint NOT NULL IDENTITY,
    [NotificationId] int NOT NULL,
    [EventKey] nvarchar(160) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [AvailableAt] datetime2 NOT NULL,
    [Attempts] int NOT NULL,
    CONSTRAINT [PK_PickupNotificationOutbox] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PickupOutbox_Attempts] CHECK ([Attempts] >= 0),
    CONSTRAINT [FK_PickupNotificationOutbox_Notifications_NotificationId] FOREIGN KEY ([NotificationId]) REFERENCES [notification].[Notifications] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_PickupNotificationOutbox_AvailableAt_Id] ON [notification].[PickupNotificationOutbox] ([AvailableAt], [Id]);

CREATE UNIQUE INDEX [IX_PickupNotificationOutbox_EventKey] ON [notification].[PickupNotificationOutbox] ([EventKey]);

CREATE UNIQUE INDEX [IX_PickupNotificationOutbox_NotificationId] ON [notification].[PickupNotificationOutbox] ([NotificationId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261028022627_AddPickupNotificationOutbox', N'9.0.0');

COMMIT;
GO

