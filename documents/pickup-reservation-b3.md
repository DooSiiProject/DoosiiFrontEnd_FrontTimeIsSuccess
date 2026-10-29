# B3 — API giữ đồ và khóa sản phẩm

Ngày xác minh: 28/10/2026. Bám theo `pickup-reservation-contract.md`.
Đây là mốc bàn giao B3; B4 đã bổ sung nhận hàng/worker và cập nhật yêu cầu key ring
trong `pickup-reservation-b4.md`.

## Đã triển khai

`PickupReservationsController` + `IPickupReservationService`/`PickupReservationService`:

| Endpoint | Quyền và hành vi |
|---|---|
| POST /api/pickup-reservations | CasualUser/Shopper giữ sản phẩm công khai của shop khác; 201 lần đầu, 200 retry |
| GET /api/pickup-reservations/mine | Lịch sử của người mua, filter/status/page/pageSize |
| GET /api/pickup-reservations/shop | Lịch sử shop hiện thuộc caller, chỉ Shopper |
| GET /api/pickup-reservations/{id} | Buyer hoặc đúng shop hiện tại; người ngoài 404 |
| POST /api/pickup-reservations/{id}/accept | Shop duyệt PENDING, khóa món, hủy các yêu cầu chờ khác cùng món |
| POST /api/pickup-reservations/{id}/reject | Shop từ chối PENDING, cần lý do/version |
| POST /api/pickup-reservations/{id}/cancel | Buyer/shop hủy PENDING/ACCEPTED, cần lý do/version |

Quyền lấy lại từ DB, không tin role claim JWT cũ. Admin/Banned không nhập vai buyer/shop.
Lookup không thuộc caller 404. Snapshot giá/tên/version lấy từ Product, không nhận
quyền, giá hoặc trạng thái từ body. JSON có trường lạ bị 400; lịch phải có offset/Z.
Contact và code material không public; ordinary DTO chỉ trả contact cho hai bên, không
trả mã, hash hay ciphertext. Response bật no-store.

Tạo mới kiểm tra lịch 30 phút..24 giờ, quota 3 lượt còn hiệu lực, duplicate buyer/product,
shop active/KYC APPROVED/chủ Shopper và product version. PENDING chưa khóa món.
Hash canonical payload chuẩn hóa contact/note và lịch UTC để phát hiện retry đổi nội dung.
Mã tạo trùng được xử lý trước khi kiểm tra lịch hiện tại để retry sau đó trả đúng record.

Duyệt lấy product UPDLOCK/HOLDLOCK, kiểm tra lại chủ shop/người mua/eligibility/version,
chụp vị trí shop và tạo mã nhận thật được bảo vệ. ACCEPTED + pointer khóa product + hủy
các PENDING khác ghi chung một transaction. Retry accept không tạo lại mã. Cancel/reject
retry phải cùng actor/lý do; version cũ không ghi đè thay đổi mới.

## Khóa chung với đơn online

- Create dùng application lock theo BuyerId để serialize retry/quota giữa các món.
  Các mutation trên cùng món dùng Product UPDLOCK, không khóa order từ pickup.
- Cleanup trước thao tác có transaction riêng; detail/list cũng expire lượt đã tới hạn.
  Hết hạn đang chờ duyệt ghi RESPONSE_TIMEOUT; giữ đã duyệt ghi PICKUP_TIMEOUT.
- Checkout quote/create cũng xét pickup, lazy-expire dưới product lock, chỉ được mua
  khi AVAILABLE và cả hai pointer rỗng. Báo giá nay dùng transaction để cleanup an toàn.
  Create gặp quote cũ phải tải lại version, không tự chấp nhận giá mới.
- Unlock/mark-sold online cần đúng ReservedOrderId và không có pointer pickup.
  Cancel/expiry đơn online không đụng khóa pickup. Pickup cancel chỉ giải phóng pointer
  đang thuộc chính lượt đó, tránh mở món đã chuyển sang người khác.
- ProductService chặn sửa/ẩn/xóa món đang giữ, trả 409 thay vì lỗi FK khi xóa sản phẩm
  có lịch sử pickup. ProductGroupService cấm UPDATE/DETACH món khóa; KEEP vẫn cho phép.
  Callback thanh toán online cũng kiểm tra không có chủ khóa pickup.
