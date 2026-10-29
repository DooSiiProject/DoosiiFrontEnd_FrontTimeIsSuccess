# B4 — nhận tại shop, mã nhận, worker và thông báo

Ngày: 28/10/2026. Tiếp nối B3; chưa mở giao diện `/holds` (B5).

## API đã thêm

### GET /api/pickup-reservations/{id}/pickup-code

Chỉ Buyer hiện tại được xem mã khi ACCEPTED còn hạn, shop/sản phẩm còn đủ điều kiện
và product vẫn thuộc đúng lượt giữ. Shop không được xem mã của khách; người ngoài
không có quyền xem record trả 404. Response `Cache-Control: no-store`.

```json
{
  "success": true,
  "data": {
    "reservationId": 102,
    "pickupCode": "012345",
    "expiresAt": "2026-10-28T04:30:00+00:00",
    "serverTime": "2026-10-28T04:00:00+00:00"
  }
}
```

Mã ví dụ là dữ liệu tổng hợp. List/detail/notification không trả mã, hash hoặc ciphertext.
Xem lại mã không reset bộ đếm, không đổi version và không gia hạn giữ đồ.

### POST /api/pickup-reservations/{id}/complete

```json
{
  "version": "AAAAAAAAB9E=",
  "pickupCode": "012345",
  "confirmedPaymentReceived": true
}
```

Chỉ đúng Shopper còn sở hữu shop được xác nhận. Bắt buộc đã giao món và nhận đủ tiền
tại quầy; backend ghi nhận xác nhận của shop, không kết nối máy POS để chứng minh tiền mặt.
Kiểm tra eligibility, version, vị trí hiện tại so với snapshot, hạn giữ và quyền sở hữu
khóa dưới Product UPDLOCK/HOLDLOCK. Không cho hoàn tất nếu shop đổi vị trí sau khi duyệt;
trả STORE_LOCATION_CHANGED để hủy/tạo lại theo địa điểm mới.

Mã đúng: trong cùng transaction, reservation → COMPLETED, ClosedById là shop,
CloseReason PAYMENT_RECEIVED_AT_STORE; product → SOLD và clear pointer pickup;
ghi thông báo cho hai bên. Không tạo đơn COD, escrow, payout hoặc cộng tiền vào ví.
Retry với cùng mã/actor sau thành công trả kết quả cũ, kể cả version cũ, không ghi
thông báo hoặc thu tiền thêm. Trạng thái kết thúc khác không được chuyển sang COMPLETED.

## Kiểm soát nhập mã

- 6 chữ số ngẫu nhiên từ cryptographic RNG; HMAC-SHA256 gắn ReservationId và pepper
  ngoài DB. Payload mã hóa bằng Data Protection cũng gắn ReservationId để chống tráo
  ciphertext sang lượt khác. So sánh HMAC constant-time.
- Trước khi tính một lần sai, kiểm tra ciphertext/HMAC đang đọc được bằng cấu hình
  hiện tại. Mất key, pepper sai/thiếu hoặc dữ liệu bị tráo trả 503
  PICKUP_CODE_UNAVAILABLE, không tăng bộ đếm và không đổi trạng thái bán.
- Mỗi mã sai đúng định dạng/version tăng FailedCodeAttempts trong transaction.
  4 lần đầu trả 400 INVALID_PICKUP_CODE; lần thứ 5 và các lần trong cửa sổ khóa trả
  429 PICKUP_CODE_LOCKED. Header Retry-After là số giây còn lại.
- Sau 15 phút, bắt đầu lại cửa sổ đếm; không kéo dài ExpiresAt. Hết hạn giữ luôn ưu tiên
  trước kiểm tra mã hoặc khóa thử. Một request stale version không được thử mã.
- Lỗi mã sai/khóa trả `data: {version, retryAfterSeconds}` để B5 cập nhật version
  mà không phải đoán; GET detail cũng trả version mới. Không gửi mã vào URL hoặc log.
- Backend không bật HTTP request-body logging/sensitive SQL logging cho luồng này.
  Reverse proxy/observability của môi trường cũng phải giữ body complete và response
  pickup-code khỏi log/cache khi vận hành.

## Worker và thông báo

`PickupMaintenanceWorker` chạy ngay khi host bắt đầu rồi mỗi 15 giây, mỗi lượt tối đa
100 product có deadline quá hạn hoặc eligibility không còn hợp lệ.

- Dùng cùng product lock như API: PENDING quá hạn → EXPIRED/RESPONSE_TIMEOUT;
  ACCEPTED quá hạn → EXPIRED/PICKUP_TIMEOUT; chỉ trả AVAILABLE khi đúng pickup pointer.
- Khách bị Banned/đổi role không hỗ trợ, shop inactive/KYC không còn APPROVED, đổi chủ
  hoặc product ngừng công khai → CANCELLED/ELIGIBILITY_LOST. ClosedById ghi seller gốc
  như actor hệ thống quy ước trong hợp đồng. Không phạt hoặc trừ điểm tự động.
- Mỗi product có transaction riêng; lỗi một món được retry, không bỏ cả backlog.
  Detail/list/checkout vẫn lazy-expire, nên tính đúng không phụ thuộc tick worker.
- Tạo/duyệt/từ chối/hủy/hết hạn/hoàn tất ghi Notification và outbox cùng transaction.
  Khi một lượt được duyệt, các lượt chờ khác được hủy cũng có thông báo riêng.
