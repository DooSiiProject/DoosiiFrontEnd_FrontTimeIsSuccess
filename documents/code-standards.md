# Doosii - Coding Standards & Team Guidelines

## 1. Git Workflow & Collaboration
- **Branch Strategy**:
  - `main`: Production-ready, stable releases.
  - `develop`: Shared integration branch where finished features are tested together.
  - Feature / Member Branches: Individual branches (e.g., `DooSii-Wee`, `DooSii-Tri`, `DooSii-FE`).
- **Merge Lifecycle**:
  ```
  [Member Branch: DooSii-Wee] ──(PR & Review)──► [develop] ──(Tested Release)──► [main]
  [Member Branch: DooSii-Tri] ──(PR & Review)──► [develop]
  [Frontend Branch: DooSii-FE] ─(PR & Review)──► [develop]
  ```
- **Commit Message Conventions (Conventional Commits)**:
  - `feat: <description>` (New feature)
  - `fix: <description>` (Bug fix)
  - `docs: <description>` (Documentation updates)
  - `refactor: <description>` (Code restructuring without feature changes)
  - `test: <description>` (Adding or modifying unit tests)
  - `chore: <description>` (Build, config, or dependency changes)

---

## 2. Backend C# & .NET Standards
- **Naming Conventions**:
  - **PascalCase**: Classes, Methods, Properties, Interfaces (`IAuthService`, `CreateEscrowOrder`).
  - **camelCase**: Local variables, method parameters (`userId`, `productPrice`).
  - **_camelCase**: Private class fields (`_dbContext`, `_authService`).
- **Layer Responsibility**:
  - **`Doosii.API`**: Controllers only handle request validation, model binding, and HTTP status codes. No business logic inside controllers.
  - **`Doosii.BLL`**: Houses all business logic, orchestration, DTOs, interfaces, and domain rules.
  - **`Doosii.DAL`**: Manages DbContext, EF Core configurations, entity models, and migrations.
- **Async/Await**:
  - All I/O operations (Database, HTTP clients, File storage) must be strictly asynchronous (`async Task<T>` with `await`).
  - Avoid synchronous blocking calls (`.Result`, `.Wait()`).
- **Dependency Injection**:
  - Register services via scoped lifetime (`builder.Services.AddScoped<IOrderService, OrderService>()`) unless explicitly required as Singleton or Transient.

---

## 3. Standardized API Responses
To ensure seamless integration between the React frontend and ASP.NET Core backend, endpoints should follow a predictable response structure:

```json
{
  "success": true,
  "message": "Order created successfully",
  "data": { ... },
  "errors": []
}
```

Error response example:
```json
{
  "success": false,
  "message": "Product is already locked by another buyer",
  "data": null,
  "errors": ["PRODUCT_LOCKED_ERROR"]
}
```

---

## 4. Frontend Standards (React + Vite)
- Functional components with React Hooks exclusively.
- Centralized API calls via an Axios instance with interceptors attaching `Authorization: Bearer <token>` and handling 401 token refresh automatically.
- Tailwind CSS utility classes used for styling; avoid arbitrary hardcoded inline styles.
- Environment variables configured via `.env` files (e.g. `VITE_API_BASE_URL`, `VITE_CLOUDINARY_UPLOAD_PRESET`).
