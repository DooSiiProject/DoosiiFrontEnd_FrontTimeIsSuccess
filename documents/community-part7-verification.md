# Kết quả xác minh B1 và B2

Ngày: 27/10/2026. Phạm vi: schema và hợp đồng API cộng đồng part 7.

## Đã kiểm chứng

- Build solution .NET: 0 warnings, 0 errors. API đang chạy giữ khóa thư mục
  output mặc định, nên build dùng `-p:OutputPath=.../BE/.build-part7/`.
  Không dừng hoặc khởi động lại API của người dùng.
- EF `has-pending-model-changes`: không có khác biệt giữa model và snapshot.
- Trên SQL Server cục bộ, tạo DB riêng `DoosiiPart7SchemaTest_20261027_01`;
  chạy toàn bộ lịch sử đến AddOrderPaymentMethod, sau đó UnifyCommunityPosts.
- Insert hợp lệ: PASS có thông tin sản phẩm, ảnh và video không gắn shop;
  OTHER chỉ có chữ, sau đó thêm media gắn sản phẩm đúng shop; bình luận,
  reply, reaction và report.
- 18 trường hợp ghi sai bị SQL Server từ chối đúng mã lỗi: gắn shop/sản phẩm
  vào PASS; giả loại media; sai shop của sản phẩm; product thiếu store;
  trùng vị trí media; giá âm; độ mới sai; khuyết điểm rỗng; JSON tag/số đo sai;
  OTHER có PASS info; reply khác bài; trùng like; trùng report PENDING;
  tương tác mồ côi; trộn trạng thái hiển thị và bán; sale status không hỗ trợ.
- SOLD và HIDDEN cùng tồn tại độc lập. Fixture được rollback sau kiểm tra.
- Down thành công khi schema trống, Up lại thành công, chạy lại 18 ca đều đạt.
- Up chặn schema cũ có dữ liệu bằng lỗi 51007; Down chặn schema mới có dữ liệu
  bằng lỗi 51008. Fixture guard nằm trong transaction và được rollback khi
  kết nối kiểm thử đóng.
- Đã xác minh database thử không còn fixture, ở đúng migration mới và dọn
  database thử sau khi hoàn tất. Không để lại database phụ trên SQL Server.

## Giới hạn có chủ đích

- Chưa áp dụng migration lên database ứng dụng. Runtime API hiện tự migrate
  khi khởi động; lần chạy build mới tiếp theo sẽ thử áp dụng migration mới.
- Endpoint cũ được gắn LegacyCommunityUnavailable, trả 503 với
  COMMUNITY_UPGRADE_PENDING thay vì truy vấn bảng đã bỏ. Chỉ các action report
  của AdminController bị chặn; các action admin khác giữ nguyên.
- Chưa triển khai API mới, kiểm tra role/ownership của service, upload video,
  feed mới hay FE. Đây là phạm vi B3–B5, không nằm trong B1/B2.
- Test ràng buộc DB không thay thế kiểm thử quyền ứng dụng ở B3/B4.
- LocalDB không khởi động được trong môi trường này; kiểm thử đã dùng SQL
  Server cục bộ với Windows authentication và database thử riêng.

## Lưu ý cho bước tiếp theo

Thay service/controller cũ theo hợp đồng, rồi bỏ gate cho từng luồng đã được
kiểm thử. Xóa các CLR model/DbSet legacy và PostType CLR của tương tác khi
không còn consumer. Không đưa chúng trở lại EF mapping. Không dùng hợp đồng
API mục tiêu như bằng chứng endpoint đã tồn tại.
