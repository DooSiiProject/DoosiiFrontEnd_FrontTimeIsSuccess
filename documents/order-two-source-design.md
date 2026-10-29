# Thiết kế dữ liệu đơn hàng: sản phẩm shop và bài PASS

## Hiện trạng

Đơn hiện tại chỉ được tạo từ `store.Products`; `OrderItems.ProductId` là `int` bắt buộc. Bài PASS dùng `forum.Posts` và `forum.PassPostInfos`, không phải product. `OrderItems` đã lưu snapshot tên, giá, ảnh; `Orders` lưu buyer và seller. Các thao tác khóa, hủy, bán và kiểm tra đánh giá hiện đều dựa vào ProductId.

## Đề xuất schema

- `OrderItems.SourceType`: `SHOP_PRODUCT` hoặc `PASS_POST`; `ProductId` nullable; `PassPostId` nullable. Check constraint buộc đúng một nguồn tương ứng với discriminator.
- Khóa ngoại PASS trỏ `PassPostInfos.PostId`, dùng RESTRICT; kiểm tra chính sách FK product và dữ liệu cũ trước khi thay đổi. Snapshot tên, giá, ảnh vẫn bắt buộc/giữ nguyên như hiện tại.
- Bổ sung snapshot `StoreId` và `StoreName` cho đơn shop mới, còn đơn PASS không có shop. SellerId giữ trên đơn. Tên shop là tên lúc đặt; đơn cũ backfill tên hiện tại và phải ghi rõ đây là giá trị khôi phục, không khẳng định là tên lịch sử.
- Migration Up: kiểm tra các dòng cũ trỏ product/shop hợp lệ, backfill và xác minh trước khi thêm ràng buộc. Nếu có đơn không thể xác định shop, không tự gán sai: cần quyết định chính sách dữ liệu trước khi chạy.
- Migration Down: chặn rollback khi có dòng PASS, vì schema cũ không thể biểu diễn chúng; không tự xóa dữ liệu.

## Việc cần thực hiện trước khi phát hành migration

1. Rà soát toàn bộ code dùng `OrderItem.ProductId`, bao gồm khóa sản phẩm, checkout, thanh toán, hủy, hoàn tất, tranh chấp, báo cáo, đánh giá và DTO; đảm bảo API shop hiện tại không thay đổi hành vi.
2. Quyết định cách biểu diễn snapshot shop ở đơn cũ không thể backfill và nguồn tên shop sau khi chủ shop thay đổi. Không suy ra tên lịch sử từ tên hiện tại.
3. Tạo migration EF đồng bộ model snapshot; kiểm tra upgrade DB có đơn cũ, FK/check constraint, rollback guard và model snapshot trên SQL Server tạm.
4. Chỉ mở tạo đơn PASS sau khi có khóa PASS và toàn bộ lifecycle theo nguồn; schema hai nguồn không đồng nghĩa luồng đặt PASS đã hoạt động.

## Quyết định mới: đặt PASS bằng COD và PayOS

Người dùng đã chọn hỗ trợ cả COD và PayOS cho đơn PASS giao hàng. Cần triển khai đồng thời hai nhánh thanh toán, không coi việc tạo đơn COD thành công là xác nhận PayOS hoạt động.

- Báo giá dựa vào `PassPostInfo` và phiên bản bài `Post`, seller là `AuthorId`; chỉ mua bài PASS công khai, chưa bán, chủ bài đủ điều kiện và không phải chính người mua.
- Khóa theo `PostId` trong cùng transaction tạo đơn; bảo đảm chỉ một đơn còn hiệu lực sở hữu món. Các thao tác chỉnh sửa, ẩn và đổi trạng thái bán của bài phải dùng cùng quy tắc khóa, kể cả yêu cầu đồng thời.
- `OrderLifecycle`, `OrderService`, `PaymentService` hiện dùng ProductId trực tiếp: phải phân nhánh nguồn khi hết hạn, hủy, webhook, xác nhận giao nhận và giải phóng/bán món. Không cập nhật nhầm `Products` có ID trùng PostId.
- COD và PayOS cần kiểm tra idempotency, tranh chấp các thao tác đồng thời và webhook đến muộn. Quy tắc giải phóng tiền, hoàn tiền và quyền người pass phải được kiểm tra với các service hiện có trước khi bật luồng.
- Giữ tương thích đơn shop, bổ sung kiểm thử SQL về migration, ràng buộc, hai người mua cùng lúc, khóa và rollback; kiểm thử HTTP cả hai phương thức và các tình huống lỗi/cạnh tranh.

## Rà soát điểm tích hợp

- `OrderLifecycle.ExpireAsync` đang giải phóng trực tiếp product; `PaymentService.ProcessPayOsWebhookAsync` xác thực khóa bằng product. Hai đường này phải hiểu nguồn PASS trước khi nhận đơn PASS.
- `OrderService` đang tạo đơn, hủy, hoàn tất và xác nhận COD dựa trên ProductId; `StoreReviewService` join product để xác định shop. Nhánh PASS phải giữ nguyên điều kiện đánh giá chỉ cho đơn shop online.
- Sửa bài, thay đổi hiển thị và đánh dấu đã bán hiện dùng `CommunityAccess.LockAsync` với resource `community:post:{id}`. Tạo/giải phóng khóa đơn PASS phải phối hợp cùng khóa này, xác định thứ tự khóa với order để tránh deadlock và ngăn thao tác cộng đồng ghi đè trạng thái món đang được giữ.
- Các trạng thái `SaleStatus` hiện chỉ cho phép AVAILABLE/SOLD. Khóa đơn cần trạng thái hoặc pointer sở hữu riêng kèm ràng buộc DB; không dùng SOLD để biểu diễn giữ tạm.