- Relocation APPROVE ghi thêm cảnh báo cho các khách đang có ACCEPTED cùng shop
  trong transaction áp dụng địa chỉ; retry cùng review không nhân đôi cảnh báo.
- TargetUrl `/holds?reservationId={id}` tương thích bộ lọc URL thông báo FE hiện có;
  B5 sẽ mở màn chi tiết theo tham số này. Hiện route vẫn là thông báo dịch vụ chưa mở.

`PickupNotificationDelivery` nhận việc qua outbox row lock, gửi SignalR vào group
`user_{UserId}` bằng ReceiveNotification rồi mới xóa outbox. Thông báo trong bảng
Notifications vẫn giữ cho REST/read history. Lỗi gửi giữ outbox và retry có backoff
10 giây..5 phút; không biến một giao dịch đã commit thành lỗi HTTP.

Realtime là at-least-once: nếu tiến trình chết sau send nhưng trước khi ghi nhận,
client có thể nhận lại cùng Notification.Id và phải upsert theo ID. Không coi push
là chứng từ thanh toán. REST là nguồn dữ liệu bền vững khi reconnect.

Migration `20261028022627_AddPickupNotificationOutbox` chỉ thêm bảng notification
outbox với FK, unique EventKey/NotificationId và index lịch gửi. Down chặn bằng SQL
51011 nếu còn outbox chờ, tránh bỏ mất thông báo. Notification/history không bị xóa.

## Cấu hình key ring

Không thêm secret vào appsettings hoặc Git. Cấu hình bằng user-secrets/environment:

| Cấu hình | Nội dung |
|---|---|
| Pickup__CodePepper | Base64 ít nhất 32 byte ngẫu nhiên, giữ ổn định giữa các lần chạy |
| Pickup__DataProtectionKeyPath | Đường dẫn tuyệt đối tới key ring bền vững, ngoài repository/public web root |
| Pickup__DataProtectionCertificateThumbprint | Certificate có private key để bảo vệ key ring; dùng khi chạy nhiều máy hoặc Linux |

ApplicationName luôn `DooSii`, purpose `DooSii.Pickup.Code.v1` giữ tương thích B3.
Windows không chỉ định certificate dùng DPAPI của tài khoản chạy service. Nếu đổi
tài khoản/máy, DPAPI key cũ không tự đọc được: dùng certificate/key ring chia sẻ khi
chạy nhiều instance. Linux/host không phải Windows cần certificate có private key
và quyền đọc cho service; không ghi key ring plaintext trong Production.

Ngoài Development, nếu đã cấu hình pepper mà thiếu persistent path (hoặc thiếu
certificate trên Linux), Program từ chối startup. Development có thể dùng nơi lưu
mặc định của Data Protection; fixture dùng provider ephemeral tổng hợp riêng.

Nếu từng duyệt lượt giữ bằng B3/default key ring, chuyển sang đường dẫn mới phải giữ
các key cũ và đúng tài khoản/certificate bảo vệ. Không xóa key cũ, đổi pepper đột ngột
hoặc tự tạo lại mã để chữa lỗi: các lượt ACCEPTED hiện tại cần key/pepper cũ để nhận.
Backup key ring/pepper theo quy trình vận hành. Automatic key rotation giữ key cũ;
rotation pepper/versioning nhiều pepper chưa được triển khai trong đợt này.

## Xác minh và giới hạn

Bộ `tests/Doosii.PickupChecks` chạy HTTP/JWT/controllers/EF/SQL Server thật trong DB
riêng, bao gồm B3 regression; dùng clock điều khiển và fake SignalR transport để mô
phỏng lỗi push. Có kiểm tra key ring trên filesystem thử, khởi tạo lại provider đọc
được mã cũ (Windows bảo vệ DPAPI); thư mục key thử và DB được dọn sau kiểm tra.

Các nhóm B4: quyền xem mã/no-store; bắt buộc xác nhận thu tiền; stale version; 5 lần
sai và khóa 15 phút; reset đúng mốc; retry/parallel complete và complete-vs-cancel;
chặn đổi vị trí; không tạo order/escrow; tráo ciphertext, sai pepper/mất key không
phạt khách; worker deadline/Banned/inactive/idempotency; relocation notification;
outbox retry/concurrent publisher/không mất REST notification; rollback guard và Up/Down.

Kết quả: **198 kiểm tra HTTP/SQL/hành vi đạt**, gồm hồi quy B3. Build solution và
bộ kiểm tra thành công, 0 warnings/0 errors. EF model khớp snapshot. Log SQL 51011
khi thử Down với outbox còn dữ liệu là ca cố ý kiểm tra guard, không phải lỗi phát hành.
SQL review migration lưu tại `pickup-reservation-outbox-migration.sql`.

```powershell
dotnet build tests/Doosii.PickupChecks --no-restore -p:OutputPath=D:/Summer26/EXE101/CheckPoint3MVP/BE/.build-part7/pickup-http/
dotnet .build-part7/pickup-http/Doosii.PickupChecks.dll
```

Chờ build hoàn tất mới chạy console, và chờ console thoát trước khi build lại output.
DB là DoosiiPickupHttpTest_GUID mới trên localhost; không sửa test connection thành
DB ứng dụng. Không khởi động Program, seeders hay các worker ứng dụng thật trong test.

Chưa migrate DB ứng dụng, restart API, commit/push hoặc deploy. Chưa kiểm tra browser
pickup/SignalR trên môi trường thật; B5/B6 sẽ nối FE và E2E. Program vẫn tự migrate
khi khởi động backend mới, cần kiểm tra connection/key configuration trước khi chạy.
