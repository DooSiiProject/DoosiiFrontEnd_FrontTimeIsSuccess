# Doosii - System Architecture

## 1. Architectural Style: Modular Monolith
To ensure rapid development, ACID transactional consistency, low operational overhead, and no complex network latency for EXE201, Doosii adopts a **Modular Monolith** architecture pattern with **In-Process communication** (eliminating premature microservices complexity such as gRPC or RabbitMQ).

```
+-------------------------------------------------------------------------+
|                         Frontend Client (React + Vite)                  |
|                         Tailwind CSS | Redux / Axios                    |
+------------------------------------+------------------------------------+
                                     | HTTPS / WSS (SignalR)
                                     v
+-------------------------------------------------------------------------+
|                           Doosii Backend (.NET 8/9)                     |
|                                                                         |
|  +-------------------+  +-------------------+  +---------------------+  |
|  |   Auth & User     |  |   Store & Map     |  |   Order & Escrow    |  |
|  |     Module        |  |     Module        |  |      Module         |  |
|  +--------+----------+  +---------+---------+  +----------+----------+  |
|           |                       |                       |             |
|           |      In-Process Calls / MediatR Events        |             |
|           +-----------------------+-----------------------+             |
|                                   |                                     |
|  +--------------------+  +--------+---------+  +---------------------+  |
|  |   Forum & Community|  |   Notification   |  | .NET Background     |  |
|  |      Module        |  |  (SignalR Hubs)  |  | Service (Cron/Jobs) |  |
|  +--------------------+  +------------------+  +---------------------+  |
+------------------------------------+------------------------------------+
                                     | EF Core 8/9
                                     v
+-------------------------------------------------------------------------+
|                  Microsoft SQL Server 2022 (Single Database)            |
|  +---------------+ +---------------+ +---------------+ +---------------+|
|  |  schema: auth | | schema: store | | schema: order | | schema: forum | |
|  +---------------+ +---------------+ +---------------+ +---------------+|
|  |  schema: notification                                               ||
|  +---------------------------------------------------------------------+|
+-------------------------------------------------------------------------+
```

---

## 2. In-Process Module Communication
Rather than separate network services, modules interact cleanly in-process:
1. **Synchronous C# Service Interfaces**:
   - `OrderModule` calls `IStoreService.LockProduct(productId)` inside a database transaction to ensure a piece of clothing cannot be double-booked.
   - `OrderModule` calls `IAuthService.GetUserSummary(userId)` to validate buyer and seller KYC/balance.
2. **Asynchronous MediatR Domain Events**:
   - `OrderPaidDomainEvent` $\rightarrow$ triggers `NotificationModule` to send real-time SignalR notifications to the seller and instructs `StoreModule` to update product status.
   - `NewBaleOpeningEvent` $\rightarrow$ triggers geospatial query for users within 5km, dispatching instant SignalR broadcasts.
3. **Background Processing (.NET `BackgroundService` / `IHostedService`)**:
   - Runs periodic background tasks:
     - Automatically cancel unpaid orders after 15 minutes and unlock product (`AVAILABLE`).
     - Auto-complete orders 72 hours after marked as `IN_TRANSIT` if no dispute is filed, releasing escrow funds to seller.

---

## 3. Database Schema Strategy (Single DB - Schema Separation)
All tables reside within `DoosiiDb`, organized cleanly into logical database schemas:
- **`auth` schema**:
  - `Users` (`Id`, `Email`, `FullName`, `PasswordHash`, `Role`, `IsGoogle`, `CreatedAt`)
  - `RefreshTokens` (`Id`, `UserId`, `Token`, `ExpiresAt`, `IsRevoked`)
  - `MerchantProfiles` (`Id`, `UserId`, `StoreName`, `Phone`, `Address`, `Latitude`, `Longitude`, `KycStatus`, `LicenseImageUrl`, `FrontFacadeUrl`, `IdCardFrontUrl`, `IdCardBackUrl`)
