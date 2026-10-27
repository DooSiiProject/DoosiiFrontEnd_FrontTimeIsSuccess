# Cộng đồng part 7 — hợp đồng dữ liệu và API

Trạng thái: B1–B4 đã triển khai trong BE, bao gồm API `/api/community`.
FE được chuyển đổi ở B5. Kết quả kiểm thử B3/B4 và hướng dẫn chạy nằm tại
`community-part7-publishing.md`.

## Quyết định nghiệp vụ

- Một bài có loại PASS hoặc OTHER. Không đổi loại sau khi đăng.
- CasualUser được tạo PASS và OTHER. Shopper chỉ được tạo OTHER.
- PASS có đúng một PassPostInfo; OTHER không có PassPostInfo.
- Title (mục đích bài) và ProductName là hai trường riêng, đều bắt buộc với PASS.
- PostDescription bắt buộc; ProductDescription tùy chọn.
- OTHER có thể chỉ có chữ hoặc kèm ảnh/video. PASS cần ít nhất một ảnh,
  có thể kèm video. Đây là mặc định triển khai đã đề xuất trong kế hoạch.
- PASS tuyệt đối không gắn shop/sản phẩm: tất cả media có StoreId/ProductId null.
- OTHER: CasualUser gắn shop/sản phẩm công khai bất kỳ; Shopper chỉ gắn shop
  do mình sở hữu và sản phẩm công khai thuộc shop đó.
- Gắn sản phẩm: BE suy ra StoreId. Nếu client gửi cả hai và không khớp, từ chối.
- Shop phải active, chủ có role Shopper và KYC APPROVED theo quy tắc hiện tại.
  Sản phẩm phải IsPublished, thuộc shop đủ điều kiện công khai. Không đồng nhất
  khả năng công khai với khả năng mua: sản phẩm SOLD vẫn có thể được chia sẻ
  nếu còn công khai, hiển thị rõ đã bán và không cho mua.
- Không có vị trí tự nhập, PostLocations, giá cọc hoặc thanh toán PASS đợt này.
- Khuyết điểm bắt buộc: nhập nội dung hoặc chọn “Không có” (lưu chuỗi này).
- Chỉ tương tác cấp bài. Không có like/comment/report riêng cho media.
- Giữ giới hạn 10 bài PASS/tác giả/24 giờ, kiểm tra trong transaction ở B3.

## Schema

| Bảng | Trường và kiểu |
|---|---|
| forum.Posts | Id int identity PK; AuthorId int FK auth.Users; Type nvarchar(10); Title nvarchar(150); PostDescription nvarchar(max), giới hạn ứng dụng 5000 ký tự; VisibilityStatus nvarchar(20); ClientRequestId uniqueidentifier; CreatedAt datetime2; UpdatedAt datetime2 nullable; RowVersion rowversion |
| forum.PassPostInfos | PostId int PK; PostType nvarchar(10), luôn PASS; ProductName nvarchar(300); ProductDescription nvarchar(2000) nullable; Price decimal(18,2); Size nvarchar(50); ConditionPercent int; Defects nvarchar(2000); StyleTagsJson nvarchar(4000); MeasurementsJson nvarchar(max) nullable; SaleStatus nvarchar(20) |
| forum.PostMedia | Id int identity PK; PostId int; PostType nvarchar(10); MediaType nvarchar(10); SourceUrl nvarchar(1000); StoragePublicId nvarchar(500); ThumbnailUrl nvarchar(1000) nullable; Caption nvarchar(1000) nullable; DisplayOrder int; StoreId int nullable; ProductId int nullable |
| forum.Comments | Giữ nội dung, tác giả, reply, ClientRequestId, thời gian, IsDeleted; bỏ cột PostType; PostId FK Posts |
| forum.Reactions | Giữ UserId, ReactionType, thời gian; bỏ cột PostType; PostId FK Posts |
| forum.PostReports | Giữ toàn bộ dữ liệu báo cáo/xử lý; bỏ cột PostType; PostId FK Posts |

PostType trong PassPostInfos/PostMedia là discriminator nội bộ để FK ghép tham
chiếu Posts(Id, Type). Client không gửi trường này. Nó giúp DB chặn OTHER có
PassPostInfo, media không khớp loại bài và PASS gắn shop/sản phẩm.

Không có AllowEscrow trong schema mới: luồng giao dịch PASS nằm ngoài phạm vi.
Không cần bảng ánh xạ ID cũ vì dữ liệu cộng đồng trống.

