# Doosii - Codebase Summary & Local Setup

## 1. Project Directory Map
```
src/
├── Doosii.sln                     # Visual Studio Solution file
├── documents/                     # Project technical & domain documentation
│   ├── project-overview.md        # Vision, value prop, actors, and EXE201 roadmap
│   ├── system-architecture.md     # Modular monolith design, schemas, escrow flow
│   ├── api-spec.md                # REST & SignalR endpoint catalog
│   ├── code-standards.md          # Git workflow, C# / React conventions, response wrapper
│   └── codebase-summary.md        # Directory map & setup guide (this file)
│
├── Doosii.API/                    # Web API Host (Controllers, Swagger, Middleware, BackgroundServices)
│   ├── BackgroundServices/
│   │   └── OrderEscrowBackgroundService.cs # Periodic background worker (auto-cancel 15m, auto-complete 72h)
│   ├── Controllers/
│   │   ├── AuthController.cs      # Endpoints: /register, /login, /google, /refresh, /forgot-password, /verify-otp, /reset-password, /me
│   │   ├── OrdersController.cs    # Endpoints: /create-escrow, /{id}, /my-purchases, /my-sales, /{id}/ship, /{id}/confirm-received
│   │   ├── PaymentsController.cs  # Endpoints: /payos-qr, /order/{id}/status, /webhook
│   │   ├── UsersController.cs     # Endpoints: /profile, /seller-application, /seller-application/status
│   │   ├── StoresController.cs    # Endpoints: /stores, /{storeId}, /{storeId}/products
│   │   ├── ProductsController.cs  # Endpoints: /{productId} (PUT, DELETE)
│   │   ├── MapController.cs       # Endpoints: /nearby-stores (Haversine spatial calculation)
│   │   ├── SellerWalletController.cs # Endpoints: /seller/wallet, /seller/wallet/withdraw, /seller/wallet/withdrawals
│   │   ├── AnnouncementsController.cs # Endpoints: /seller/announcements, /announcements/upcoming
│   │   ├── NotificationsController.cs # Endpoints: /notifications, /unread-count, /{id}/read, /read-all
│   │   └── AdminController.cs     # Endpoints: /admin/kyc-requests, /admin/disputes, /admin/withdrawals, /admin/analytics
│   ├── Properties/
│   │   └── launchSettings.json    # http: 5241, https: 7117
│   ├── appsettings.json           # Connection string, JWT secret, Google ClientId, PayOS config
│   └── Program.cs                 # DI configuration, EF migration, SignalR Hub mapping, pipeline setup
│
├── Doosii.BLL/                    # Business Logic Layer
│   ├── Common/
│   │   └── ApiResponse.cs         # Unified API response wrapper { success, message, data, errors }
│   ├── DTOs/                      # Data Transfer Objects (Auth, Orders, Payments, Stores, Products, Users, KYC, Map, Wallet, Disputes, Admin, Notifications, Announcements)
│   ├── Helpers/
│   │   └── GeoCalculator.cs       # Haversine distance calculator for Thrift Map radius
│   ├── Hubs/
│   │   └── NotificationHub.cs     # SignalR Real-time Hub (/hubs/notifications) for push alerts & broadcasts
│   ├── Interfaces/                # Contracts (IAuthService, IEmailService, IProductLockService, IOrderService, IPaymentService, IWalletService, IAdminService, IUserService, IStoreService, IProductService, INotificationService, IAnnouncementService)
│   └── Services/                  # Implementations (AuthService, EmailService, MockProductLockService, OrderService, PaymentService, WalletService, AdminService, UserService, StoreService, ProductService, NotificationService, AnnouncementService)
│
└── Doosii.DAL/                    # Data Access Layer
    ├── Data/
    │   └── AppDbContext.cs        # EF Core DbContext with auth, store, order, and notification schemas
    ├── Migrations/                # EF Core Code-First migration history (Auth, Store, Escrow, Notification)
    └── Models/                    # Database entity models
        ├── User.cs, RefreshToken.cs, EmailOtp.cs, MerchantProfile.cs
        ├── Store/ (Store.cs, Category.cs, Product.cs, ProductImage.cs, StoreLocation.cs, BaleAnnouncement.cs)
        ├── Notification/ (Notification.cs)
        └── Order/ (Order.cs, OrderItem.cs, EscrowTransaction.cs, PaymentLog.cs, Dispute.cs, WithdrawalRequest.cs)
```

---

## 2. Technical Environment & Prerequisites
- **.NET SDK**: .NET 9.0 (compatible with installed SDK 10.0.401).
- **Database**: Microsoft SQL Server 2022 (Default: `localhost`, Database: `DoosiiDb`).
- **Authentication**: SQL Server Authentication (`sa` / `12345`) or Windows Authentication (`Trusted_Connection=True`).

---

## 3. How to Run Locally

### Step 1: Verify Connection String
Check `Doosii.API/appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=DoosiiDb;User Id=sa;Password=12345;TrustServerCertificate=True;"
}
```

### Step 2: Build the Solution
```powershell
dotnet build
```

### Step 3: Start the Backend API
```powershell
cd Doosii.API
dotnet run --launch-profile "http"
```
*Note: `Program.cs` automatically executes `dbContext.Database.Migrate()` on startup, creating the `DoosiiDb` database and executing pending migrations.*

### Step 4: Explore the Endpoints via Swagger
Open your browser and navigate to:
👉 **`http://localhost:5241/swagger`**
