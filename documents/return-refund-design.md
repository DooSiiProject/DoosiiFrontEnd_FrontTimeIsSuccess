# Đề xuất kỹ thuật: trả hàng và hoàn tiền

Tài liệu này ghi lại thiết kế đang chuẩn bị, chưa xác nhận chính sách và chưa mô tả tính năng đã triển khai.

## Phạm vi

Áp dụng cho toàn bộ đơn shop và PASS đã giao; hỗ trợ COD và PayOS. Đợt đầu xử lý toàn bộ đơn, không tự động đổi hàng hoặc mở bán lại. Yêu cầu trả hàng là nghiệp vụ riêng với khiếu nại hiện tại, có thể chuyển Admin phân xử nếu hai bên bất đồng.

## Trạng thái đề xuất

Yêu cầu: `REQUESTED` → `APPROVED` → `RETURN_SHIPPED` → `RETURN_RECEIVED` → `REFUND_PENDING` → `REFUND_COMPLETED`. Người bán từ chối phải có lý do; người mua có thể yêu cầu Admin xử lý. Khi hai bên bất đồng hoặc hàng trả có vấn đề, trạng thái chuyển `ADMIN_REVIEW`; quyết định phải lưu tác giả, thời gian và lý do. Cần quy định rõ các nhánh từ chối, rút yêu cầu và quyết định của Admin trước khi áp dụng.

Chỉ ghi nhận hoàn tất sau khi có xác nhận tiền thực sự được hoàn theo quy trình đã chọn. Lưu bằng chứng, người xác nhận, số tiền và thời điểm; không lưu thông tin tài khoản ngân hàng nhạy cảm trong log. COD không giả định DooSii đang giữ tiền. PayOS cần xác minh kênh hoàn thực tế; trạng thái duyệt hoàn không phải xác nhận tiền đã chuyển.

## Dữ liệu và đồng thời

Tạo bảng yêu cầu riêng liên kết Order, có lý do, bằng chứng, trạng thái, thời hạn chính sách chụp tại lúc tạo, thông tin vận chuyển trả, thông tin nhận hàng, phương án phí, số tiền đề nghị/được duyệt/đã hoàn, phiên bản đồng thời và dấu thời gian. Thêm lịch sử sự kiện bất biến với người thực hiện. Một yêu cầu đang hoạt động tối đa cho mỗi đơn, bảo đảm bằng ràng buộc DB và giao dịch khóa đơn; các thao tác lặp phải an toàn và có kiểm soát phiên bản.

Chỉ người mua, người bán của đơn và Admin có quyền tương ứng; kiểm tra lại quyền trong service. Không tự mở lại tồn kho shop hoặc PASS: chỉ sau xác nhận nhận hàng và kiểm tra tình trạng, với hành động rõ ràng của người bán. Snapshot đơn đã tồn tại là căn cứ đối chiếu, đặc biệt với khuyết điểm PASS.

## Tương thích cần xử lý

- COD hiện chuyển thẳng `COD_COMPLETED` khi người bán xác nhận thu tiền và đánh dấu món đã bán; cần cho phép yêu cầu sau trạng thái này mà không giả định tiền nằm trong ví DooSii.
- PayOS hiện xác nhận nhận hàng sẽ giải ngân ký quỹ trong dữ liệu; ví khóa khả dụng theo thời hạn khiếu nại 24 giờ. Nếu thời hạn trả hàng mới dài hơn, phải thay đổi nhất quán điều kiện khả dụng của ví và xử lý các đơn cũ đã rút tiền; không khẳng định có thể giữ hay thu hồi tiền đã chi.
- `DeliveredAt` hiện được ghi trong bước hoàn tất COD và xác nhận hoàn tất PayOS; chưa thấy nguồn xác nhận giao hàng độc lập. Cần công bố nguồn thời điểm và xử lý đơn thiếu dữ liệu, không suy diễn từ thời gian gửi.
- Luồng phân xử hiện tại đổi thẳng trạng thái sang hoàn tiền và ký quỹ sang refunded, chưa chứng minh giao dịch hoàn thực tế; không tái sử dụng bước đó làm xác nhận chuyển tiền cho quy trình mới.
- Kiểm thử cạnh tranh giữa yêu cầu trả, hoàn tất đơn và rút tiền; các API chuyển trạng thái, quyền truy cập, retry, đơn PASS/shop và dữ liệu cũ.

## Chính sách đã xác nhận

- Người mua có 48 giờ sau khi nhận hàng để tạo yêu cầu; bên có lỗi chịu phí vận chuyển, Admin phân xử khi bất đồng.
- COD: người bán cung cấp bằng chứng hoàn tiền, người mua xác nhận đã nhận; Admin xử lý bất đồng.
- Phương thức hoàn PayOS thực tế cần đối soát trước khi triển khai chuyển tiền tự động; không giả định chức năng này đã tồn tại.

Migration hiện tạo một yêu cầu cho mỗi đơn; cần quyết định rõ cách mở lại yêu cầu bị từ chối trước khi hoàn thiện API. Chưa áp dụng migration hoặc kết nối các bước nghiệp vụ.