### Ràng buộc tại DB

- Type: PASS/OTHER; VisibilityStatus: ACTIVE/HIDDEN; SaleStatus: AVAILABLE/SOLD.
- MediaType: IMAGE/VIDEO; DisplayOrder >= 0, unique (PostId, DisplayOrder).
- Price > 0; ConditionPercent 0..100; tên, size, khuyết điểm, tiêu đề/nội dung không rỗng.
- Tags/measurements là JSON array; cấu trúc phần tử được kiểm tra tại BLL.
- ProductId có giá trị thì StoreId phải có giá trị. FK ghép tới Products(Id, StoreId)
  bảo đảm sản phẩm thuộc shop được gắn. Thêm alternate key này trên Products.
- Unique (AuthorId, ClientRequestId) cho tạo bài; không nhận UUID toàn số 0.
- Một reaction/user/post; một báo cáo PENDING/reporter/post.
- Reply tham chiếu (ParentCommentId, PostId), không thể thuộc bài khác.
- Quan hệ mới dùng NO ACTION để tránh cascade làm mất nội dung hoặc lịch sử.

### Ràng buộc tại service B3/B4

DB không thể yêu cầu một parent vừa insert phải lập tức có child bằng FK thông
thường. Tạo/sửa PASS ghi Post + PassInfo + media trong cùng transaction,
kiểm tra đúng một PassInfo và ít nhất một ảnh trước commit. Quyền theo user,
trạng thái công khai, số lượng media, số đo/tag hợp lệ và reply tối đa hai cấp
được kiểm tra tại service B3/B4 và đã có bộ kiểm thử HTTP/SQL.

## Hợp đồng API

Base: `/api/community`. Response dùng ApiResponse hiện tại:
`{ success, message, data, errors: ["ERROR_CODE"] }`.

| Method | Path | Chức năng |
|---|---|---|
| POST | /posts | Tạo bài, authenticated |
| GET | /posts/{id} | Chi tiết công khai |
| PUT | /posts/{id} | Sửa nội dung/media, tác giả |
| PATCH | /posts/{id}/visibility | Tác giả ẩn bài; admin xử lý theo quyền kiểm duyệt |
| PATCH | /posts/{id}/sale-status | Tác giả PASS đổi AVAILABLE/SOLD |
| GET | /posts?tab=newest&page=1&pageSize=12 | Feed chung |
| GET | /posts?tab=by-style&style=90s-vintage | Lọc style của PASS |
| GET | /posts?tab=near-you&latitude=10.77&longitude=106.70&radiusKm=5 | OTHER gắn shop trong bán kính |
| GET | /posts?storeId=8&page=1&pageSize=12 | Bài OTHER nhắc shop, kể cả qua sản phẩm |
| PUT / DELETE | /posts/{id}/reaction | Like/unlike idempotent |
| GET / POST | /posts/{id}/comments | Đọc/tạo bình luận |
| DELETE | /posts/{id}/comments/{commentId} | Xóa mềm bình luận theo quyền |
| POST | /posts/{id}/reports | Báo cáo bài |
| GET | /moderation/reports | Hàng đợi admin |
| GET | /moderation/reports/{id}/post | Nội dung cho admin kiểm duyệt |
| PUT | /moderation/reports/{id}/resolve | Xử lý báo cáo |

Danh sách trả `{ items, totalItems, page, pageSize }`, page >= 1, pageSize 1..50.
Ngày ISO 8601 UTC. Sort newest: CreatedAt DESC, Id DESC. Nearby: khoảng cách
tới shop công khai gần nhất ASC, CreatedAt DESC, Id DESC; DISTINCT bài trước
count và phân trang. Không có tọa độ lưu trên bài. Bài PASS không vào nearby.
Shop/sản phẩm hết công khai: không trả chi tiết/đường dẫn của liên kết đó;
media vẫn được đọc cùng bài, đánh dấu attachmentUnavailable; không dùng shop
không công khai để tính nearby. Không tự chuyển shop hoặc xóa nội dung bài.

### Tạo bài

Multipart/form-data gồm `payload` (JSON) và các file theo `uploadKey`.
BE xác minh file thực, không tin URL/publicId do client tự gửi.

