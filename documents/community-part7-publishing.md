# Triển khai API cộng đồng B3 và B4

API chính: `/api/community`. FE chưa chuyển đổi, thuộc B5.

## B3

- Tạo/sửa PASS và OTHER bằng multipart. Trường `payload` chứa JSON theo
  `community-part7-contract.md`; mỗi file part có tên bằng `uploadKey`.
  Không nhận URL, public ID, AuthorId hay trạng thái do client tự chèn vào JSON.
- OTHER có thể chỉ có chữ. PASS cần ít nhất một ảnh, hỗ trợ thêm video.
- CasualUser đăng PASS/OTHER; Shopper chỉ OTHER. Shopper chỉ gắn shop của mình
  và sản phẩm công khai thuộc shop đó. CasualUser có thể gắn shop/sản phẩm
  công khai bất kỳ. Sản phẩm tự xác định StoreId; ID shop mâu thuẫn bị từ chối.
- PASS có sản phẩm riêng, mọi media của PASS đều không được có liên kết shop
  hoặc sản phẩm catalog. Giữ Title và ProductName riêng.
- Kiểm tra số đo theo cấu trúc ProductMeasurement, tag theo danh sách FE hiện tại,
  giá, size, độ mới, khuyết điểm và giới hạn text.
- Unique ClientRequestId/tác giả và SHA-256 của payload/file cho retry tạo bài.
  Cùng mã và nội dung trả bài cũ; khác nội dung trả 409. Có khóa theo tác giả
  để đồng thời không vượt giới hạn 10 PASS/24 giờ.
- RowVersion chống ghi đè khi sửa. Không đổi loại bài; media giữ lại phải thuộc
  chính bài đó và đúng loại. Đổi thứ tự qua vị trí tạm trong transaction.
- SaleStatus AVAILABLE/SOLD tách VisibilityStatus ACTIVE/HIDDEN. Response có
  `saleStatus` ở cấp bài, `passInfo` chứa các thuộc tính mô tả sản phẩm.
- Tác giả có thể đọc bài ẩn của mình để quản lý; người khác nhận 404.

## Media

Tối đa 10 media/bài, trong đó tối đa 2 video. Ảnh JPG/PNG/WebP <=5 MiB;
video MP4 <=50 MiB, tối đa 60 giây, H.264 và AAC nếu có âm thanh. Tổng file
<=140 MiB. Endpoint cho request <=150 MiB để đủ multipart overhead; môi trường
IIS/reverse proxy cần cấu hình giới hạn tương ứng.

Validation dùng cả MIME/extension và signature file. Cloudinary phải giải mã
được ảnh/video; kiểm tra kích thước, codec và thời lượng từ kết quả upload.
Thumbnail video là Cloudinary transformation frame đầu, không nhận từ client.

CommunityMediaCleanup lưu public ID trước khi upload, thời hạn dự phòng 24 giờ.
Khi transaction gắn media thành công, xóa mục cleanup trong cùng transaction.
Upload thất bại được đưa lịch xóa về hiện tại; gỡ media khỏi bài tạo công việc
xóa trong cùng transaction. Worker chạy mỗi phút, xử lý tối đa 20 mục/lượt,
kiểm tra media không còn được tham chiếu rồi mới xóa. Lỗi được retry với backoff.
Nếu tiến trình dừng trong upload, record vẫn tồn tại để worker dọn sau thời hạn.

Storage dùng biến cấu hình Cloudinary hiện có; không thêm credentials vào Git.
Không lưu public ID trong response API.

## B4

- Feed newest, by-style (PASS), near-you (OTHER gắn shop) và storeId.
- Nearby dùng SQL Server tính khoảng cách hình cầu đến shop công khai gần nhất;
  gộp theo PostId trước count/phân trang, không nhân đôi bài gắn nhiều shop.
- Shop thiếu tọa độ không tham gia nearby. Shop không active/KYC chưa duyệt,
  chủ không còn Shopper hoặc sản phẩm không công khai không được tính.
- Liên kết hết công khai được trả `attachmentUnavailable=true`, store/product
  null. Ảnh/video và nội dung bài vẫn tồn tại. Giá/trạng thái sản phẩm còn công
  khai lấy dữ liệu hiện tại; sản phẩm SOLD không bị coi là đã mất liên kết.
- Like/unlike idempotent; bình luận hỗ trợ root/reply và mã chống gửi trùng.
  Bình luận xóa mềm giữ vị trí để reply có ngữ cảnh. Ẩn nội dung của tài khoản Banned.
- Báo cáo chống trùng PENDING. Admin xem hàng đợi, chi tiết đủ ảnh/video và xử lý
  DISMISS/HIDE_POST. HiddenByAdmin ngăn tác giả tự sửa/mở bài đã bị kiểm duyệt.
  Admin có thể mở lại qua visibility PATCH có version.
