# Doosii - Roadmap & Task Tracker (Final Backend Assignment & Progress)

## 1. Overview & Team Allocation Strategy

The backend workload is divided **50 / 50** between **Wee** and **Tri** based on **Domain Schema Boundaries** to allow parallel, independent development with zero blocking dependencies:
- **Tri's Domain**: `store` and `forum` schemas (Discovery, Thrift Map, Inventory, Community Feed, Cloudinary, Chat Hub).
- **Wee's Domain**: `order` and `notification` schemas (Escrow State Machine, PayOS VietQR, Background Workers, Wallets, Admin Arbitration, Notification Hub, Store KYC).

```
+-----------------------------------------------------------------------------------+
|                            INDEPENDENT PARALLEL TRACKS                            |
+-----------------------------------------+-----------------------------------------+
|        TRACK 1: TRI                     |        TRACK 2: WEE                     |
|  - Domain: Store, Map & Forum           |  - Domain: Escrow, Orders & Admin       |
|  - Schemas: `store`, `forum`            |  - Schemas: `order`, `notification`     |
|  - Storage: Cloudinary (Images)         |  - Payments: PayOS (Dynamic VietQR)     |
|  - Spatial: Haversine Radius (1-10km)   |  - Background: .NET BackgroundService   |
|  - Hub: /hubs/chat                      |  - Hub: /hubs/notifications             |
+-----------------------------------------+-----------------------------------------+
                    \                                 /
                     \-- Shared Interface Contract --/
                        IStoreService.LockProduct()
```

---

## 2. Independence & Conflict Prevention Rules
1. **Schema Isolation**: Tri touches only entities in `Doosii.DAL/Models/Store/` and `Forum/`. Wee touches only entities in `Doosii.DAL/Models/Order/` and `Notification/`.
2. **Decoupled Integration (In-Process Contract)**: Wee does not wait for Tri's product table to finish. Wee defines:
   ```csharp
   public interface IProductLockService {
       Task<bool> LockProductAsync(int productId, int orderId);
       Task UnlockProductAsync(int productId);
   }
   ```
   Wee tests with a mock/stub while Tri builds the full store catalog.
3. **Migration Coordination**: Each developer specifies migration names clearly (e.g. `AddStoreAndForumSchema` by Tri, `AddOrderAndEscrowSchema` by Wee).
4. **Git Branching**:
   - Tri works on `DooSii-Tri` $\rightarrow$ PR to `develop`.
   - Wee works on `DooSii-Wee` $\rightarrow$ PR to `develop`.

---

## 3. Project Phase Overview (EXE201 Milestones)

| Phase | Duration | Focus Area | Status | Ghi chú nghiệm thu |
|---|---|---|:---:|---|
| **Phase 1** | Weeks 1–2 | Requirements, Documentation, Architecture & Schemas | 🟢 Completed | Hoàn thành tài liệu kiến trúc, database ERD, API spec |
| **Phase 2** | Weeks 3–6 | Core Backend (Auth, Thrift Map, Store & Inventory) | 🟡 In Progress | Wee xong Auth & Store KYC; Tri xong Store/Map, còn thiếu Forum & Cloudinary |
| **Phase 3** | Weeks 7–10 | Escrow Order Processing, PayOS, SignalR Notification Hub | 🟢 Completed | Wee hoàn thành 100% Escrow, PayOS VietQR, Background Workers |
| **Phase 4** | Weeks 11–12 | Dispute Workflows, Admin Arbitration & Wallet Payouts | 🟢 Completed | Wee hoàn thành 100% Ví Seller, Khiếu nại unboxing, Trọng tài Admin |
| **Phase 5** | Week 13+ | Pilot Launch (Ho Chi Minh City) & Maintenance | ⚪ Upcoming | Sẵn sàng tích hợp Frontend & Test End-to-End thực tế |

---

## 4. Feature Implementation & Task Breakdown

