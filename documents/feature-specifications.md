# Doosii - Feature Specifications & Business Rules

This document details all 20 system features, their functional requirements, and governing business rules.

---

## Part 1: Onboarding & Account Management

### REQ-01: Authentication (Đăng ký & Đăng nhập)
- **Actors**: Guest, Customer, Seller, Admin
- **Requirements**:
  - Email/Password registration with regex email validation and 6-digit OTP email verification.
  - Password criteria: Minimum 8 characters, uppercase, lowercase, numbers (hashed with BCrypt).
  - Google OAuth registration/login (`IsGoogle = true`).
  - JWT token generation: `AccessToken` (15–60 min validity) + `RefreshToken` (7–30 days stored in DB).
  - Default role assigned: `Customer`.
  - Forgot password flow: Verify email via 6-digit OTP, then reset password.
- **Business Rules**:
  - Email is strictly unique across the system.
  - Lock account temporarily for 15 minutes after 5 consecutive failed login attempts.

### REQ-02: User Profile Management (Quản lý Hồ sơ)
- **Actors**: Authenticated Users
- **Requirements**:
  - Update Full Name, Avatar URL (uploaded to Cloudinary), Default Delivery Address.
  - Email change requires 6-digit OTP verification on the new email address.

### REQ-03: Store Onboarding (Đăng ký Cửa hàng & KYC)
- **Actors**: Customer, Admin
- **Requirements**:
  - Customer submits store application: Store Name, Contact Name (custom), Contact Email (custom), Phone, Full Address, Address Type (`OLD` using delivery address or `NEW` custom address), GPS Coordinates (`Latitude`, `Longitude`), Established Date, Tax Code, Shop Media URLs (multiple photos & videos), Business License photo, ID Card photos (front & back).
  - Application stored with status `PENDING`.
  - User can view their submitted application (`GET /api/users/seller-application/me`), edit while pending or resubmit when rejected (`PUT /api/users/seller-application/me`), and cancel application (`DELETE /api/users/seller-application/me`).
  - Admin reviews via dedicated endpoints (`GET /api/admin/kyc-requests`, `GET /pending`, `GET /{id}`).
  - Admin approves $\rightarrow$ User upgraded to `Seller`, shop pin appears on Thrift Map.
  - Admin rejects $\rightarrow$ Reason provided, notification sent to user for resubmission.
- **Business Rules**:
  - Each `Customer` can have at most 1 active pending store application at any given time.
  - If rejected, user can update the existing application with corrected proof and resubmit, which resets status to `PENDING` and clears previous rejection reasons.
  - Only verified `Seller` (`KYC_APPROVED`) accounts can list store products and appear on Thrift Map.

---

## Part 2: Thrift Map & Store Discovery

### REQ-04: Thrift Map Discovery (Bản đồ định vị)
- **Actors**: Guest, Customer, Seller
- **Requirements**:
  - HTML5 Geolocation API / Leaflet determines user coordinates; defaults to HCMC District 1 if permission denied.
  - Query nearby shops by radius (default 5km; selectable: 1km, 3km, 5km, 10km) using spatial distance calculation.
  - Visual map markers with interactive popup: Store name, avatar, brief address, rating, "View Store" button.
- **Business Rules**:
  - Only stores with `KYC_APPROVED` and `IsActive = true` appear on the map.

### REQ-05: Store & Style Filtering (Bộ lọc chuyên sâu)
- **Actors**: Guest, Customer
- **Requirements**:
  - Filter by Fashion Style Tags: `Vintage`, `Y2K`, `Streetwear`, `Classic/Retro`, `Minimalism`, `Luxury/Brand`.
  - Filter by Store Type: Flea Market Stall (*Sạp chợ truyền thống*), Consignment Chain (*Chuỗi ký gửi*), Boutique (*Tiệm độc lập*).
  - Filter by Price Bracket: <50k, 50k–150k, 150k–300k, >300k.
  - Sort by: Distance (Nearest), Rating (Highest), Newly Joined.
- **Business Rules**:
  - Filters are combined using logical `AND` conditions.

### REQ-06: Store Profile, Reviews & Product Detail (Chi tiết Cửa hàng & Sản phẩm)
- **Actors**: Guest, Customer, Seller
- **Requirements**:
  - View store overview, opening hours, Google Maps directions button, average rating.
  - View single product detail (`GET /api/products/{productId}`): images, size, condition %, price, style tags, store & owner summary (supports `AVAILABLE`, `LOCKED`, `SOLD`).
  - Paginated catalog of store clothing items (12 or 24 items/page).
  - Product statuses: `AVAILABLE`, `LOCKED`, `SOLD`. Only `AVAILABLE` shown to standard buyers.
  - Store Reviews: Users with completed transaction can post 1–5 stars + text + up to 3 photos (`POST /api/stores/{storeId}/reviews`).
  - Review Management: Public listing (`GET /api/stores/{storeId}/reviews`, sort by `newest`, `highest`, `lowest`), update review (`PUT`), and delete review (`DELETE`).
  - Store Spot Posts: Public listing of community spot-sharing posts tagging this store (`GET /api/stores/{storeId}/spot-posts`).