- StoreLocationChanged so sánh snapshot với vị trí hiện tại; snapshot không tự đổi.
  Thông báo relocation và chặn hoàn tất khi đổi địa điểm sẽ được nối trong B4.

## Cấu hình trước khi dùng accept

B2 yêu cầu acceptance có code material hợp lệ; vì vậy B3 đã tạo 6 chữ số ngẫu nhiên,
HMAC-SHA256 trên `ReservationId:code`, dùng pepper ngoài DB và mã hóa cùng payload
bằng ASP.NET Data Protection, purpose `DooSii.Pickup.Code.v1`, ApplicationName `DooSii`.
Chưa có API xem/nhập/xác nhận mã; đó là B4.

Đặt `Pickup:CodePepper` bằng user-secrets hoặc biến môi trường `Pickup__CodePepper`:
Base64 của ít nhất 32 byte ngẫu nhiên. Không đặt secret thật vào appsettings trong Git.
Giữ pepper ổn định; thay pepper khi có lượt ACCEPTED cần kế hoạch rotation ở B4.
Thiếu/sai cấu hình: accept trả **503 PICKUP_CODE_UNAVAILABLE**, sản phẩm và các yêu cầu
khác không bị đổi một phần. Create/list/cancel/reject vẫn hoạt động.

Data Protection dùng nơi lưu key mặc định theo host. Khi đưa lên máy chủ/container,
phải cấu hình key ring bền vững/chia sẻ giữa các instance và quyền đọc key; không dùng
ephemeral key trong ứng dụng thật. B4 cần hoàn tất cấu hình, xem mã riêng tư, constant-time
verification, 5 lần sai/khóa 15 phút, xác nhận nhận hàng và worker trước khi mở tính năng FE.

## Kiểm thử và vận hành

`tests/Doosii.PickupChecks` chạy actual HTTP/JWT/controllers/EF/SQL Server trong DB
`DoosiiPickupHttpTest_GUID` mới; không chạy Program, seeders, workers hoặc dịch vụ ngoài.
Key JWT, pepper và Data Protection provider chỉ dùng cho fixture, không phải tài khoản thật.

**107 kiểm tra đạt**, bao gồm:

- Guest/Admin/Banned và JWT role cũ; private detail/shop list, phân trang; JSON authority
  injection, lịch thiếu offset/quá sớm/quá xa, giữ món của mình.
- Retry/hash conflict, duplicate request, quota đồng thời giữa 4 món, retry đồng thời
  4 request, 2 khách được duyệt cùng món, và accept cạnh tranh checkout COD.
- PENDING không khóa; ACCEPTED chụp vị trí/giá, mã HMAC/ciphertext thật không lộ DTO;
  hủy các lượt chờ khác, stale version và retry accept/cancel/reject.
- Online không mua/mở khóa/đánh dấu bán món đang giữ; publication bị chặn; xóa history
  có thông báo 409. Group DTO không cho sửa/gỡ món khóa và service từ chối DETACH.
- Product thay đổi, buyer Banned, đổi vị trí shop; expiry đúng hạn/commit dù trả 409,
  detail mở khóa quá hạn, checkout quote tự mở khóa quá hạn, COD create/cancel hồi quy.
- Thiếu pepper fail closed và không khóa sản phẩm.

Build solution và bộ kiểm thử: 0 warnings, 0 errors. DB fixture được xóa trong finally
và có thông báo cleanup thành công. Không có migration mới ở B3.

```powershell
dotnet build tests/Doosii.PickupChecks -p:OutputPath=D:/Summer26/EXE101/CheckPoint3MVP/BE/.build-part7/pickup-http/
dotnet .build-part7/pickup-http/Doosii.PickupChecks.dll
```

Cần quyền tạo DB mới trên SQL Server localhost bằng Windows authentication. Không sửa
test connection thành DB ứng dụng. Chạy xong console mới build sang output khác vì
project references dùng chung MSBuild file list. Danh sách shop cleanup các product có
lượt quá hạn trước khi phân trang; B4 worker sẽ giảm backlog, cần tối ưu batch khi scale.

Chưa migrate DB ứng dụng, restart API, commit/push hoặc deploy. Program tự migrate khi
khởi động backend mới theo connection hiện có; phải kiểm tra DB mục tiêu trước đó.
FE `/holds` chưa mở lại. Worker/thông báo, pickup-code và complete thuộc B4; FE thuộc B5.
