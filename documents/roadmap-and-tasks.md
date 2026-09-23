# Doosii - Roadmap & Task Tracker (Backend Team Assignment)

## 1. Overview & Team Allocation Strategy

The backend workload is divided **50 / 50** between **Wee** and **Tri** based on **Domain Schema Boundaries** to allow parallel, independent development with zero blocking dependencies:
- **Tri's Domain**: `store` and `forum` schemas (Discovery, Thrift Map, Inventory, Community Feed, Cloudinary, Chat Hub).
- **Wee's Domain**: `order` and `notification` schemas (Escrow State Machine, PayOS VietQR, Background Workers, Wallets, Admin Arbitration, Notification Hub).

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
   Wee can test with a mock/stub while Tri builds the full store catalog.
3. **Migration Coordination**: Each developer specifies migration names clearly (e.g. `AddStoreAndForumSchema` by Tri, `AddOrderAndEscrowSchema` by Wee).
4. **Git Branching**:
   - Tri works on `DooSii-Tri` $\rightarrow$ PR to `develop`.
   - Wee works on `DooSii-Wee` $\rightarrow$ PR to `develop`.

---

## 3. Project Phase Overview (EXE201 Milestones)

| Phase | Duration | Focus Area | Status |
|---|---|---|---|
| **Phase 1** | Weeks 1–2 | Requirements, Documentation, Architecture & Figma UI | 🟡 In Progress |
| **Phase 2** | Weeks 3–6 | Core Backend (Auth, Schemas, Thrift Map & Store Inventory) | ⚪ Upcoming |
| **Phase 3** | Weeks 7–10 | Escrow Order Processing, PayOS, React Frontend, SignalR | ⚪ Upcoming |
| **Phase 4** | Weeks 11–12 | Dispute Workflows, Functional & Security Testing | ⚪ Upcoming |
| **Phase 5** | Week 13+ | Pilot Launch (Ho Chi Minh City) & Maintenance | ⚪ Upcoming |

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
- [x] `[Tri]` Store KYC application submission API (`POST /api/users/seller-application`) `[Completed]`

---

### Module 2: Thrift Map & Stores [Track 1: Tri]
*Domain Schema: `store`*
- [x] `[Tri]` EF Core Entities: `Store`, `Category`, `Product`, `ProductImage`, `StoreLocation` `[Completed]`
- [ ] `[Tri]` Cloudinary upload helper service for multi-image uploads (1–5 photos)
- [x] `[Tri]` Store CRUD & Product listing API (`AVAILABLE`, `LOCKED`, `SOLD`) `[Completed]`
- [x] `[Tri]` Geolocation query API (`/api/map/nearby-stores`) using Haversine formula (1km, 3km, 5km, 10km) `[Completed]`
- [x] `[Tri]` Store & style filtering (Vintage, Y2K, Streetwear, price range, store type) `[Completed]`
- [x] `[Tri]` Store detail page & direction link API `[Completed]`
- [ ] `[Tri]` Store reviews & 1–5 star rating API

---

### Module 3: Thrift Forum & Community [Track 1: Tri]
*Domain Schema: `forum`*
- [ ] `[Tri]` EF Core Entities: `PassPost`, `PostImage`, `SpotSharingPost`, `Comment`, `Reaction`
- [ ] `[Tri]` Create Pass Post API (1–5 Cloudinary photos, size, condition %, price, allowEscrow)
- [ ] `[Tri]` Spot-sharing post API (GPS tag or link to Thrift Map store)
- [ ] `[Tri]` Community feed API with pagination (Newest, Near You, By Style)
- [ ] `[Tri]` 2-level comment thread API & Like/Heart reaction toggles
- [ ] `[Tri]` Flag / Report post API (sends to moderation queue)
- [ ] `[Tri]` SignalR Chat Hub (`/hubs/chat`) for 1-on-1 buyer/seller chat with product card preview

---

### Module 4: Escrow & Order Processing [Track 2: Wee]
*Domain Schema: `order`*
- [x] `[Wee]` EF Core Entities: `Order`, `OrderItem`, `EscrowTransaction`, `PaymentLog`, `Dispute`, `WithdrawalRequest` `[Completed]`
- [x] `[Wee]` Implement `IProductLockService` contract to reserve items during checkout `[Completed]`
- [x] `[Wee]` Create Escrow Order API (`POST /api/orders/create-escrow`) with 2.5% fee calculation & order tracking (`GET /api/orders/{id}`, `/my-purchases`, `/my-sales`) `[Completed]`
- [ ] `[Wee]` PayOS / SePay dynamic VietQR code generator (`DOSI <OrderId>`)
- [ ] `[Wee]` Secure PayOS Webhook endpoint: HMAC signature validation, idempotency, move to `ESCROW_HOLDING`
- [ ] `[Wee]` Seller shipping carrier & tracking code update API (move to `IN_TRANSIT`)
- [ ] `[Wee]` Buyer "Received & Accepted" confirmation API (move to `COMPLETED_RELEASED` + credit seller wallet)
- [ ] `[Wee]` **.NET `BackgroundService`**:
  - Auto-cancel unpaid orders after 15 minutes (unlock product)
  - Auto-complete orders after 72 hours in `IN_TRANSIT` if no dispute is opened

---

### Module 5: Seller Tools, Wallets & Admin Portal [Track 2: Wee]
*Domain Schema: `order` & `auth` (KYC)*
- [ ] `[Wee]` Seller wallet balance tracking & transaction ledger API
- [ ] `[Wee]` Bank withdrawal request API (minimum 50,000 VND)
- [ ] `[Wee]` Buyer dispute filing API (within 24h of delivery with mandatory unboxing video/photos)
- [ ] `[Wee]` Admin KYC application review & approval/rejection API
- [ ] `[Wee]` Admin Dispute Arbitration API (refund buyer vs. release funds to seller)
- [ ] `[Wee]` Financial analytics API (GMV, escrow fee platform revenue, withdrawal logs)
- [ ] `[Wee]` Bale-opening announcement purchase & payment flow
- [ ] `[Wee]` SignalR Notification Hub (`/hubs/notifications`) for order state changes & wallet alerts
