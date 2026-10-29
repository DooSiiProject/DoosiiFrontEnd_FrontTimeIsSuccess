# Giữ đồ và nhận tại shop — B1/B2

Ngày: 27/10/2026. B1 chốt quy tắc; B2 thêm model, migration và DTO hợp đồng.
Bản cập nhật B4 ngày 28/10/2026 đã có toàn bộ endpoint trong hợp đồng, worker
và notification outbox. Xem `pickup-reservation-b3.md` và `pickup-reservation-b4.md`.
FE `/holds` vẫn hiện thông báo dừng cho đến B5. Chưa migrate DB ứng dụng.

## B1 — Quy tắc nghiệp vụ

1. Mỗi lượt giữ đúng một Product của shop công khai, active, chủ Shopper có KYC APPROVED.
   Người giữ là CasualUser hoặc Shopper hiện tại, không phải chủ shop của món đó.
   Không áp dụng bài PASS, sản phẩm local, bài gộp nguyên lô, hoặc tài khoản Banned/Admin.
2. Không thu cọc, phí giữ, phí ship hoặc phí ký quỹ. Giá snapshot là số tiền khách trả
   trực tiếp tại quầy khi nhận. Không tạo escrow, thu qua PayOS hay cộng vào ví DooSii.
   Không tạo Order COD giả: lịch sử pickup nằm riêng, chưa tính vào đánh giá đơn mua/
   analytics doanh thu đơn online. Tích hợp các chỉ số đó là phạm vi đợt sau.
3. Giờ đến phải từ serverNow + 30 phút đến serverNow + 24 giờ, tính cả hai đầu.
   Client gửi ISO 8601 có offset; server lưu UTC, giao diện hiển thị Asia/Saigon.
   OpeningHours hiện là text: shop chịu trách nhiệm chỉ duyệt lịch phù hợp giờ mở cửa.
4. ResponseDeadline = min(CreatedAt + 2 giờ, ScheduledAt). PENDING không khóa món,
   không hứa chắc còn hàng. Shop chỉ duyệt khi now < ResponseDeadline.
5. ACCEPTED khóa món ngay trong transaction chấp nhận. ExpiresAt = ScheduledAt + 30 phút;
   nhận hàng hợp lệ khi now < ExpiresAt, kể cả đến sớm nếu shop đồng ý giao tại quầy.
   Tại đúng hạn là hết hạn; không tự gia hạn do mất mạng hoặc nhập mã sai.
6. Tối đa 3 lượt PENDING + ACCEPTED chưa quá deadline mỗi khách, và một lượt còn hiệu lực
   của cùng khách trên cùng sản phẩm. Khách khác được gửi yêu cầu chờ duyệt cho cùng món.
   Khi một yêu cầu được duyệt, các PENDING khác cùng sản phẩm bị CANCELLED với lý do
   PRODUCT_RESERVED_ELSEWHERE và ClosedById là shop đang xác nhận. Không có hàng đợi tự nhận món.
7. Khách/shop được hủy PENDING/ACCEPTED; shop phải có lý do, khách cũng nhập lý do để
   lưu lịch sử. REJECTED chỉ từ PENDING và do đúng shop. Không sửa lịch/giá trên lượt giữ;
   muốn đổi thì hủy và tạo lại. Bản đầu chưa phạt khách không đến hoặc trừ điểm tự động.
8. Giá/tên/version sản phẩm chụp lúc yêu cầu; shop chỉ duyệt nếu product rowversion vẫn
   khớp. Nếu có thay đổi, trả PRODUCT_CHANGED để khách xem lại giá và tạo yêu cầu mới.
   Khi ACCEPTED, cấm sửa/ẩn/xóa món bằng các luồng quản lý sản phẩm hiện hành.