```json
{
  "clientRequestId": "aaf58201-1be3-4c4c-bc50-fbcf5bccf417",
  "type": "PASS",
  "title": "Mình cần pass áo khoác denim",
  "postDescription": "Áo ít mặc, mình muốn tìm chủ mới.",
  "passInfo": {
    "productName": "Áo khoác denim xanh",
    "productDescription": null,
    "price": 250000,
    "size": "M",
    "conditionPercent": 90,
    "defects": "Sờn nhẹ cổ tay",
    "styleTagIds": ["90s-vintage"],
    "measurements": [{ "name": "Vai", "unit": "cm", "value": 44 }]
  },
  "media": [
    { "uploadKey": "front", "mediaType": "IMAGE", "caption": "Mặt trước", "displayOrder": 0, "storeId": null, "productId": null },
    { "uploadKey": "clip", "mediaType": "VIDEO", "caption": null, "displayOrder": 1, "storeId": null, "productId": null }
  ]
}
```

`front` và `clip` là tên file parts; uploadKey duy nhất và phải khớp đúng một
file. Với OTHER, passInfo phải null và media có thể rỗng. Ví dụ media OTHER:

```json
{ "uploadKey": "product", "mediaType": "IMAGE", "caption": "Mẫu mới", "displayOrder": 0, "storeId": null, "productId": 42 }
```

BE suy ra shop từ product 42 và kiểm tra quyền của tác giả. AuthorId, visibility,
saleStatus, sourceUrl, storagePublicId, PostType nội bộ đều không lấy từ payload tạo.
Tạo thành công trả 201, data là chi tiết bài. Gửi lại cùng ClientRequestId và
cùng nội dung trả bài đã tạo; payload khác cùng ID trả 409.

### Chi tiết và cập nhật

```json
{
  "success": true,
  "message": "Thành công.",
  "data": {
    "id": 101,
    "type": "PASS",
    "author": { "id": 12, "fullName": "An", "avatarUrl": null },
    "title": "Mình cần pass áo khoác denim",
    "postDescription": "Áo ít mặc, mình muốn tìm chủ mới.",
    "visibilityStatus": "ACTIVE",
    "passInfo": {
      "productName": "Áo khoác denim xanh", "productDescription": null,
      "price": 250000, "size": "M", "conditionPercent": 90,
      "defects": "Sờn nhẹ cổ tay", "styleTagIds": ["90s-vintage"],
      "measurements": [{ "name": "Vai", "unit": "cm", "value": 44 }]
    },
    "saleStatus": "AVAILABLE",
    "media": [{ "id": 501, "mediaType": "IMAGE", "sourceUrl": "https://media.example/denim.jpg", "thumbnailUrl": null, "caption": "Mặt trước", "displayOrder": 0, "store": null, "product": null, "attachmentUnavailable": false }],
    "summary": { "reactionCount": 0, "commentCount": 0, "likedByMe": false },
    "version": "AAAAAAAAAAE=",
    "createdAt": "2026-10-27T03:00:00Z",
    "updatedAt": null
  },
  "errors": []
}
```

Version là RowVersion base64. PUT gửi version và toàn bộ nội dung/media mong
muốn; giữ media cũ bằng id, thêm bằng uploadKey, media bị bỏ khỏi danh sách sẽ
được gỡ. Không nhận id media của bài khác. Type không đổi. Kiểm tra version
trước cập nhật và chạm Posts.UpdatedAt khi sửa bất kỳ child nào để version tăng.
Xung đột trả 409. Reorder unique index cần thực hiện hai bước vị trí tạm trong
transaction; không update hoán đổi trực tiếp gây unique violation.

Visibility PATCH: `{ "visibilityStatus": "HIDDEN", "version": "..." }`.
Tác giả không được sửa/mở lại bài đã bị admin ẩn. `HiddenByAdmin` lưu nguồn ẩn;
admin có thể mở lại bằng cùng endpoint, tác giả chỉ tự mở bài do mình ẩn.
Sale PATCH: `{ "saleStatus": "SOLD", "version": "..." }`.

### Validation và mã lỗi

Title 1..150, PostDescription 1..5000, ProductName 1..300, ProductDescription
<=2000, Size 1..50, Defects 1..2000, caption <=1000. Trim chuỗi trước kiểm tra.
Giá decimal >0, tối đa 16 chữ số phần nguyên và 2 số lẻ. StyleTagIds bắt buộc
ít nhất một tag hợp lệ cho PASS, tối đa 50, từng ID <=64, không trùng.
Measurements tùy chọn, tái dùng ProductMeasurement hiện tại: tối đa 30 dòng,
name 1..80, unit 1..20, value hữu hạn >0 và <=1000000.