- Quyền admin được kiểm tra lại từ DB, kể cả JWT đang mang claim Admin cũ.
- Các mutation bài/tương tác/kiểm duyệt dùng chung khóa theo PostId.
- Xóa sản phẩm đang được nhắc trong cộng đồng trả 409, hướng dẫn ẩn sản phẩm,
  thay vì để lỗi khóa ngoại 500 hoặc mất liên kết.

## Route cũ và chuyển FE

- `/api/forum/posts/{type}/{id}/...` cho tương tác và `/api/forum/moderation/...`
  đã dùng schema mới; SPOT chỉ là alias OTHER trên route tương tác chuyển tiếp.
- `/api/admin/reports` và resolve đã được mở lại với service mới.
- Các API tạo/đọc/feed PASS/SPOT cũ và `/api/stores/{id}/spot-posts` vẫn được chặn
  bởi LegacyCommunityUnavailable (503). Service cũ không được gọi vào bảng đã bỏ.
  B5 phải chuyển FE sang `/api/community`, không chỉ đổi tên SPOT thành OTHER.
- Không có tương tác riêng từng media, nhập tọa độ shop ngoài hệ thống, giá cọc
  hoặc thanh toán PASS trong đợt này.

## Migration và vận hành

Migration bổ sung `20261027111239_SupportCommunityPublishing` thêm RequestHash,
HiddenByAdmin và bảng CommunityMediaCleanup. Không sửa lịch sử migration B1/B2.
Down chặn nếu có bài hoặc công việc cleanup để không mất trạng thái kiểm duyệt.

Chưa chạy migration vào DB ứng dụng trong lần triển khai này. Program hiện tự
migrate khi khởi động, vì vậy lần chạy BE mới sẽ áp dụng migration vào DB cấu
hình. Không chạy bản API cũ trên schema mới.

```powershell
dotnet build Doosii.sln --no-restore -p:OutputPath=D:/Summer26/EXE101/CheckPoint3MVP/BE/.build-part7/
dotnet build Doosii.DAL --no-restore
dotnet ef migrations has-pending-model-changes --project Doosii.DAL --startup-project Doosii.DAL --no-build
dotnet restore tests/Doosii.CommunityChecks
dotnet build tests/Doosii.CommunityChecks --no-restore -p:OutputPath=D:/Summer26/EXE101/CheckPoint3MVP/BE/.build-part7/checks/
dotnet .build-part7/checks/Doosii.CommunityChecks.dll
```

Bộ kiểm thử console chạy HTTP với JWT, controller/model binding, EF và SQL
Server thật. Nó tạo DB tên DoosiiCommunityTest_GUID bằng Windows authentication
vào localhost và tự xóa chính DB đó trong finally. Không khởi động Program của
ứng dụng, không seed admin thật hoặc chạy các background worker khác.
Media storage dùng fake để không upload/xóa tài nguyên ngoài. Cần quyền tạo
database thử nghiệm; tuyệt đối không thay connection của test thành DB ứng dụng.

Các ca kiểm thử gồm tạo chữ/ảnh/video, retry và tạo đồng thời, phân quyền khi
tạo/sửa, gắn sản phẩm suy ra shop, nearby không trùng, style/store feed, reorder,
stale version, media của bài khác, like/comment retry, reply sai bài/quá cấp,
xóa mềm, sale status, kiểm duyệt, ẩn bài khỏi các API công khai, role Banned,
JWT Admin không khớp DB, upload thất bại, cleanup retry và quota 24 giờ.

Kết quả xác minh ngày 27/10/2026: 86 lượt gọi HTTP và kiểm tra hành vi đạt;
build bộ kiểm thử và solution thành công. EF xác nhận model khớp migration.
Database thử được dọn sau mỗi lần chạy. Cảnh báo “Simulated delete failure”
trong test là lỗi được chủ động tạo để xác minh cleanup vẫn được lưu và retry.

Upload Cloudinary thật chưa được kiểm chứng tự động trong môi trường này;
trước phát hành cần smoke test ảnh/video bằng cấu hình Cloudinary của môi trường.

## Cập nhật B5/B6 (27/10/2026)

FE đã chuyển sang API `/api/community`, form PASS/OTHER hỗ trợ media, gallery,
quyền CasualUser/Shopper, edit/version, visibility/sale và moderation.
Chi tiết bàn giao: FE `docs/community-part7-integration.md`.
131 test FE, lint, build, SSR và smoke dùng mã Axios thật đều đạt.
Đã kiểm tra UI với host `Doosii.CommunityChecks --ui` (SQL DB riêng dùng một lần),
bao gồm Shopper ownership, reorder nhiều media, nearby, report/admin hide.
Chế độ UI chỉ mở login/media giả trong project test, không sửa Program API thật.
Host ở 127.0.0.1:5257; tạo `.build-part7/community-ui.stop` từ BE để thoát và dọn DB.
Giữ giới hạn: DB ứng dụng chưa migrate, chưa deploy và chưa smoke Cloudinary thật.