9. Chụp tên/địa chỉ/tọa độ shop từ DB lúc duyệt. Địa chỉ bắt buộc, tọa độ tùy chọn theo cặp.
   Không cho khách truyền địa chỉ pickup. Snapshot không tự đổi khi shop chuyển chỗ.
   Detail trả StoreLocationChanged khi địa chỉ/tọa độ hiện tại khác snapshot; thông báo
   khi relocation được áp dụng. Khách có thể hủy. Chặn nhận tại chỗ nếu vị trí đã đổi:
   hủy/tạo lại theo vị trí mới để tránh xác nhận nhầm địa điểm.
10. Mã nhận gồm 6 chữ số ngẫu nhiên an toàn, riêng mỗi lượt, tạo lúc ACCEPTED.
    Chỉ khách được xem mã; shop chỉ nhập mã khách đưa tại quầy. Shop phải xác nhận đã
    giao đúng món và nhận đủ PriceSnapshot trước khi hoàn tất. Sau 5 lần sai, khóa thử
    mã 15 phút, reset bộ đếm khi cửa sổ khóa hết. Khóa không kéo dài hạn giữ.
    So sánh hash constant-time, hash gắn ReservationId và pepper ngoài DB; mã để khách
    xem lại được mã hóa bằng ASP.NET Data Protection (key ring bền vững). Không log mã,
    không trả hash/ciphertext cho client, không gửi mã trong thông báo hoặc danh sách.
11. Banned, shop inactive/KYC thu hồi, đổi chủ làm mất điều kiện trước duyệt/nhận.
    Yêu cầu trả lỗi eligibility; vẫn cho bên còn quyền hủy để trả món. Worker B4 hủy
    các lượt mất điều kiện, ghi actor shop cho cancellation; không tự xác nhận mua.
12. Không xóa cứng lịch sử. Mỗi mutation kiểm tra actor từ DB, version và thời gian
    phía server. Hết hạn xử lý cả bởi worker và trong transaction khi một thao tác
    đụng đến lượt giữ/sản phẩm; không phụ thuộc worker chạy đúng từng giây.

## Trạng thái và trách nhiệm

| Trạng thái | Khóa sản phẩm | Chuyển tiếp |
|---|---|---|
| PENDING | Không | ACCEPTED / REJECTED / CANCELLED / EXPIRED |
| ACCEPTED | Có, thuộc đúng lượt giữ | COMPLETED / CANCELLED / EXPIRED |
| REJECTED | Không | Kết thúc |
| CANCELLED | Không | Kết thúc |
| EXPIRED | Không | Kết thúc, phân biệt chờ duyệt quá hạn và khách không đến bằng CloseReason |
| COMPLETED | Sản phẩm SOLD | Kết thúc, shop xác nhận giao và thu tiền |

Terminal state không có chuyển tiếp. Retry đúng trạng thái/actor/nội dung thành công
trả kết quả đã lưu; retry khác ý định trả 409, không ghi đè lịch sử.
Expired PENDING dùng RESPONSE_TIMEOUT; expired ACCEPTED dùng PICKUP_TIMEOUT.
Completed dùng PAYMENT_RECEIVED_AT_STORE. Lý do từ người dùng giới hạn 500 ký tự.

## B2 — DB

`store.PickupReservations` lưu BuyerId, SellerId, StoreId, ProductId, ContactName/Phone,
BuyerNote, Status, ClientRequestId, RequestHash (SHA256 canonical create payload),
ProductTitleSnapshot, PriceSnapshot, ProductVersionSnapshot, snapshot vị trí shop,
CreatedAt/ScheduledAt/ResponseDeadline/AcceptedAt/ExpiresAt/ClosedAt, ClosedById,
CloseReason, hash/mã nhận được bảo vệ, số lần sai/khóa mã, UpdatedAt và RowVersion.
SellerId lấy từ chủ shop khi tạo, kiểm tra lại lúc duyệt; không nhận từ body.