Giới hạn media cho B3 (mặc định kỹ thuật, cần kiểm tra storage trước triển khai):
tối đa 10 media/bài, tối đa 2 video; ảnh JPEG/PNG/WebP <=5 MiB;
video MP4 <=50 MiB và <=60 giây, xác minh codec hỗ trợ phát trên web;
tổng file <=140 MiB. Đặt request body limit đủ phần multipart overhead.
MIME/extension không thay thế kiểm tra nội dung file. Dọn upload khi transaction
thất bại và chỉ xóa file cũ sau khi cập nhật DB thành công.

| HTTP | Mã lỗi |
|---|---|
| 400 | INVALID_POST_TYPE, INVALID_POST_CONTENT, PASS_INFO_REQUIRED, PASS_INFO_NOT_ALLOWED, INVALID_PASS_INFO, PASS_TAG_NOT_ALLOWED, INVALID_MEDIA, PRODUCT_STORE_MISMATCH, POST_TYPE_IMMUTABLE |
| 401 | UNAUTHORIZED |
| 403 | PASS_ROLE_REQUIRED, ATTACHMENT_NOT_OWNED, POST_NOT_OWNED, USER_BANNED |
| 404 | POST_NOT_FOUND, ATTACHMENT_NOT_AVAILABLE |
| 409 | COMMUNITY_CONFLICT, CLIENT_REQUEST_CONFLICT, REPORT_ALREADY_PENDING |
| 413 | MEDIA_TOO_LARGE |
| 429 | PASS_DAILY_LIMIT |
| 503 | CLOUDINARY_NOT_CONFIGURED; COMMUNITY_UPGRADE_PENDING chỉ còn ở các route tạo/đọc/feed cũ |

## Chuyển đổi và xác minh

Migration mới thay bốn bảng cũ PassPosts/PostImages/SpotSharingPosts/SpotImages,
đổi các bảng tương tác và thêm Posts/PassPostInfos/PostMedia. Có preflight chặn
nếu bất kỳ bảng nội dung/tương tác cộng đồng cũ nào không trống; không tự xóa
dữ liệu ngoài giả định người dùng đã xác nhận. Không sửa lịch sử migration cũ.

Các CLR type/DbSet cũ và PostType CLR trên tương tác tạm giữ để service cũ
compile, nhưng bị Ignore trong EF. Tương tác và report admin đã chuyển sang
schema mới và được mở lại. Các route tạo/đọc/feed PASS/SPOT cũ vẫn trả 503;
FE cần chuyển sang `/api/community` ở B5. Chi tiết route và kết quả B3/B4 xem
`community-part7-publishing.md`.

Design-time factory cho phép tạo/script migration bằng DAL mà không khởi động
API (API hiện tự migrate/seed khi chạy). Mặc định trỏ DB LocalDB thử nghiệm;
để dùng DB khác truyền `DOOSII_EF_CONNECTION` hoặc `--connection` rõ ràng.
Không thay connection string runtime. Lưu ý khởi động API sẽ áp dụng migration
vào DB cấu hình của ứng dụng.

```powershell
dotnet build Doosii.sln
dotnet build Doosii.DAL --no-restore
dotnet ef migrations has-pending-model-changes --project Doosii.DAL --startup-project Doosii.DAL --no-build
dotnet ef migrations script 20261021040704_AddOrderPaymentMethod --project Doosii.DAL --startup-project Doosii.DAL --no-build
```

Kiểm chứng trên SQL Server thử nghiệm: áp dụng đến migration trước, áp dụng
migration mới, kiểm tra FK/check/index, chạy ca dữ liệu hợp lệ/sai trong
transaction rồi rollback; thử Down khi schema mới trống và Up lại. Down có
guard từ chối khi đã có dữ liệu mới để tránh mất dữ liệu sau khi vận hành.

SQL constraint test: `documents/tests/community-part7-constraints.sql`. Khi chạy
script EF bằng sqlcmd, dùng `-I -b` để bật QUOTED_IDENTIFIER cho filtered index
và dừng ngay khi có lỗi. Chỉ chạy test trên DB riêng có tên
`DoosiiPart7SchemaTest_*`. Kết quả lần triển khai: xem
`community-part7-verification.md`.
