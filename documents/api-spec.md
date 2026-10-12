# Doosii - API Specification

## 1. Overview & Conventions
- **Base URL**: `http://localhost:5241/api` (or `https://localhost:7117/api`)
- **Swagger Documentation**: `/swagger`
- **Authentication**: Bearer Token via HTTP Header: `Authorization: Bearer <access_token>`
- **Response Format**: JSON standard format

---

## 2. API Modules Breakdown

### 2.1. Authentication & User Profile (`/api/auth`, `/api/users`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `POST` | `/api/auth/register` | Public | Register with email, password, full name, birthdate (default role: `Customer`) |
| `POST` | `/api/auth/login` | Public | Login with email & password, returns JWT token |
| `POST` | `/api/auth/google` | Public | Sign in / register with Google OAuth payload |
| `POST` | `/api/auth/refresh` | Public | Refresh expired access token using refresh token |
| `POST` | `/api/auth/forgot-password` | Public | Send 6-digit OTP to user's registered email |
| `POST` | `/api/auth/verify-otp` | Public | Verify 6-digit OTP code before proceeding to reset password |
| `POST` | `/api/auth/reset-password` | Public | Reset password using verified OTP |
| `GET` | `/api/auth/me` | Authenticated | Retrieve authenticated user's profile |
| `PUT` | `/api/users/profile` | Authenticated | Update full name, avatar URL (Cloudinary), delivery address |
| `POST` | `/api/users/seller-application` | Customer | Submit store application (name, contact name, email, phone, address, addressType [OLD/NEW], coordinates, establishedDate, taxCode, shopMediaUrls, ID card front/back) |
| `GET` | `/api/users/seller-application/me` | Customer | Retrieve submitted store application of the current authenticated user |
| `PUT` | `/api/users/seller-application/me` | Customer | Edit / resubmit store application when status is `PENDING` or `REJECTED` |
| `DELETE` | `/api/users/seller-application/me` | Customer | Cancel / remove submitted store application while in `PENDING` status |

---

### 2.2. Thrift Map & Store Discovery (`/api/stores`, `/api/products`, `/api/map`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/map/nearby-stores` | Public | Search stores by radius (`lat`, `lng`, `radiusKm` [1, 3, 5, 10]) |
| `GET` | `/api/stores` | Public | Filter stores by style tags, rating, price segment, and sort by distance/rating/newest |
| `POST` | `/api/stores` | Seller (KYC) | Create a new thrift store (Owner must be KYC Approved) |
| `GET` | `/api/stores/{storeId}` | Public | Store details, opening hours, coordinates, rating summary |
| `PUT` | `/api/stores/{storeId}` | Store Owner | Update store details (name, description, phone, address, hours, coordinates) |
| `GET` | `/api/stores/{storeId}/products` | Public | Paginated list of store products (`AVAILABLE` only, 12 or 24 per page) |
| `POST` | `/api/stores/{storeId}/products` | Store Owner | Add new clothing item to store inventory with images, size, condition % |
| `GET` | `/api/products/{productId}` | Public | Get product details (title, description, price, size, condition %, images, store/seller info, status: `AVAILABLE`, `LOCKED`, `SOLD`) |
| `PUT` | `/api/products/{productId}` | Product Owner | Update product title, price, description, images (cannot edit if `LOCKED` or `SOLD`) |
| `DELETE` | `/api/products/{productId}` | Product Owner | Remove product from listing (cannot delete if `LOCKED` or `SOLD`) |
| `POST` | `/api/stores/{storeId}/reviews` | Buyer | Add rating (1–5 stars), review text, and photos (completed order required, max 1 review/user/store) |
| `GET` | `/api/stores/{storeId}/reviews` | Public | List store reviews with pagination (`page`, `pageSize`, sort: `newest`, `highest`, `lowest`) |
| `PUT` | `/api/stores/{storeId}/reviews/{reviewId}` | Review Author | Update star rating, comment text, and review photos |
| `DELETE` | `/api/stores/{storeId}/reviews/{reviewId}` | Author / Admin | Delete store review, automatically recalculates store average rating |
| `GET` | `/api/stores/{storeId}/spot-posts` | Public | List active community spot-sharing posts tagging this store (paginated) |

---

