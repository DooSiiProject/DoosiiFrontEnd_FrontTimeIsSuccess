BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM [dbo].[OrderItems] i LEFT JOIN [store].[Products] p ON p.Id=i.ProductId WHERE p.Id IS NULL)
    THROW 51020, 'OrderItems contains missing products; repair source references before upgrading.', 1;

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderItems]') AND [c].[name] = N'ProductId');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [OrderItems] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [OrderItems] ALTER COLUMN [ProductId] int NULL;

ALTER TABLE [OrderItems] ADD [PassPostId] int NULL;

ALTER TABLE [OrderItems] ADD [SellerNameSnapshot] nvarchar(256) NULL;

ALTER TABLE [OrderItems] ADD [SnapshotOrigin] nvarchar(24) NOT NULL DEFAULT N'AT_ORDER';

ALTER TABLE [OrderItems] ADD [SourceType] nvarchar(20) NOT NULL DEFAULT N'SHOP_PRODUCT';

ALTER TABLE [OrderItems] ADD [SourceVersionSnapshot] varbinary(8) NULL;

ALTER TABLE [OrderItems] ADD [StoreIdSnapshot] int NULL;

ALTER TABLE [OrderItems] ADD [StoreNameSnapshot] nvarchar(256) NULL;

UPDATE i SET SellerNameSnapshot=u.FullName,
    StoreIdSnapshot=CASE WHEN s.OwnerId=o.SellerId AND LEN(LTRIM(RTRIM(s.Name)))>0 THEN s.Id END,
    StoreNameSnapshot=CASE WHEN s.OwnerId=o.SellerId AND LEN(LTRIM(RTRIM(s.Name)))>0 THEN s.Name END,
    SnapshotOrigin=CASE WHEN s.OwnerId=o.SellerId AND LEN(LTRIM(RTRIM(s.Name)))>0 THEN 'LEGACY_BACKFILL' ELSE 'LEGACY_UNKNOWN' END
FROM [dbo].[OrderItems] i
JOIN [dbo].[Orders] o ON o.Id=i.OrderId
JOIN [auth].[Users] u ON u.Id=o.SellerId
JOIN [store].[Products] p ON p.Id=i.ProductId
LEFT JOIN [store].[Stores] s ON s.Id=p.StoreId;

CREATE INDEX [IX_OrderItems_PassPostId] ON [OrderItems] ([PassPostId]);

CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);

ALTER TABLE [OrderItems] ADD CONSTRAINT [CK_OrderItems_ShopSnapshot] CHECK (([StoreIdSnapshot] IS NULL AND [StoreNameSnapshot] IS NULL) OR ([SourceType] = 'SHOP_PRODUCT' AND [StoreIdSnapshot] IS NOT NULL AND [StoreIdSnapshot] > 0 AND [StoreNameSnapshot] IS NOT NULL AND LEN(LTRIM(RTRIM([StoreNameSnapshot]))) > 0));

ALTER TABLE [OrderItems] ADD CONSTRAINT [CK_OrderItems_SnapshotOrigin] CHECK ([SnapshotOrigin] IN ('AT_ORDER','LEGACY_BACKFILL','LEGACY_UNKNOWN'));

ALTER TABLE [OrderItems] ADD CONSTRAINT [CK_OrderItems_Source] CHECK (([SourceType] = 'SHOP_PRODUCT' AND [ProductId] IS NOT NULL AND [PassPostId] IS NULL) OR ([SourceType] = 'PASS_POST' AND [ProductId] IS NULL AND [PassPostId] IS NOT NULL));

ALTER TABLE [OrderItems] ADD CONSTRAINT [FK_OrderItems_PassPostInfos_PassPostId] FOREIGN KEY ([PassPostId]) REFERENCES [forum].[PassPostInfos] ([PostId]) ON DELETE NO ACTION;

ALTER TABLE [OrderItems] ADD CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [store].[Products] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029162005_AddOrderItemSources', N'9.0.0');

COMMIT;
GO