- FK product/store ghép ngăn gắn món thuộc shop khác; FK users/store dùng RESTRICT.
- Unique BuyerId+ClientRequestId chống retry trùng; RequestHash phát hiện khác payload.
- Unique có filter BuyerId+ProductId cho PENDING/ACCEPTED; ProductId cho ACCEPTED.
  B3 phải expire bản ghi đã quá hạn trước khi kiểm tra quota/unique index.
- Index theo người mua/shop/status/time phục vụ phân trang; index deadline phục vụ worker.
- CHECK trạng thái, lịch, giá/contact, người mua khác người bán, acceptance snapshot,
  closure/actor, độ dài hash/version, tọa độ và bộ đếm mã.
- `Products.ReservedPickupReservationId` nullable, FK ghép với Product.Id đảm bảo đúng món.
  CHECK cấm đồng thời ReservedOrderId và ReservedPickupReservationId, và yêu cầu LOCKED
  khi có pointer pickup. Hoàn tất phải chuyển SOLD và clear pointer trong cùng UPDATE;
  history giữ ProductId ở reservation. Hủy/hết hạn chuyển AVAILABLE và clear pointer.
- CHECK/FK không tự kiểm tra role, quota xuyên bản ghi, status bên kia bảng hay vòng đời
  trước/sau. Đó là invariant transaction của service B3/B4; schema riêng chưa đủ để mở tính năng.

### Khóa chung với checkout (triển khai ở B3)

Lock B3: application lock theo BuyerId cho create (quota/idempotency) → Product
(UPDLOCK,HOLDLOCK) → reservation liên quan → eligibility shop/seller. Application lock
thay user UPDLOCK để không chặn chéo đơn bán của chính Shopper đó.
Cleanup hết hạn có transaction riêng, chỉ khóa một product mỗi lần trước transaction
nghiệp vụ để một phản hồi stale version không rollback việc mở khóa đã hết hạn.
Không giữ reservation row lock trước
product rồi quay lại lấy product. Worker, cancel, complete và accept phải cùng thứ tự;
rà soát tương tác với OrderLifecycle để tránh vòng chờ qua product/order.

Online checkout và pickup đều kiểm tra AVAILABLE + hai pointer rỗng dưới product lock.
Accept đổi ACCEPTED và product LOCKED/pointer atomically; hoàn tất/hủy/expire chỉ giải
phóng khi pointer vẫn trỏ chính reservation đó. Never gọi unlock không truyền owner.
Rà soát MockProductLockService, OrderService, ProductService, ProductGroupService,
relocation và worker khi B3/B4; không chỉ thêm điều kiện ở controller.

## Hợp đồng HTTP (B3/B4 đã triển khai)

Prefix `/api/pickup-reservations`; đăng nhập bắt buộc. Dùng envelope ApiResponse hiện có.

| Method / route | Actor | Body / kết quả |
|---|---|---|
| POST / | Khách | CreatePickupReservationRequest → 201 DTO; retry → 200 cùng id |
| GET /mine | Khách | page/pageSize/status → PickupReservationPage |
| GET /shop | Shop của caller | Cùng filter, shop suy ra từ DB |
| GET /{id} | Buyer / đúng shop | DTO riêng tư; không public |
| POST /{id}/accept | Đúng shop | PickupVersionRequest → DTO |
| POST /{id}/reject | Đúng shop | ClosePickupReservationRequest → DTO |
| POST /{id}/cancel | Buyer / đúng shop | ClosePickupReservationRequest → DTO |
| GET /{id}/pickup-code | Buyer, còn ACCEPTED/hạn | PickupCodeDto, Cache-Control: no-store |
| POST /{id}/complete | Đúng shop | CompletePickupReservationRequest → DTO |

Không có admin endpoint ở bản đầu. Role Admin không được nhập vai buyer/shop.
Request không nhận Status, BuyerId, SellerId, StoreId, Price, deadlines hoặc khóa mã.
DTO không trả code trong detail/list; contact chỉ lộ cho hai bên tham gia. PENDING trả
store snapshot null vì chưa được shop xác nhận. Mỗi DTO có version base64 và serverTime UTC.
Status filter chỉ nhận 6 trạng thái; page >=1, pageSize 1..24, sort CreatedAt desc, Id desc.
Chưa hỗ trợ cập nhật lịch, partial payment, no-show penalty hoặc public reservation search.