- **Business Rules**:
  - Each user can post at most 1 review per store (editable).
  - Store rating = average of all valid reviews (`RatingAverage` and `RatingCount` auto-recalculated on create/update/delete).

---

## Part 3: Thrift Forum & Community

### REQ-07: Create Pass Post (Đăng bài thanh lý đồ cá nhân)
- **Actors**: Customer, Seller
- **Requirements**:
  - Title (max 150 chars), detailed description (condition % new, flaws, brand, dimensions/size), pass price (> 0), style tags, 1–5 Cloudinary photos via `multipart/form-data` (`POST /api/forum/posts/pass`).
  - View single pass post detail (`GET /api/forum/posts/{id}`): image gallery, seller info, condition %, escrow availability.
  - Checkbox "Allow purchase via Escrow" (default: enabled).
  - Post status: `ACTIVE`, `LOCKED` (under escrow transaction), `SOLD`, `HIDDEN`.
- **Business Rules**:
  - Cannot edit price or description once post is `LOCKED` or `SOLD`.
  - Posts with status `HIDDEN` return 404 to non-admin users.

### REQ-08: Spot Sharing Post (Đăng bài chia sẻ tọa độ săn đồ)
- **Actors**: Customer, Seller
- **Requirements**:
  - Post outfit photo (1–5 Cloudinary images) and tag an existing store on the Thrift Map, or pin manual GPS coordinates via `multipart/form-data` (`POST /api/forum/posts/spot`).
  - View spot post detail (`GET /api/forum/posts/spot/{id}`).
  - Appears in community feed and in tagged store profile page (`GET /api/stores/{storeId}/spot-posts`).
- **Business Rules**:
  - Spot sharing posts do not have a price or escrow checkout.
  - Must provide either `TaggedStoreId` or both `Latitude` and `Longitude`.

### REQ-09: Community Feed & Interactions (Bảng tin & Tương tác)
- **Actors**: Guest, Authenticated Users
- **Requirements**:
  - Feed tabs (`GET /api/forum/feed`): Explore (`newest`), Pass items near you (`near-you`, GPS radius 1-10km), By Style (`by-style`).
  - 2-level hierarchical comments: Root comments and child replies (`POST /api/forum/posts/{id}/comments`), comment tree listing (`GET`), and soft delete (`DELETE /api/forum/comments/{commentId}`).
  - Reactions: Like / Heart toggle (`POST/GET /api/forum/posts/{id}/reactions`).
  - Report post (`POST /api/forum/posts/{id}/report`): Report violation for `PASS` or `SPOT` post (`SCAM`, `COUNTERFEIT`, `OFFENSIVE`, `PROHIBITED_CONTENT`, `OTHER`) with optional evidence URLs $\rightarrow$ queues for Admin review.
- **Business Rules**:
  - Unauthenticated or banned users cannot comment, react, or report.
  - Only author or Admin can delete comments.

### REQ-10: In-app Chat & Real-Time Messaging (Nhắn tin & Thẻ sản phẩm)
- **Actors**: Buyer, Seller
- **Requirements**:
  - 1-on-1 conversation creation or retrieval (`POST /api/chat/conversations`) between buyer and seller.
  - Real-time text messaging and interactive product card previews via SignalR Hub (`/hubs/chat`).
  - REST fallback endpoints for sending messages, sending product cards, and marking as read (`/api/chat`).
  - Real-time events: `ReceiveMessage`, `ReceiveProductCard`, `MessagesRead`, `UserTyping`.
  - Conversation inbox sorted by `LastMessageAt` DESC with unread indicators.
- **Business Rules**:
  - Users cannot initiate a chat with themselves.
  - Chat logs are preserved permanently for dispute arbitration.

---

## Part 4: Escrow & Order Processing

### REQ-11: Create Escrow Order (Khởi tạo đơn hàng ký quỹ)
- **Actors**: Buyer
- **Requirements**:
  - Enter shipping recipient info (name, phone, address).
  - Total calculation:
    $$\text{TotalAmount} = \text{ProductPrice} + \text{ShippingFee} + \text{EscrowFee}$$
    *(EscrowFee = 2% to 3% of ProductPrice).*
  - Lock item: Status transitions to `LOCKED` with a **15-minute expiration timer**.
  - Creates Order record with status `PENDING_PAYMENT`. Snapshot product name, price, thumbnail into `OrderItems`.
- **Business Rules**:
  - Buyers cannot purchase items they posted.
  - If unpaid after 15 minutes, order is automatically `CANCELLED` and product unlocked (`AVAILABLE`).