### Module 1: Auth & User Onboarding [Foundation - Shared / Wee]
- [x] Initial 3-layer architecture (`Doosii.API`, `Doosii.BLL`, `Doosii.DAL`) `[Completed]`
- [x] Basic `Users` table migration & BCrypt password hashing `[Completed]`
- [x] `POST /api/auth/register` `[Completed]`
- [x] `POST /api/auth/login` (JWT token issuance) `[Completed]`
- [x] `GET /api/auth/me` (Bearer token validation) `[Completed]`
- [x] `[Wee]` 6-digit OTP email verification for registration & password reset `[Completed]`
- [x] `[Wee]` Google OAuth login integration (`IsGoogle = true`) `[Completed]`
- [x] `[Wee]` Refresh token mechanism (`POST /api/auth/refresh`) `[Completed]`
- [x] `[Tri]` User profile update API (`PUT /api/users/profile`) `[Completed]`
- [x] `[Wee]` **Đơn mở shop (Leader Request)**: Enhanced entity `auth.MerchantProfiles` with custom ContactName, ContactEmail, AddressType (OLD/NEW), EstablishedDate, TaxCode, ShopMediaUrls (JSON array), User CRUD (`POST/GET/PUT/DELETE /api/users/seller-application/me`) & Admin Review (`GET /api/admin/kyc-requests`, `/pending`, `PUT /{id}/approve`, `PUT /{id}/reject`) `[Completed]`

---

### Module 2: Thrift Map & Stores [Track 1: Tri]
*Domain Schema: `store`*
- [x] `[Tri]` EF Core Entities: `Store`, `Category`, `Product`, `ProductImage`, `StoreLocation` `[Completed]`
- [x] `[Tri]` Store CRUD API (`POST /api/stores`, `GET /api/stores/{id}`, `PUT /api/stores/{id}`) `[Completed]`
- [x] `[Tri]` Geolocation query API (`/api/map/nearby-stores`) using Haversine formula (1km, 3km, 5km, 10km) `[Completed]`
- [x] `[Tri]` Store & style filtering (Vintage, Y2K, Streetwear, price range, store type, sort by distance/rating/newest) `[Completed]`
- [x] `[Tri]` Store catalog product listing API (`GET /api/stores/{id}/products`, `POST /api/stores/{id}/products`) `[Completed]`
- [x] `[Tri]` Product update & delete API (`PUT/DELETE /api/products/{id}`, locked against `LOCKED`/`SOLD`) `[Completed]`
- [ ] `[Tri]` **API Xem Chi Tiết 1 Sản Phẩm** (`GET /api/products/{productId}`) `[Pending / Tri]`
- [ ] `[Tri]` **Store Reviews & Rating API** (Bảng `StoreReview`, đánh giá 1–5 sao, tự tính `RatingAverage`) `[Pending / Tri]`
- [ ] `[Tri]` **Cloudinary Upload Helper Service** (Upload ảnh thực tế qua `multipart/form-data`) `[Pending / Tri]`

---

### Module 3: Thrift Forum & Community [Track 1: Tri]
*Domain Schema: `forum`*
- [ ] `[Tri]` EF Core Entities: `PassPost`, `PostImage`, `SpotSharingPost`, `Comment`, `Reaction`, `PostReport` `[Pending / Tri]`
- [ ] `[Tri]` Create Pass Post API (`POST /api/forum/posts/pass`: 1–5 Cloudinary photos, size, condition %, price, allowEscrow) `[Pending / Tri]`
- [ ] `[Tri]` Spot-sharing post API (`POST /api/forum/posts/spot`: GPS tag hoặc link tới shop Thrift Map) `[Pending / Tri]`
- [ ] `[Tri]` Community feed API with pagination (`GET /api/forum/feed`: Newest, Near You, By Style) `[Pending / Tri]`
- [ ] `[Tri]` 2-level comment thread API (`POST /api/forum/posts/{id}/comments`) & Like/Heart reaction toggles `[Pending / Tri]`
- [ ] `[Tri]` Flag / Report post API (`POST /api/forum/posts/{id}/report`) & Admin moderation queue `[Pending / Tri]`
- [ ] `[Tri]` SignalR Chat Hub (`/hubs/chat`) for 1-on-1 buyer/seller chat with product card preview & chat history `[Pending / Tri]`

---

