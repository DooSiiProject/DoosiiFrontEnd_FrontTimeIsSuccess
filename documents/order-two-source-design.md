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
