# Doosii - AI Agent Instructions & Operating Rules

This document serves as the **Master Constitution** for any AI Agent working in this repository. Read and follow these rules strictly before modifying code or running commands.

---

## 1. Core Architectural Constraints
- **Pattern**: Strictly follow the **Modular Monolith** architecture with **In-Process communication**.
- **No Premature Complexity**: Do NOT introduce gRPC, RabbitMQ, Kafka, or microservices. Use C# interfaces for synchronous operations, MediatR for in-process domain events, and `.NET BackgroundService` for background timers.
- **Single Database**: All entities must reside in `DoosiiDb` on SQL Server 2022, cleanly separated by schemas: `auth`, `store`, `order`, `forum`, `notification`.

---

## 2. Technology & Environment Rules
- **Backend**: .NET 9.0 (C# 13).
- **ORM**: Entity Framework Core 9.
- **Database Engine**: Microsoft SQL Server 2022.
- **Database Connection String**:
  ```json
  "DefaultConnection": "Server=localhost;Database=DoosiiDb;User Id=sa;Password=12345;TrustServerCertificate=True;"
  ```
  *Never overwrite or break the existing `sa` / `12345` connection string.*
- **Frontend**: React + Vite, Tailwind CSS, Axios.

---

## 3. Coding & Design Standards
- **3-Tier Separation**:
  - `Doosii.API`: Controllers, request validation, routing, Swagger. No business logic.
  - `Doosii.BLL`: Business logic, DTOs, service implementations, JWT handling.
  - `Doosii.DAL`: DbContext, EF Core model configurations, migrations.
- **Standard Unified API Response**:
  All new endpoints should return responses wrapped in this standard structure:
  ```json
  {
    "success": true,
    "message": "Human readable message",
    "data": { ... },
    "errors": []
  }
  ```
- **Async/Await**: All database and I/O operations must be strictly asynchronous (`async Task<T>` / `await`). Never block threads using `.Result` or `.Wait()`.

---

## 4. Business Guardrails
- **Escrow Platform Fee**: 2% to 3% of product price deducted from seller payout.
- **Order Timeouts**:
  - Unpaid order in `PENDING_PAYMENT` expires in **15 minutes** (unlocks item).
  - Seller has **48 hours** to provide shipping tracking code after payment.
  - Buyer has **72 hours** after delivery before auto-complete completes payout.
  - Buyer has **24 hours** after delivery to file a dispute with mandatory unboxing proof.
- **Spam Prevention**: Max 10 pass posts per customer per 24 hours.
- **KYC Requirement**: Only users with role `Seller` and `KYC_APPROVED` can appear on the Thrift Map and list store inventory.

---

## 5. Agent Verification Mandate
Before reporting any backend coding task as complete:
1. Always run `dotnet build` from `c:\FPTU\Semester\8\EXE201\project\src` and verify zero errors and warnings.
2. If adding or modifying entities, create or test EF Core migrations carefully.
3. Preserve all existing working code, comments, and connection strings.