- **`store` schema**:
  - `Stores` (`Id`, `OwnerId`, `Name`, `Description`, `OpeningHours`, `IsActive`, `RatingAverage`, `RatingCount`)
  - `StoreLocations` (`StoreId`, `Latitude`, `Longitude`, `Address`)
  - `Categories` (`Id`, `Name`, `Slug`)
  - `Products` (`Id`, `StoreId`, `Title`, `Description`, `Price`, `Size`, `ConditionPercent`, `Status` [AVAILABLE, LOCKED, SOLD], `StyleTags`)
  - `ProductImages` (`Id`, `ProductId`, `ImageUrl`, `DisplayOrder`)
  - `StoreReviews` (`Id`, `StoreId`, `UserId`, `Rating`, `Comment`, `Images`)
- **`order` schema**:
  - `Orders` (`Id`, `BuyerId`, `SellerId`, `TotalAmount`, `ProductPrice`, `ShippingFee`, `EscrowFee`, `Status` [PENDING_PAYMENT, ESCROW_HOLDING, IN_TRANSIT, COMPLETED_RELEASED, DISPUTED, REFUNDED, CANCELLED], `CreatedAt`)
  - `OrderItems` (`Id`, `OrderId`, `ProductId`, `ProductNameSnapshot`, `PriceSnapshot`, `ThumbnailSnapshot`)
  - `EscrowTransactions` (`Id`, `OrderId`, `Amount`, `Status`, `ReleasedAt`)
  - `PaymentLogs` (`Id`, `OrderId`, `Gateway` [PayOS/SePay], `TransactionCode`, `Amount`, `Payload`, `CreatedAt`)
  - `Disputes` (`Id`, `OrderId`, `RaisedByUserId`, `Reason`, `EvidenceVideoUrl`, `EvidenceImages`, `Status` [DISPUTED, RESOLVED_REFUND, RESOLVED_RELEASE], `AdminVerdict`)
  - `WithdrawalRequests` (`Id`, `UserId`, `BankName`, `AccountNumber`, `AccountHolder`, `Amount`, `Status`)
- **`forum` schema**:
  - `PassPosts` (`Id`, `AuthorId`, `Title`, `Description`, `Price`, `Size`, `ConditionPercent`, `AllowEscrow`, `Status` [ACTIVE, LOCKED, SOLD, HIDDEN], `CreatedAt`)
  - `PostImages` (`Id`, `PostId`, `ImageUrl`, `DisplayOrder`)
  - `SpotSharingPosts` (`Id`, `AuthorId`, `TaggedStoreId`, `Latitude`, `Longitude`, `LocationName`, `Content`, `Images`)
  - `Comments` (`Id`, `PostId`, `ParentCommentId`, `AuthorId`, `Content`, `CreatedAt`)
  - `Reactions` (`Id`, `PostId`, `UserId`, `ReactionType`, `CreatedAt`)
- **`notification` schema**:
  - `Notifications` (`Id`, `UserId`, `Title`, `Message`, `Type`, `TargetUrl`, `IsRead`, `CreatedAt`)
  - `UserConnections` (`Id`, `UserId`, `ConnectionId`, `LastActiveAt`)

---

## 4. Escrow State Machine Flow
```
[Buyer: Places Order]
       │
       ▼
[Status: PENDING_PAYMENT] ──(15 min timeout: Unpaid)──► [Status: CANCELLED] (Product unlocked)
       │
 (VietQR PayOS/SePay Webhook received)
       │
       ▼
[Status: ESCROW_HOLDING]
       │
 (Seller ships item & inputs tracking within 48h)
       │
       ▼
[Status: IN_TRANSIT]
       │
       ├─────────────────────────────────────────────┐
       │ (Buyer confirms OR 72h auto-complete)        │ (Buyer disputes within 24h)
       ▼                                             ▼
[Status: COMPLETED_RELEASED]                   [Status: DISPUTED]
  (Deduct 2-3% platform fee,                     (Freeze funds, Admin arbitrates:
   release payout to seller balance)               Refund Buyer OR Release Seller)
```

---

## 5. Technology Stack Summary
- **Frontend**: React (Vite), Tailwind CSS, Redux Toolkit / React Query, Axios, HTML5 Geolocation / Leaflet / Google Maps API.
- **Backend API**: ASP.NET Core (.NET 8/9), Entity Framework Core, BCrypt.Net-Next, SignalR.
- **Database**: Microsoft SQL Server 2022 (Single Database with Schema Separation).
- **Storage**: Cloudinary API (clothing pictures, unboxing proofs, storefronts).
- **Payments**: PayOS / SePay (dynamic VietQR integration with webhook validation).