### REQ-12: Payment & Escrow Holding (Thanh toán tạm giữ)
- **Actors**: Buyer, PayOS/SePay
- **Requirements**:
  - Dynamic VietQR generated with unique payment reference: `DOSI <OrderId>`.
  - Secure webhook received: Verify signature, match order amount, update status to `ESCROW_HOLDING`.
  - In-process event notifies seller via SignalR to pack and ship.
- **Business Rules**:
  - Webhook processing must be strictly idempotent.

### REQ-13: Fulfillment & Shipping (Đóng gói & Vận chuyển)
- **Actors**: Seller
- **Requirements**:
  - Seller hands parcel to shipping carrier (GHTK, GHN, Viettel Post, etc.) and inputs carrier name + tracking code (+ optional package photo).
  - Order status updates to `IN_TRANSIT`.
- **Business Rules**:
  - Seller has a maximum of **48 hours** from `ESCROW_HOLDING` to provide tracking code.
  - After 48 hours without tracking, buyer can cancel for a 100% full refund.

### REQ-14: Inspection & Fund Release (Đồng kiểm & Giải ngân)
- **Actors**: Buyer, Background Worker
- **Requirements**:
  - Buyer inspects item; clicks "Received & Accepted" $\rightarrow$ order status becomes `COMPLETED_RELEASED`.
  - **Auto-complete Job**: If after **72 hours** in `IN_TRANSIT` buyer neither confirms nor disputes, background service automatically completes the order.
  - Payout calculation to seller wallet:
    $$\text{PayoutAmount} = \text{ProductPrice} + \text{ShippingFee} - \text{EscrowFee}$$
  - Product marked `SOLD` permanently.
- **Business Rules**:
  - Once `COMPLETED_RELEASED`, payout cannot be reversed via standard UI.

### REQ-15: Dispute & Refund Workflow (Khiếu nại & Tranh chấp)
- **Actors**: Buyer, Seller, Admin
- **Requirements**:
  - Buyer can file dispute within **24 hours** of delivery: Reason + mandatory unboxing video or min 2 photos of defects.
  - Order status becomes `DISPUTED` (funds frozen).
  - Seller can accept return $\rightarrow$ buyer returns parcel $\rightarrow$ seller confirms receipt $\rightarrow$ status `REFUNDED` (100% refund).
  - If contested $\rightarrow$ escalated to Admin Portal for arbitration.
- **Business Rules**:
  - Funds remain frozen during active dispute; neither party can withdraw.

---

## Part 5: Seller Tools, Notifications & Admin

### REQ-16: Seller Inventory & Dashboard (Quản lý kho hàng)
- **Actors**: Seller
- **Requirements**:
  - Add/edit products (images, condition %, price, style tags).
  - Order management tabs: Escrow Holding, In Transit, Completed, Disputed.
  - Revenue analytics: Completed sales, available balance, escrow-held funds.
- **Business Rules**:
  - Cannot modify price or delete products in `LOCKED`, `ESCROW_HOLDING`, or `IN_TRANSIT`.

### REQ-17: Bale-Opening Announcements (Gói thông báo khui kiện)
- **Actors**: Seller
- **Requirements**:
  - Seller creates announcement: Event title, banner photo, event date/time.
  - Pay broadcast fee (e.g., 50,000 VND) via VietQR PayOS.
  - Broadcast push notifications via SignalR to all users active within a **5km radius**.
- **Business Rules**:
  - Max 2 bale-opening announcements per shop per day.

### REQ-18: Notification Center (Trung tâm thông báo)
- **Actors**: All Users
- **Requirements**:
  - Real-time SignalR push for order updates, comments, reactions, nearby bale openings.
  - Unread badge counter, mark as read, persisted in database.

### REQ-19: Wallet Withdrawal (Rút tiền về tài khoản ngân hàng)
- **Actors**: Seller, Customer, Admin
- **Requirements**:
  - Request payout to bank: Bank name, account number, account holder name (must match KYC name), amount.
  - Minimum withdrawal: **50,000 VND**.
  - Admin reviews and approves payout.
- **Business Rules**:
  - Cannot submit a new withdrawal if one is already `PENDING`.

### REQ-20: Admin Portal (Cổng quản trị)
- **Actors**: Admin
- **Requirements**:
  - KYC Review: View store applications, photos, address, approve or reject with reason (`/api/admin/kyc-requests`).
  - Dispute Arbitration: Review unboxing evidence and seller response, decide refund buyer or release to seller (`/api/admin/disputes`).
  - Forum Moderation: View flagged post queue (`GET /api/admin/reports`), take disciplinary verdict (`PUT /api/admin/reports/{reportId}/resolve` with actions: `DISMISS`, `HIDE_POST`, `DELETE_POST`, `BAN_AUTHOR` and `adminNote`).
  - Withdrawal Processing: Review and approve/reject seller bank payouts (`/api/admin/withdrawals`).
  - Financial Analytics: GMV, platform escrow commission (2–3%), announcement package revenue (`/api/admin/analytics`).
- **Business Rules**:
  - All admin decisions (KYC, dispute, bans, report resolutions) are audit-tracked.