### Module 4: Escrow & Order Processing [Track 2: Wee]
*Domain Schema: `order`*
- [x] `[Wee]` EF Core Entities: `Order`, `OrderItem`, `EscrowTransaction`, `PaymentLog`, `Dispute`, `WithdrawalRequest` `[Completed]`
- [x] `[Wee]` Implement `IProductLockService` contract to reserve items during checkout `[Completed]`
- [x] `[Wee]` Create Escrow Order API (`POST /api/orders/create-escrow`) with 2.5% fee calculation & order tracking (`GET /api/orders/{id}`, `/my-purchases`, `/my-sales`) `[Completed]`
- [x] `[Wee]` PayOS / SePay dynamic VietQR code generator (`DOSI <OrderId>`) `[Completed]`
- [x] `[Wee]` Secure PayOS Webhook endpoint: HMAC signature validation, idempotency, move to `ESCROW_HOLDING` `[Completed]`
- [x] `[Wee]` Seller shipping carrier & tracking code update API (move to `IN_TRANSIT`) `[Completed]`
- [x] `[Wee]` Buyer "Received & Accepted" confirmation API (move to `COMPLETED_RELEASED` + credit seller wallet) `[Completed]`
- [x] `[Wee]` **.NET `BackgroundService` (`OrderEscrowBackgroundService`)**: `[Completed]`
  - Auto-cancel unpaid orders after 15 minutes (unlock product)
  - Auto-complete orders after 72 hours in `IN_TRANSIT` if no dispute is opened (bỏ qua đơn đang tranh chấp)

---

### Module 5: Seller Tools, Wallets & Admin Portal [Track 2: Wee]
*Domain Schema: `order` & `auth` (KYC)*
- [x] `[Wee]` Seller wallet balance tracking & transaction ledger API (`GET /api/seller/wallet`) `[Completed]`
- [x] `[Wee]` Bank withdrawal request API (minimum 50,000 VND, `POST /api/seller/wallet/withdraw`, `GET /withdrawals`) `[Completed]`
- [x] `[Wee]` Buyer dispute filing API (within 24h of delivery with mandatory unboxing video/photos, `POST /api/orders/{id}/dispute`) `[Completed]`
- [x] `[Wee]` Admin KYC application review & approval/rejection API (`GET/PUT /api/admin/kyc-requests`) `[Completed]`
- [x] `[Wee]` Admin Dispute Arbitration API (phán quyết `REFUND_BUYER` hoặc `RELEASE_SELLER` giải ngân người bán) `[Completed]`
- [x] `[Wee]` Admin Withdrawal Processing API (`POST /api/admin/withdrawals/{id}/process`: duyệt lệnh hoặc từ chối hoàn tiền ví) `[Completed]`
- [x] `[Wee]` Financial analytics API (GMV, escrow fee platform revenue, orders & sellers breakdown) `[Completed]`
- [x] `[Wee]` Bale-opening announcement purchase & payment flow (max 2/day/store, 50,000 VND fee via wallet/PayOS, public upcoming events API) `[Completed]`
- [x] `[Wee]` SignalR Notification Hub (`/hubs/notifications`) for real-time order state changes, wallet alerts, bale-opening broadcasts, and persistent in-app notifications (`/api/notifications`) `[Completed]`

---

## 5. Bảng Tổng Hợp So Sánh Tiến Độ (Final Task Matrix: Wee vs. Trí)

