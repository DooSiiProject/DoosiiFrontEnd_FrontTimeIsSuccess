BEGIN TRANSACTION;
ALTER TABLE [store].[Products] ADD [ReservedPickupReservationId] int NULL;

CREATE TABLE [store].[PickupReservations] (
    [Id] int NOT NULL IDENTITY,
    [BuyerId] int NOT NULL,
    [SellerId] int NOT NULL,
    [StoreId] int NOT NULL,
    [ProductId] int NOT NULL,
    [ClientRequestId] uniqueidentifier NOT NULL,
    [RequestHash] nvarchar(64) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [ContactName] nvarchar(256) NOT NULL,
    [ContactPhone] nvarchar(20) NOT NULL,
    [BuyerNote] nvarchar(500) NULL,
    [ProductTitleSnapshot] nvarchar(300) NOT NULL,
    [PriceSnapshot] decimal(18,2) NOT NULL,
    [ProductVersionSnapshot] varbinary(8) NOT NULL,
    [StoreNameSnapshot] nvarchar(200) NULL,
    [StoreAddressSnapshot] nvarchar(500) NULL,
    [StoreLatitudeSnapshot] float NULL,
    [StoreLongitudeSnapshot] float NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ScheduledAt] datetime2 NOT NULL,
    [ResponseDeadline] datetime2 NOT NULL,
    [AcceptedAt] datetime2 NULL,
    [ExpiresAt] datetime2 NULL,
    [ClosedAt] datetime2 NULL,
    [ClosedById] int NULL,
    [CloseReason] nvarchar(500) NULL,
    [PickupCodeHash] varbinary(32) NULL,
    [ProtectedPickupCode] nvarchar(2048) NULL,
    [FailedCodeAttempts] int NOT NULL,
    [CodeLockedUntil] datetime2 NULL,
    [UpdatedAt] datetime2 NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_PickupReservations] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_PickupReservations_Id_ProductId] UNIQUE ([Id], [ProductId]),
    CONSTRAINT [CK_Pickup_Acceptance] CHECK (([AcceptedAt] IS NULL AND [ExpiresAt] IS NULL AND [StoreNameSnapshot] IS NULL AND [StoreAddressSnapshot] IS NULL AND [PickupCodeHash] IS NULL AND [ProtectedPickupCode] IS NULL AND [Status] IN ('PENDING','REJECTED','CANCELLED','EXPIRED')) OR ([AcceptedAt] IS NOT NULL AND [AcceptedAt] >= [CreatedAt] AND [AcceptedAt] < [ResponseDeadline] AND [ExpiresAt] IS NOT NULL AND [ExpiresAt] = DATEADD(minute,30,[ScheduledAt]) AND [StoreNameSnapshot] IS NOT NULL AND LEN(LTRIM(RTRIM([StoreNameSnapshot]))) > 0 AND [StoreAddressSnapshot] IS NOT NULL AND LEN(LTRIM(RTRIM([StoreAddressSnapshot]))) > 0 AND [PickupCodeHash] IS NOT NULL AND DATALENGTH([PickupCodeHash]) = 32 AND [ProtectedPickupCode] IS NOT NULL AND LEN([ProtectedPickupCode]) > 0 AND [Status] IN ('ACCEPTED','CANCELLED','EXPIRED','COMPLETED'))),
    CONSTRAINT [CK_Pickup_BuyerSeller] CHECK ([BuyerId] <> [SellerId]),
    CONSTRAINT [CK_Pickup_Closure] CHECK (([Status] IN ('PENDING','ACCEPTED') AND [ClosedAt] IS NULL AND [ClosedById] IS NULL AND [CloseReason] IS NULL) OR ([Status] IN ('REJECTED','CANCELLED','EXPIRED','COMPLETED') AND [ClosedAt] IS NOT NULL AND [ClosedAt] >= [CreatedAt] AND ([AcceptedAt] IS NULL OR [ClosedAt] >= [AcceptedAt]) AND [CloseReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CloseReason]))) > 0 AND (([Status] = 'EXPIRED' AND [ClosedById] IS NULL AND (([AcceptedAt] IS NULL AND [ClosedAt] >= [ResponseDeadline]) OR ([AcceptedAt] IS NOT NULL AND [ClosedAt] >= [ExpiresAt]))) OR ([Status] = 'COMPLETED' AND [ClosedById] IS NOT NULL AND [ClosedById] = [SellerId] AND [ClosedAt] < [ExpiresAt]) OR ([Status] = 'REJECTED' AND [ClosedById] IS NOT NULL AND [ClosedById] = [SellerId]) OR ([Status] = 'CANCELLED' AND [ClosedById] IS NOT NULL AND [ClosedById] IN ([BuyerId],[SellerId]))))),
    CONSTRAINT [CK_Pickup_CodeAttempts] CHECK ([FailedCodeAttempts] BETWEEN 0 AND 5 AND ([CodeLockedUntil] IS NULL OR ([AcceptedAt] IS NOT NULL AND [CodeLockedUntil] >= [AcceptedAt]))),
    CONSTRAINT [CK_Pickup_Coordinates] CHECK (([StoreLatitudeSnapshot] IS NULL AND [StoreLongitudeSnapshot] IS NULL) OR ([StoreLatitudeSnapshot] IS NOT NULL AND [StoreLongitudeSnapshot] IS NOT NULL AND [StoreLatitudeSnapshot] BETWEEN -90 AND 90 AND [StoreLongitudeSnapshot] BETWEEN -180 AND 180)),
    CONSTRAINT [CK_Pickup_Request] CHECK ([ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64 AND [RequestHash] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%' AND DATALENGTH([ProductVersionSnapshot]) = 8),
    CONSTRAINT [CK_Pickup_Schedule] CHECK ([ScheduledAt] >= DATEADD(minute,30,[CreatedAt]) AND [ScheduledAt] <= DATEADD(hour,24,[CreatedAt]) AND [ResponseDeadline] > [CreatedAt] AND [ResponseDeadline] <= [ScheduledAt] AND [ResponseDeadline] <= DATEADD(hour,2,[CreatedAt])),
    CONSTRAINT [CK_Pickup_Status] CHECK ([Status] IN ('PENDING','ACCEPTED','REJECTED','CANCELLED','EXPIRED','COMPLETED')),
    CONSTRAINT [CK_Pickup_TextPrice] CHECK ([PriceSnapshot] > 0 AND LEN(LTRIM(RTRIM([ContactName]))) > 0 AND LEN(LTRIM(RTRIM([ContactPhone]))) > 0 AND LEN(LTRIM(RTRIM([ProductTitleSnapshot]))) > 0),
    CONSTRAINT [FK_PickupReservations_Products_ProductId_StoreId] FOREIGN KEY ([ProductId], [StoreId]) REFERENCES [store].[Products] ([Id], [StoreId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PickupReservations_Stores_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [store].[Stores] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PickupReservations_Users_BuyerId] FOREIGN KEY ([BuyerId]) REFERENCES [auth].[Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PickupReservations_Users_ClosedById] FOREIGN KEY ([ClosedById]) REFERENCES [auth].[Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PickupReservations_Users_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [auth].[Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Products_ReservedPickupReservationId_Id] ON [store].[Products] ([ReservedPickupReservationId], [Id]);

ALTER TABLE [store].[Products] ADD CONSTRAINT [CK_Products_ReservationOwner] CHECK ([ReservedPickupReservationId] IS NULL OR ([ReservedOrderId] IS NULL AND [Status] = 'LOCKED'));

CREATE UNIQUE INDEX [IX_PickupReservations_BuyerId_ClientRequestId] ON [store].[PickupReservations] ([BuyerId], [ClientRequestId]);

CREATE UNIQUE INDEX [IX_PickupReservations_BuyerId_ProductId] ON [store].[PickupReservations] ([BuyerId], [ProductId]) WHERE [Status] IN ('PENDING','ACCEPTED');

CREATE INDEX [IX_PickupReservations_BuyerId_Status_CreatedAt_Id] ON [store].[PickupReservations] ([BuyerId], [Status], [CreatedAt], [Id]);

CREATE INDEX [IX_PickupReservations_ClosedById] ON [store].[PickupReservations] ([ClosedById]);

CREATE INDEX [IX_PickupReservations_ExpiresAt] ON [store].[PickupReservations] ([ExpiresAt]) WHERE [Status] = 'ACCEPTED';

CREATE UNIQUE INDEX [IX_PickupReservations_ProductId] ON [store].[PickupReservations] ([ProductId]) WHERE [Status] = 'ACCEPTED';

CREATE INDEX [IX_PickupReservations_ProductId_StoreId] ON [store].[PickupReservations] ([ProductId], [StoreId]);

CREATE INDEX [IX_PickupReservations_ResponseDeadline] ON [store].[PickupReservations] ([ResponseDeadline]) WHERE [Status] = 'PENDING';

CREATE INDEX [IX_PickupReservations_SellerId] ON [store].[PickupReservations] ([SellerId]);

CREATE INDEX [IX_PickupReservations_StoreId_Status_CreatedAt_Id] ON [store].[PickupReservations] ([StoreId], [Status], [CreatedAt], [Id]);

ALTER TABLE [store].[Products] ADD CONSTRAINT [FK_Products_PickupReservations_ReservedPickupReservationId_Id] FOREIGN KEY ([ReservedPickupReservationId], [Id]) REFERENCES [store].[PickupReservations] ([Id], [ProductId]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261027162606_AddPickupReservations', N'9.0.0');

COMMIT;
GO