### 2.3. Community & Forum (`/api/forum`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/forum/feed` | Public | Unified community feed (PassPost & SpotSharing). Query: `tab` (`newest`, `near-you`, `by-style`), `style`, `lat`, `lng`, `radiusKm` (1, 3, 5, 10), `page`, `pageSize` |
| `POST` | `/api/forum/posts/pass` | Authenticated | Create pass post with 1–5 images via `multipart/form-data` (`title`, `description`, `price`, `size`, `conditionPercent`, `allowEscrow`, `styleTags`, `images`) |
| `GET` | `/api/forum/posts/{id}` | Public | Get pass post detail by ID (images, author info, condition %, price, status) |
| `POST` | `/api/forum/posts/spot` | Authenticated | Create spot-sharing post via `multipart/form-data` (`taggedStoreId` or `lat`/`lng`, `locationName`, `content`, 1–5 `images`) |
| `GET` | `/api/forum/posts/spot/{id}` | Public | Get spot-sharing post detail by ID (images, tagged store or GPS, author, content) |
| `POST` | `/api/forum/posts/{id}/comments` | Authenticated | Add root comment or nested reply (`content`, optional `parentCommentId` for max 2-level hierarchy) |
| `GET` | `/api/forum/posts/{id}/comments` | Public | Get comment thread as a tree hierarchy (root comments with nested replies) |
| `DELETE` | `/api/forum/comments/{commentId}` | Author / Admin | Soft-delete a comment (`IsDeleted = true`) |
| `POST` | `/api/forum/posts/{id}/reactions` | Authenticated | Toggle reaction on post (`ReactionType`: `LIKE` or `HEART`) |
| `GET` | `/api/forum/posts/{id}/reactions` | Public | Get reaction counts (total likes, hearts, and current user's active reaction) |
| `POST` | `/api/forum/posts/{id}/report` | Authenticated | Report post for moderation (`postType`: `PASS` or `SPOT`, `reason`: `SCAM`, `COUNTERFEIT`, `OFFENSIVE`, `PROHIBITED_CONTENT`, `OTHER`, `description`, `evidenceUrls`) |

---

### 2.4. Media Uploads & Cloudinary (`/api/uploads`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `POST` | `/api/uploads/image` | Authenticated | Upload single image to Cloudinary via `multipart/form-data`. Whitelist folder: `products`, `stores`, `kyc`, `reviews`, `avatars`, `forum` (max 5 MB) |
| `POST` | `/api/uploads/images` | Authenticated | Upload batch of 1–5 images via `multipart/form-data` (max 5 MB/image, 30 MB request limit) |
| `DELETE` | `/api/uploads/{*publicId}` | Authenticated | Delete image asset from Cloudinary by its public ID |

---

### 2.5. In-App Chat & Messaging (`/api/chat`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `POST` | `/api/chat/conversations` | Authenticated | Get or create 1-on-1 conversation between buyer and seller (optional `productId` to pre-link product) |
| `GET` | `/api/chat/conversations` | Authenticated | List all active conversations for current user, sorted by `LastMessageAt` DESC (paginated) |
| `GET` | `/api/chat/conversations/{id}/messages` | Participant | Paginated message history for a conversation (newest messages first) |
| `PUT` | `/api/chat/conversations/{id}/read` | Participant | Mark received messages as read (emits `MessagesRead` event to SignalR group) |
| `POST` | `/api/chat/conversations/{id}/messages` | Participant | Send text message via REST fallback (emits `ReceiveMessage` to SignalR group) |
| `POST` | `/api/chat/conversations/{id}/product-card` | Participant | Send product card preview message via REST fallback (emits `ReceiveProductCard` to SignalR group) |

---

### 2.6. Orders & Escrow Processing (`/api/orders`, `/api/escrow`, `/api/payments`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `POST` | `/api/orders/create-escrow` | Customer | Create escrow order, lock item for 15 mins, calculate total amount + 2-3% fee |
| `POST` | `/api/payments/payos-qr` | Customer | Generate dynamic VietQR code with order ID syntax (`DOSI <OrderId>`) |
| `GET` | `/api/payments/order/{orderId}/status` | Authenticated | Check real-time payment and escrow status of an order |
| `POST` | `/api/payments/webhook` | Webhook (PayOS) | Handle payment confirmation, verify signature, transition order to `ESCROW_HOLDING` |
| `POST` | `/api/orders/{id}/ship` | Seller | Submit shipping carrier and tracking code (moves to `IN_TRANSIT`) |
| `POST` | `/api/orders/{id}/confirm-received` | Buyer | Confirm receipt of item, release funds to seller wallet (`COMPLETED_RELEASED`) |
| `POST` | `/api/orders/{id}/dispute` | Buyer | File dispute within 24h with unboxing video / photos (moves to `DISPUTED`) |
| `GET` | `/api/orders/{id}/dispute` | Authenticated | View dispute details of an order |
| `GET` | `/api/orders/my-purchases` | Customer | Order history of bought items with status tracking |
| `GET` | `/api/orders/my-sales` | Seller/Customer | Orders sold, pending fulfillment, or in escrow |

---

### 2.7. Seller Dashboard & Tools (`/api/seller`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/seller/inventory` | Seller | Manage listed items (CRUD, status: `AVAILABLE`, `LOCKED`, `SOLD`) |
| `POST` | `/api/seller/products` | Seller | Add new store clothing item with images, size, and condition |
| `POST` | `/api/seller/announcements` | Seller | Purchase a bale-opening broadcast ("khui kiện") (max 2/day, 50k VND fee via WALLET or PayOS) |
| `GET` | `/api/seller/announcements` | Seller | View all bale-opening announcements created for the seller's store |
| `GET` | `/api/announcements/upcoming` | Public | View active upcoming bale-opening announcements across all stores |
| `GET` | `/api/seller/wallet` | Seller/Customer | Check available balance, escrow-locked balance, and transaction history |
| `POST` | `/api/seller/wallet/withdraw` | Seller/Customer | Request withdrawal to personal bank account (minimum 50,000 VND) |
| `GET` | `/api/seller/wallet/withdrawals` | Seller/Customer | View history of personal bank withdrawal requests |

---

### 2.8. Admin Portal (`/api/admin`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/admin/kyc-requests` | Admin | Review store applications (supports query `status=PENDING/APPROVED/REJECTED`) |
| `GET` | `/api/admin/kyc-requests/pending` | Admin | Quick query for pending store applications awaiting review |
| `GET` | `/api/admin/kyc-requests/{id}` | Admin | View complete details of a store application |
| `PUT` | `/api/admin/kyc-requests/{id}/approve` | Admin | Approve store application, automatically promoting user to `Seller` |
| `PUT` | `/api/admin/kyc-requests/{id}/reject` | Admin | Reject store application with specified reason |
| `POST` | `/api/admin/kyc-requests/{id}/verdict` | Admin | Approve or reject store with explanation (legacy/backward compatibility) |
| `GET` | `/api/admin/disputes` | Admin | Review disputed escrow orders with unboxing proof |
| `POST` | `/api/admin/disputes/{id}/arbitrate` | Admin | Rule in favor of Buyer (refund) or Seller (release funds) |
| `GET` | `/api/admin/withdrawals` | Admin | List pending withdrawal requests from sellers |
| `POST` | `/api/admin/withdrawals/{id}/process` | Admin | Approve or reject seller withdrawal request |
| `GET` | `/api/admin/reports` | Admin | Moderation queue for reported posts (filters: `status`, `postType`, `reason`, pagination) |
| `PUT` | `/api/admin/reports/{reportId}/resolve` | Admin | Resolve reported post violation (`action`: `DISMISS`, `HIDE_POST`, `DELETE_POST`, `BAN_AUTHOR`, with `adminNote`) |
| `GET` | `/api/admin/analytics` | Admin | GMV, escrow fee platform revenue, orders & users metrics |

---

### 2.9. In-App Notifications (`/api/notifications`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/notifications` | Authenticated | List current user notifications (supports `unreadOnly=true/false` filter, newest 50) |
| `GET` | `/api/notifications/unread-count` | Authenticated | Get total count of unread notifications for badge indicator |
| `PUT` | `/api/notifications/{id}/read` | Authenticated | Mark a single notification as read |
| `PUT` | `/api/notifications/read-all` | Authenticated | Mark all notifications of the current user as read |

---

### 2.10. Real-time Communication (SignalR Hubs)
#### A. Real-time Chat Hub (`/hubs/chat`)
- **Authentication**: JWT query string `?access_token={jwt}` (required).
- **Group Convention**: `conversation_{conversationId}` for 1-on-1 isolated messaging rooms.
- **Client Methods (Invoked by Frontend)**:
  - `JoinConversation(int conversationId)`: Join a conversation room.
  - `LeaveConversation(int conversationId)`: Leave a conversation room.
  - `SendMessage(int conversationId, string content)`: Send real-time text message.
  - `SendProductCard(int conversationId, int productId)`: Send interactive product card preview.
  - `MarkAsRead(int conversationId)`: Mark messages sent to current user as read.
  - `SendTyping(int conversationId, bool isTyping)`: Broadcast typing indicator.
- **Server Broadcasts (Listened by Frontend)**:
  - `ReceiveMessage(ChatMessageDto message)`: Receive incoming chat message in real time.
  - `ReceiveProductCard(ChatMessageDto message)`: Receive incoming product card.
  - `MessagesRead(MessagesReadStatusDto status)`: Notify when conversation messages are read.
  - `UserTyping(UserTypingDto typingInfo)`: Receive typing indicator.
  - `Error(string errorMessage)`: Error notification.

#### B. Notification Hub (`/hubs/notifications`)
- **Authentication**: JWT query string parameter `?access_token={jwt}` (for WebSocket connections).
- **Groups**:
  - Personal Group: `user_{userId}` for targeted order updates, wallet credits, and dispute alerts.
  - Broadcast Channel: `broadcast_channel` for platform announcements and nearby bale-opening broadcasts.
- **Client Listeners**:
  - `ReceiveNotification(NotificationDto)`: Real-time event for personal notification.
  - `ReceiveBroadcast(BroadcastPayload)`: Real-time event for broadcast announcement.
