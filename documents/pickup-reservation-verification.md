# Xác minh B1/B2 — giữ đồ nhận tại shop

Ngày: 27/10/2026.

## Kết quả

- B1 chốt trong `pickup-reservation-contract.md`: phạm vi sản phẩm shop, chưa cọc,
  giờ hẹn 30 phút..24 giờ, phản hồi tối đa 2 giờ, giữ đến giờ hẹn +30 phút,
  tối đa 3 lượt PENDING/ACCEPTED, quyền hủy/duyệt/nhận và mã nhận tại quầy.
- B2 có entity/configuration/DbSet, Product.ReservedPickupReservationId, migration
  `20261027162606_AddPickupReservations`, designer/snapshot và DTO request/response.
- SQL review: `pickup-reservation-migration.sql`, sinh bằng EF từ migration trước
  đến AddPickupReservations. Chỉ thêm schema pickup; không sửa identity Products,
  xóa bảng hoặc sửa dữ liệu cộng đồng/đơn hàng hiện có.
- EF `has-pending-model-changes`: không có khác biệt model/snapshot.
- Build toàn solution và bộ console kiểm tra: thành công, 0 warnings, 0 errors.
- Bộ console `tests/Doosii.PickupSchemaChecks`: **35 kiểm tra đạt** trên SQL Server
  thật, migrations thực tế. Kiểm tra schema, DTO, Up/Down, giữ nguyên Product identity
  và dữ liệu sản phẩm qua Down/Up.
- 25 lệnh ghi sai bị từ chối: trạng thái, giá/contact, tự giữ món của mình, lịch quá
  sớm/quá xa, deadline sai, product thuộc shop khác, ACCEPTED thiếu metadata,
  thiếu hạn/địa chỉ, sai số lần nhập mã/tọa độ/request ID, closure thiếu actor,
  hoàn tất tại đúng hạn, pointer online+pickup đồng thời, pointer pickup khi AVAILABLE,
  pointer sang lượt giữ của món khác, duplicate request/active/accepted, hash/version
  sai và xóa sản phẩm có lịch sử pickup.
- Down khi còn reservation bị chặn bằng SQL 51009. Sau khi dọn fixture pickup,
  Down rồi Up thành công và sản phẩm trước đó vẫn còn.
- DB thử `DoosiiPickupTest_GUID` được tạo riêng, dọn trong finally; lần chạy thành công
  in thông báo đã xóa DB. Không dùng connection appsettings, không chạy API Program,
  seed admin hoặc background worker ứng dụng.

## Chạy lại

Từ thư mục BE; cần .NET SDK và quyền tạo DB thử trên SQL Server localhost bằng
Windows authentication. Các dependency dùng chung với solution, không thêm package mới.

```powershell
dotnet build Doosii.DAL --no-restore
dotnet ef migrations has-pending-model-changes --project Doosii.DAL --startup-project Doosii.DAL --no-build
dotnet build tests/Doosii.PickupSchemaChecks -p:OutputPath=D:/Summer26/EXE101/CheckPoint3MVP/BE/.build-part7/pickup-checks/
dotnet .build-part7/pickup-checks/Doosii.PickupSchemaChecks.dll
dotnet build Doosii.sln --no-restore -p:OutputPath=D:/Summer26/EXE101/CheckPoint3MVP/BE/.build-part7/pickup-solution/
```

Chạy console xong mới build solution sang output khác: project references dùng chung
MSBuild file list. Không tắt cưỡng bức console trong lúc tạo DB nếu cần cleanup tự động.

## Giới hạn bàn giao

Chưa có controller/service pickup, worker, notification, bảo vệ mã nhận runtime hoặc FE.
35 ca là kiểm tra schema/hợp đồng, không phải chứng minh ứng dụng đã khóa/giải phóng
sản phẩm đúng khi có hai request đồng thời; phần đó thuộc B3/B4 và B6.
FK/CHECK chỉ bảo đảm các invariant mô tả trong hợp đồng; role/quota/lifecycle và trạng
thái liên bảng cần transaction/service. Không mở nút giữ đồ chỉ dựa vào schema này.

Chưa migrate DB ứng dụng, chưa commit/push/deploy. Program API đang tự migrate khi
khởi động: lần khởi động backend mới có thể áp dụng migration pickup theo cấu hình
DB hiện tại. Rollback chỉ được khi bảng PickupReservations trống; không bỏ guard nếu
đã có lịch sử. Giữ API/FE pickup tắt đến khi B3–B6 hoàn tất.
