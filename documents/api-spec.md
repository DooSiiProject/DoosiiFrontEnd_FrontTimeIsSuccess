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
| `POST` | `/api/users/seller-application` | Customer | Submit store KYC application (store name, phone, address, coordinates, facade photo, ID card) |
| `GET` | `/api/users/seller-application/status` | Customer | Check status of submitted KYC store application (`PENDING`, `APPROVED`, `REJECTED`) |

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
| `PUT` | `/api/products/{productId}` | Product Owner | Update product title, price, description, images (cannot edit if `LOCKED` or `SOLD`) |
| `DELETE` | `/api/products/{productId}` | Product Owner | Remove product from listing (cannot delete if `LOCKED` or `SOLD`) |
| `POST` | `/api/stores/{storeId}/reviews` | Buyer | Add rating (1–5 stars), review text, and photos for a shop |

---

### 2.3. Community & Forum (`/api/forum`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/forum/feed` | Public | Feed with tabs: Explore/Newest, Near You, By Style Tag (pagination) |
| `POST` | `/api/forum/posts/pass` | Authenticated | Create pass post (1–5 Cloudinary images, title, description, size, condition %, price, allowEscrow) |
| `POST` | `/api/forum/posts/spot` | Authenticated | Share a thrift spot/stall (tag store or GPS pin, photo, outfit review) |
| `GET` | `/api/forum/posts/{id}` | Public | Details of post, image gallery, comments, reaction counts |
| `POST` | `/api/forum/posts/{id}/comments` | Authenticated | Add root comment or nested reply (2-level hierarchy) |
| `POST` | `/api/forum/posts/{id}/reactions` | Authenticated | Toggle like/heart reaction |
| `POST` | `/api/forum/posts/{id}/report` | Authenticated | Report scam/fake/abusive post for admin review |

---

### 2.4. Orders & Escrow Processing (`/api/orders`, `/api/escrow`, `/api/payments`)
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

### 2.5. Seller Dashboard & Tools (`/api/seller`)
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

### 2.6. Admin Portal (`/api/admin`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/admin/kyc-requests` | Admin | Review pending store applications |
| `POST` | `/api/admin/kyc-requests/{id}/verdict` | Admin | Approve or reject store with explanation |
| `GET` | `/api/admin/disputes` | Admin | Review disputed escrow orders with unboxing proof |
| `POST` | `/api/admin/disputes/{id}/arbitrate` | Admin | Rule in favor of Buyer (refund) or Seller (release funds) |
| `GET` | `/api/admin/withdrawals` | Admin | List pending withdrawal requests from sellers |
| `POST` | `/api/admin/withdrawals/{id}/process` | Admin | Approve or reject seller withdrawal request |
| `GET` | `/api/admin/reports` | Admin | List flagged forum posts for moderation |
| `GET` | `/api/admin/analytics` | Admin | GMV, escrow fee platform revenue, orders & users metrics |

---

### 2.7. In-App Notifications (`/api/notifications`)
| Method | Endpoint | Access | Description |
|---|---|---|---|
| `GET` | `/api/notifications` | Authenticated | List current user notifications (supports `unreadOnly=true/false` filter, newest 50) |
| `GET` | `/api/notifications/unread-count` | Authenticated | Get total count of unread notifications for badge indicator |
| `PUT` | `/api/notifications/{id}/read` | Authenticated | Mark a single notification as read |
| `PUT` | `/api/notifications/read-all` | Authenticated | Mark all notifications of the current user as read |

---

### 2.8. Real-time Communication (SignalR Hubs)
- **Chat Hub**: `/hubs/chat`
  - Real-time 1-on-1 messaging between buyer and seller with attached product card.
- **Notification Hub**: `/hubs/notifications`
  - Authentication: JWT query string parameter `?access_token={jwt}` (for WebSocket connections).
  - Groups:
    - Personal Group: `user_{userId}` for targeted order updates, wallet credits, and dispute alerts.
    - Broadcast Channel: `broadcast_channel` for platform announcements and nearby bale-opening broadcasts.
  - Client Listeners:
    - `ReceiveNotification(NotificationDto)`: Real-time event for personal notification.
    - `ReceiveBroadcast(BroadcastPayload)`: Real-time event for broadcast announcement.
