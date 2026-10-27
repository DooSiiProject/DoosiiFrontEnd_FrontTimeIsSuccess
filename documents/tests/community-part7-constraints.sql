-- Run only against the isolated Part7 schema test database after the new migration.
-- All fixtures are rolled back, including when a test fails.
SET NOCOUNT ON;
SET XACT_ABORT OFF;
IF DB_NAME() NOT LIKE 'DoosiiPart7SchemaTest[_]%'
    THROW 51020, 'Only run this script against an isolated Part7 test database.', 1;
BEGIN TRANSACTION;
BEGIN TRY
    INSERT auth.Users (Email, FullName, PasswordHash, Role, IsGoogle, CreatedAt)
    VALUES ('part7-schema@example.invalid', N'Schema test', 'not-a-login', 'CasualUser', 0, SYSUTCDATETIME());
    DECLARE @user int = SCOPE_IDENTITY();
    INSERT store.Stores (OwnerId, Name, IsActive, RatingAverage, RatingCount, CreatedAt)
    VALUES (@user, N'Test store A', 1, 0, 0, SYSUTCDATETIME());
    DECLARE @store int = SCOPE_IDENTITY();
    INSERT store.Stores (OwnerId, Name, IsActive, RatingAverage, RatingCount, CreatedAt)
    VALUES (@user, N'Test store B', 1, 0, 0, SYSUTCDATETIME());
    DECLARE @otherStore int = SCOPE_IDENTITY();
    INSERT store.Categories (Name, Slug) VALUES (N'Test category', 'part7-schema-test');
    DECLARE @category int = SCOPE_IDENTITY();
    INSERT store.Products (StoreId, CategoryId, Title, Price, ConditionPercent, Status, CreatedAt, IsPublished)
    VALUES (@store, @category, N'Test product', 100, 100, 'AVAILABLE', SYSUTCDATETIME(), 1);
    DECLARE @product int = SCOPE_IDENTITY();
    INSERT forum.Posts (AuthorId, Type, Title, PostDescription, ClientRequestId, CreatedAt)
    VALUES (@user, 'PASS', N'Pass title', N'Post description', NEWID(), SYSUTCDATETIME());
    DECLARE @pass int = SCOPE_IDENTITY();
    INSERT forum.Posts (AuthorId, Type, Title, PostDescription, ClientRequestId, CreatedAt)
    VALUES (@user, 'OTHER', N'Other title', N'Text only is valid', NEWID(), SYSUTCDATETIME());
    DECLARE @other int = SCOPE_IDENTITY();
    INSERT forum.PassPostInfos (PostId, PostType, ProductName, Price, Size, ConditionPercent, Defects, StyleTagsJson, MeasurementsJson)
    VALUES (@pass, 'PASS', N'Denim jacket', 250000, 'M', 90, N'Không có', '["vintage"]', '[{"name":"Vai","unit":"cm","value":44}]');
    INSERT forum.PostMedia (PostId, PostType, MediaType, SourceUrl, StoragePublicId, DisplayOrder)
    VALUES (@pass, 'PASS', 'IMAGE', 'https://example.invalid/image.jpg', 'test/image', 0),
           (@pass, 'PASS', 'VIDEO', 'https://example.invalid/video.mp4', 'test/video', 1);
    INSERT forum.PostMedia (PostId, PostType, MediaType, SourceUrl, StoragePublicId, DisplayOrder, StoreId, ProductId)
    VALUES (@other, 'OTHER', 'IMAGE', 'https://example.invalid/product.jpg', 'test/product', 0, @store, @product);
    INSERT forum.Comments (PostId, AuthorId, Content, CreatedAt)
    VALUES (@pass, @user, N'Root comment', SYSUTCDATETIME());
    DECLARE @comment int = SCOPE_IDENTITY();
    INSERT forum.Comments (PostId, AuthorId, ParentCommentId, Content, CreatedAt)
    VALUES (@pass, @user, @comment, N'Reply', SYSUTCDATETIME());
    INSERT forum.Reactions (PostId, UserId, ReactionType, CreatedAt)
    VALUES (@pass, @user, 'LIKE', SYSUTCDATETIME());
    INSERT forum.PostReports (PostId, ReporterId, Reason, CreatedAt)
    VALUES (@pass, @user, 'OTHER', SYSUTCDATETIME());

    DECLARE @cases table (Name nvarchar(100), SqlText nvarchar(max), ExpectedError int);
    INSERT @cases VALUES
    ('PASS cannot tag store', N'UPDATE forum.PostMedia SET StoreId=@store WHERE PostId=@pass', 547),
    ('PASS cannot tag product', N'UPDATE forum.PostMedia SET StoreId=@store, ProductId=@product WHERE PostId=@pass', 547),
    ('Cannot disguise PASS media as OTHER', N'UPDATE forum.PostMedia SET PostType=''OTHER'', StoreId=@store WHERE PostId=@pass', 547),
    ('Product must belong to tagged store', N'UPDATE forum.PostMedia SET StoreId=@otherStore WHERE PostId=@other', 547),
    ('Product requires store', N'UPDATE forum.PostMedia SET StoreId=NULL WHERE PostId=@other', 547),
    ('Media position unique', N'UPDATE forum.PostMedia SET DisplayOrder=0 WHERE PostId=@pass', 2601),
    ('Negative price rejected', N'UPDATE forum.PassPostInfos SET Price=-1 WHERE PostId=@pass', 547),
    ('Invalid condition rejected', N'UPDATE forum.PassPostInfos SET ConditionPercent=101 WHERE PostId=@pass', 547),
    ('Defects must be explicit', N'UPDATE forum.PassPostInfos SET Defects='' '' WHERE PostId=@pass', 547),
    ('Tags must be JSON array', N'UPDATE forum.PassPostInfos SET StyleTagsJson=''{}'' WHERE PostId=@pass', 547),
    ('Measurements must be JSON array', N'UPDATE forum.PassPostInfos SET MeasurementsJson=''invalid'' WHERE PostId=@pass', 547),
    ('OTHER cannot have PASS info', N'INSERT forum.PassPostInfos (PostId,PostType,ProductName,Price,Size,ConditionPercent,Defects,StyleTagsJson) VALUES (@other,''PASS'',''Item'',1,''M'',90,''None'',''[]'')', 547),
    ('Reply cannot cross posts', N'INSERT forum.Comments(PostId,AuthorId,ParentCommentId,Content,CreatedAt) VALUES(@other,@user,@comment,''Cross post'',SYSUTCDATETIME())', 547),
    ('Reaction unique per user and post', N'INSERT forum.Reactions(PostId,UserId,ReactionType,CreatedAt) VALUES(@pass,@user,''LIKE'',SYSUTCDATETIME())', 2601),
    ('Pending report unique', N'INSERT forum.PostReports(PostId,ReporterId,Reason,CreatedAt) VALUES(@pass,@user,''SCAM'',SYSUTCDATETIME())', 2601),
    ('Orphan interaction rejected', N'UPDATE forum.Reactions SET PostId=2147483647 WHERE PostId=@pass', 547),
    ('Sale status independent from visibility', N'UPDATE forum.Posts SET VisibilityStatus=''SOLD'' WHERE Id=@pass', 547),
    ('No unsupported sale lock', N'UPDATE forum.PassPostInfos SET SaleStatus=''LOCKED'' WHERE PostId=@pass', 547);

    DECLARE @name nvarchar(100), @sql nvarchar(max), @expected int, @actual int, @passed int=0;
    DECLARE checks CURSOR LOCAL FAST_FORWARD FOR SELECT Name, SqlText, ExpectedError FROM @cases;
    OPEN checks;
    FETCH NEXT FROM checks INTO @name, @sql, @expected;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @actual=0;
        BEGIN TRY
            EXEC sp_executesql @sql,
                N'@user int,@store int,@otherStore int,@product int,@pass int,@other int,@comment int',
                @user,@store,@otherStore,@product,@pass,@other,@comment;
        END TRY
        BEGIN CATCH
            SET @actual=ERROR_NUMBER();
        END CATCH;
        IF @actual<>@expected
        BEGIN
            DECLARE @failure nvarchar(2048)=CONCAT(@name, ': expected ',@expected,', got ',@actual);
            THROW 51021,@failure,1;
        END;
        SET @passed+=1;
        FETCH NEXT FROM checks INTO @name, @sql, @expected;
    END;
    CLOSE checks;
    DEALLOCATE checks;
    UPDATE forum.PassPostInfos SET SaleStatus='SOLD' WHERE PostId=@pass;
    UPDATE forum.Posts SET VisibilityStatus='HIDDEN' WHERE Id=@pass;
    IF NOT EXISTS(SELECT 1 FROM forum.Posts p JOIN forum.PassPostInfos i ON i.PostId=p.Id
        WHERE p.Id=@pass AND p.VisibilityStatus='HIDDEN' AND i.SaleStatus='SOLD')
        THROW 51022,'Sale and visibility must remain independent.',1;
    ROLLBACK TRANSACTION;
    PRINT CONCAT('PASS: valid fixtures, ',@passed,' rejected invalid writes, independent sale/visibility, all fixtures rolled back.');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