Lỗi: 400 INVALID_REQUEST/INVALID_SCHEDULE; 401 chưa đăng nhập; 403 ROLE_NOT_ALLOWED/
NOT_RESERVATION_PARTICIPANT/STORE_NOT_ELIGIBLE; 404 RESERVATION_NOT_FOUND;
409 STALE_VERSION/PRODUCT_CHANGED/PRODUCT_UNAVAILABLE/INVALID_STATE/
RESERVATION_EXPIRED/IDEMPOTENCY_CONFLICT/ACTIVE_RESERVATION_EXISTS/
RESERVATION_LIMIT_REACHED/STORE_LOCATION_CHANGED; 400 INVALID_PICKUP_CODE;
429 PICKUP_CODE_LOCKED (retryAfterSeconds, không trả mã đúng).
Lookup id không thuộc caller trả 404 để tránh dò thông tin; 403 dùng cho action trên
record caller đã có quyền xem hoặc role không hỗ trợ.

## DB mẫu (giả lập, không seed DB ứng dụng)

| Id | Buyer / Seller / Store / Product | Status | ScheduledAt UTC | ResponseDeadline UTC | ExpiresAt UTC |
|---|---|---|---|---|---|
| 101 | 10 / 20 / 5 / 81 | PENDING | 2026-10-28T03:00:00Z | 2026-10-27T23:30:00Z | null |
| 102 | 11 / 20 / 5 / 82 | ACCEPTED | 2026-10-28T04:00:00Z | 2026-10-28T02:00:00Z | 2026-10-28T04:30:00Z |
| 103 | 12 / 20 / 5 / 83 | COMPLETED | 2026-10-28T02:00:00Z | 2026-10-28T01:00:00Z | 2026-10-28T02:30:00Z |

Ví dụ 102: CreatedAt 00:00Z, AcceptedAt 00:10Z, địa chỉ snapshot
"12 Đường Ví Dụ, TP.HCM", giá 250000; Products(82) có Status LOCKED,
ReservedOrderId null, ReservedPickupReservationId 102. Hẹn 04:00Z là 11:00 giờ VN.
Ví dụ 101 CreatedAt 21:30Z ngày 27; ví dụ 103 CreatedAt 23:00Z ngày 27,
AcceptedAt 23:10Z, ClosedAt 02:05Z ngày 28 và ClosedById 20; Products(83) SOLD,
hai pointer null. Hash/code trong mẫu chỉ minh họa hợp đồng, không công bố mã thật.

```json
{
  "productId": 82,
  "productVersion": "AAAAAAAAB9E=",
  "clientRequestId": "929c810e-e82d-4aad-8557-638b6a3c98fd",
  "scheduledAt": "2026-10-28T11:00:00+07:00",
  "contactName": "Khách thử",
  "contactPhone": "0900000000",
  "buyerNote": "Mình ghé xem và nhận tại quầy."
}
```

## Bàn giao B3–B6

B3/B4 đã có service/controller, guard khóa sản phẩm, mã nhận/nhận hàng, worker và
notification outbox. B5 đã nối FE (xem `FE/docs/pickup-b5-integration.md`): tạo,
duyệt, mã nhận, hoàn tất và hủy đã kiểm tra qua trình duyệt trên DB tạm.
B6 đã kiểm tra tích hợp cục bộ Production với DB/key ring riêng, restart tiến trình,
worker hết hạn, relocation và xung đột qua UI; xem `FE/docs/pickup-b6-verification.md`.
Smoke trên máy triển khai thật vẫn cần trước phát hành.
Các ca HTTP/SQL concurrency đã được kiểm tra trong quá trình B3/B4; chưa áp dụng DB ứng dụng.
