# Migration nguồn hàng của OrderItem

Migration: `20261029162005_AddOrderItemSources`.

## Dữ liệu

- `SourceType`: SHOP_PRODUCT hoặc PASS_POST. `ProductId` chuyển nullable; `PassPostId` nullable. Check constraint buộc đúng một mã theo loại nguồn.
- FK ProductId → store.Products; FK PassPostId → forum.PassPostInfos. Cả hai RESTRICT khi xóa nguồn, không xóa lịch sử đơn theo sản phẩm/bài PASS.
- Snapshot hiện có (tên món, giá, ảnh) giữ nguyên. Thêm StoreIdSnapshot, StoreNameSnapshot, SellerNameSnapshot, SourceVersionSnapshot và SnapshotOrigin.
- Khi tạo đơn shop mới, service lưu tên shop/người bán, shop ID và version sản phẩm tại lúc đặt. SnapshotOrigin là AT_ORDER. API chi tiết, retry, danh sách mua/bán và các phản hồi thao tác dùng snapshot shop.
- Đơn cũ nhận LEGACY_BACKFILL khi xác định shop cùng chủ với seller của đơn; các tên là dữ liệu lúc migration, không khẳng định là tên tại thời điểm mua. Trường hợp không xác định chắc chắn shop dùng LEGACY_UNKNOWN và không tự gán shop.
- Snapshot shop không có FK để không thay đổi lịch sử khi shop đổi tên/chủ. Nguồn sản phẩm/bài PASS vẫn có FK riêng.

## Nâng cấp và rollback

Up kiểm tra ProductId cũ không bị mồ côi trước khi đổi schema, báo SQL 51020 nếu dữ liệu không hợp lệ; không tự xóa hoặc thay ID bằng 0. Cần sửa dữ liệu được báo lỗi trước khi chạy lại.

Down báo SQL 51021 nếu còn bất kỳ dòng PASS nào; không chuyển PASS thành product giả và không xóa đơn. Khi chỉ còn đơn shop, Down khôi phục ProductId NOT NULL, giữ tên/giá/ảnh gốc và các đơn hiện có; các cột snapshot mới bị bỏ theo schema cũ.

SQL review: `order-item-sources-migration.sql`. Chưa áp dụng DB ứng dụng và chưa khởi động lại Program. Program ứng dụng hiện auto-migrate khi startup; cần review migration và backup DB trước khi triển khai bản backend mới.

## Kiểm chứng

`tests/Doosii.OrderSourceChecks` tạo DB DoosiiOrderSourcesTest_GUID trên localhost, không đọc connection của ứng dụng, tự xóa DB sau chạy.

26 kiểm tra SQL/service đạt: upgrade schema cũ có đơn, giữ ID/tên/giá/ảnh, backfill và provenance, snapshot sau đổi tên, sai/both/neither nguồn, FK, snapshot shop thiếu cặp, dòng PASS hợp lệ, guard tránh xử lý PASS như product, rollback có PASS, orphan preflight, model khớp EF snapshot, tạo đơn shop mới, source version, DTO chi tiết/list/retry và hủy đơn.

```powershell
dotnet build tests/Doosii.OrderSourceChecks --no-restore -p:OutputPath=D:/Summer26/EXE101/CheckPoint3MVP/BE/.build-part7/order-source-checks/
dotnet .build-part7/order-source-checks/Doosii.OrderSourceChecks.dll
```

Backend API build đạt 0 warnings/0 errors. Các DB thử đã được dọn sạch.

## Giới hạn giai đoạn này

Đây là migration nền dữ liệu. API tạo đơn vẫn chỉ nhận sản phẩm shop. Chưa mở checkout, khóa món, COD/PayOS hay vòng đời PASS. Các đường xử lý product cũ dùng RequireShopProductId để chặn nhầm nguồn; không coi PostId trùng ProductId là cùng món. Trước khi bật PASS cần thay guard bằng xử lý nguồn phù hợp trong order/payment/expiry/admin dispute và đồng bộ khóa với community writes.