| Hạng mục / Module | Phụ trách | Phạm vi chi tiết | Tình trạng | Tỷ lệ hoàn thành |
|---|:---:|---|:---:|:---:|
| **Xác thực & Onboarding** | Wee | Đăng ký, đăng nhập JWT, OTP Email 6 số, Refresh Token Rotation, Quên/đổi mật khẩu, Google OAuth | 🟢 **Hoàn thành** | **100%** |
| **Đơn mở shop (KYC)** | Wee *(Leader Request)* | Entity mở rộng (6 trường), User CRUD (`/me`), Admin duyệt/từ chối, nâng quyền Seller | 🟢 **Hoàn thành** | **100%** |
| **Ký quỹ & Đơn hàng (Escrow)** | Wee | Tạo đơn ký quỹ, PayOS VietQR, HMAC Webhook, giao hàng, xác nhận nhận hàng, giải ngân ví | 🟢 **Hoàn thành** | **100%** |
| **Tiến trình ngầm (Worker)** | Wee | Auto-cancel sau 15m (nhả lock), Auto-complete sau 72h, bảo vệ đơn tranh chấp | 🟢 **Hoàn thành** | **100%** |
| **Ví tiền & Rút tiền ngân hàng** | Wee | Tra cứu số dư ví (khả dụng/tạm giữ), sao kê Ledger, rút tiền (min 50k), hoàn tiền khi Admin từ chối | 🟢 **Hoàn thành** | **100%** |
| **Khiếu nại & Trọng tài Admin** | Wee | Mở khiếu nại unboxing, Admin xử `BUYER_FAVOR` / `RELEASE_SELLER`, thống kê tài chính GMV | 🟢 **Hoàn thành** | **100%** |
| **Thông báo & Mở kiện hàng** | Wee | Đăng ký mở kiện (max 2/ngày, phí 50k), chuông in-app, SignalR Notification Hub | 🟢 **Hoàn thành** | **100%** |
| **Kiểm thử tự động (Track 2)** | Wee | 8 Test Suites (Happy Paths + 16 Edge Cases & Security) chạy 1 lệnh tự động | 🟢 **Hoàn thành** | **100%** |
| **Profile người dùng** | Trí | Cập nhật họ tên, avatar, địa chỉ nhận hàng (`PUT /api/users/profile`) | 🟢 **Hoàn thành** | **100%** |
| **Bản đồ Thrift & Cửa hàng** | Trí | CRUD Cửa hàng, Định vị Haversine GPS (1-10km), Lọc Style/Giá, CRUD Sản phẩm | 🟡 **Hoàn thành 70%** | **70%**<br>*(Thiếu: Xem chi tiết 1 SP, Review/Rating, Cloudinary)* |
| **Diễn đàn & Cộng đồng (Forum)** | Trí | Đăng bài pass đồ, chia sẻ tọa độ, bảng tin Feed, bình luận 2 cấp, thả tim, báo cáo vi phạm | 🔴 **Chưa bắt đầu** | **0%** |
| **SignalR Chat Hub** | Trí | Nhắn tin 1-on-1 thời gian thực, ghim card sản phẩm, mua nhanh qua Ký quỹ | 🔴 **Chưa bắt đầu** | **0%** |

---

## 6. Hệ Thống Kiểm Thử Tự Động Toàn Diện (Automated Test Suites Status)

Toàn bộ **8 bộ Test Suites** của Wee được tổ chức trong thư mục `scratch/`, loại trừ khỏi Git theo `.gitignore`, và được điều phối bằng **1 câu lệnh duy nhất** qua Master Runner:

```powershell
powershell -ExecutionPolicy Bypass -File scratch/run_all_tests.ps1
```

### Bảng Kết Quả Nghiệm Thu Master Runner:
```
=======================================================================
                       MASTER TEST RUN SUMMARY                         
=======================================================================

Index Suite                            Script                                   Status Duration
----- -----                            ------                                   ------ --------
    1 1. Auth & OTP Lifecycle          test_auth_flow.ps1                       PASSED 13.72s  
    2 2. Store Application (KYC)       test_store_application_crud.ps1          PASSED 9.28s   
    3 3. Escrow & Order Fulfillment    test_order_fulfillment.ps1               PASSED 10.16s  
    4 4. Background Services           test_background_worker.ps1               PASSED 10.56s  
    5 5. Seller Wallet & Ledger        test_seller_wallet.ps1                   PASSED 9.76s   
    6 6. Disputes & Admin Arbitration  test_admin_and_dispute.ps1               PASSED 10.70s  
    7 7. Notifications & Announcements test_notifications_and_announcements.ps1 PASSED 10.07s  
    8 8. Edge Cases & Security         test_edge_cases_and_security.ps1         PASSED 23.95s  

ALL 8 TEST SUITES PASSED (100% COVERAGE FOR WEE'S DOMAIN - QUALITY GRADE: 10/10)
Total Execution Time: 102.48 seconds
=======================================================================
```
