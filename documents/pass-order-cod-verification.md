# Đặt hàng PASS qua COD

## Hành vi

- Từ chi tiết bài PASS công khai, người mua CasualUser/Shopper có thể mở trang đặt COD; chủ bài không thể mua món của mình. Món đã bán, bị ẩn, chủ không còn CasualUser hoặc đang có khóa đơn không thể đặt.
- `GET /api/Orders/pass/{postId}/quote` báo giá từ server. `POST /api/Orders/pass/cod` chỉ nhận post ID, version bài, clientRequestId và thông tin nhận; không nhận giá hoặc seller từ client. Chưa hỗ trợ PayOS cho PASS.
- Người mua được khóa theo tài khoản và bài được khóa bằng cùng `community:post:{id}` application lock với thao tác cộng đồng. Order, snapshot và `PassOrderLocks` được tạo trong một transaction. Lần gửi lại cùng clientRequestId và cùng nội dung trả cùng đơn; thay đổi nội dung bị từ chối.
- Bài PASS được đánh dấu đang có đơn qua `isPassReserved` (không trả ID đơn/người mua). Chủ bài không thể sửa, tự ẩn hoặc đổi trạng thái bán khi món đang được giữ. Quản trị viên vẫn có thể ẩn bài để kiểm duyệt; khóa đơn không tự xóa khi kiểm duyệt.
- Người mua hoặc người bán hủy COD trước khi gửi; hệ thống xóa đúng khóa thuộc đơn và món còn AVAILABLE. Người bán xác nhận vận chuyển; sau đó xác nhận giao thành công và thu đủ tiền thì đơn COD_COMPLETED, món SOLD và khóa bị xóa. Đơn COD không tạo escrow.
- Trang đơn hiển thị người bán, dẫn tới bài PASS, không hiện đánh giá shop cho đơn chỉ có PASS. Người bán là CasualUser vẫn dùng tab đơn bán.

## Giới hạn

- COD_PENDING chưa có thời hạn tự hủy: khóa giữ đến khi đơn được hủy hoặc hoàn tất, theo hành vi COD hiện tại. Không áp 15 phút của PayOS cho COD. Cần chốt chính sách xử lý đơn không được gửi/bom hàng/trả hàng riêng.
- Chưa có thanh toán PayOS cho PASS. API tạo PASS COD không nhận phương thức PAYOS và UI chỉ hiển thị COD. Chưa có luồng hoàn tiền hay khiếu nại COD.
- Khi quản trị ẩn bài có đơn đang xử lý, đơn vẫn tồn tại; chưa có quy trình tự hủy hay quyết định xử lý sau kiểm duyệt. Đơn chưa gửi có thể hủy theo quyền hiện tại.
- Chưa áp migration vào DB ứng dụng; API thật tự migrate khi khởi động, cần kiểm tra connection và backup dữ liệu trước khi chạy bản mới. Chưa kiểm tra bằng giao dịch thật hoặc trình duyệt với tài khoản thật.

## Kiểm chứng

- `tests/Doosii.PickupChecks`: 219 HTTP/JWT/SQL và hồi quy, gồm báo giá PASS, hai người đặt đồng thời chỉ có một đơn, retry, quyền xem, ship/collect, hủy trước/sau gửi và khóa. SQL 51011 trong log là ca thử guard rollback.
- `tests/Doosii.CommunityChecks`: 92 HTTP/SQL assertions, gồm trạng thái giữ công khai và chặn chủ bài đổi bán/ẩn khi đang khóa. Lỗi fake storage trong log là ca kiểm tra retry.
- `tests/Doosii.OrderSourceChecks`: migration và service SQL, gồm COD PASS báo giá, đặt đồng thời, hủy, đặt lại, retry, gửi/thu tiền và không tạo escrow; DB test tự xóa.
- FE: 141 unit tests, lint và build thành công; build có cảnh báo kích thước bundle. Chưa test thao tác trên trình duyệt.
