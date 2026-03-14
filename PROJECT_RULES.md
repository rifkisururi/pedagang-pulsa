# PROJECT RULES & DEVELOPMENT GUIDELINES
## PedagangPulsa.com — Admin Panel & Member API

**Version:** 1.0
**Date:** 2026-03-12
**Status:** ACTIVE — All development MUST follow these rules

---

## 1. MANDATORY RULES

### 1.1 PRD Compliance (CRITICAL)
- **ALWAYS** reference `PRD_v1.1.md` before implementing any feature
- No feature should be implemented that contradicts the PRD
- If PRD is ambiguous, ask user clarification BEFORE coding
- Sprint goals override individual preferences

### 1.2 Documentation Usage (CRITICAL)
- **ALWAYS** use **MCP Context7** for all library/framework documentation
- Never guess API usage or assume patterns
- For any third-party library (EF Core, Hangfire, SignalR, etc.):
  1. Call `mcp__context7__resolve-library-id` first
  2. Then call `mcp__context7__query-docs` for specific usage
- Context7 calls are limited to 3 per question — use efficiently

### 1.3 Build Verification (CRITICAL)
- **ALWAYS** run build after completing ANY code changes
- **MANDATORY**: Execute `dotnet build` before marking task as complete
- No code changes are considered complete without successful build verification
- Build must succeed without errors (warnings are acceptable if justified)

---

## 2. PROJECT STRUCTURE

### 2.1 Required Projects (per PRD Section 2.1)

```
PedagangPulsa.sln
├── PedagangPulsa.Web/          ← ASP.NET 10 MVC (Admin Panel) - PRIORITY
├── PedagangPulsa.Api/          ← ASP.NET 10 Web API (Member Mobile)
├── PedagangPulsa.Application/  ← Business Logic (shared)
├── PedagangPulsa.Domain/       ← Entities, Enums, Value Objects
├── PedagangPulsa.Infrastructure/← EF Core, Repository, External Services
└── PedagangPulsa.Tests/
```

### 2.2 Current Status
- ✅ `PedagangPulsa.Web` created (basic MVC template)
- ⏳ All other projects NEED to be created (Sprint 1)

---

## 3. TECHNOLOGY STACK (per PRD Section 2.2)

| Layer | Technology | Version |
|---|---|---|
| Web Framework | ASP.NET 10 MVC + Minimal API | .NET 10 |
| ORM | Entity Framework Core | 10 |
| Database | PostgreSQL | 16 |
| Caching | Redis | - |
| Background Job | Hangfire | - |
| Realtime | SignalR | - |
| Auth Admin | ASP.NET Core Identity + Cookie | - |
| Auth API | JWT Bearer Token | - |
| Frontend | Bootstrap 5.3 + Chart.js + DataTables | - |
| File Upload | MinIO / Local Storage | - |

---

## 4. SPRINT EXECUTION STRATEGY

### 4.1 Sprint Priority (per PRD Section 1.4)
**FOCUS: Admin Panel FIRST (Sprint 1-4), then Member API (Sprint 5-6)**

### 4.2 Sprint Breakdown
| Sprint | Focus | Story Points |
|---|---|---|
| **Sprint 1** | Foundation + Auth + Dashboard | 21 |
| **Sprint 2** | User & Product Management | 26 |
| **Sprint 3** | Supplier & Transaction Core | 34 |
| **Sprint 4** | Topup, Finance, Reports | 28 |
| **Sprint 5** | Member API Auth & Balance | 21 |
| **Sprint 6** | Member API Transaction | 26 |

---

## 5. CODING STANDARDS

### 5.1 C# Code Style
- Use `.NET 10` features where appropriate
- `ImplicitUsings` enabled
- `Nullable` enabled
- Follow C# naming conventions:
  - Classes: `PascalCase`
  - Methods: `PascalCase`
  - Properties: `PascalCase`
  - Local variables: `camelCase`
  - Private fields: `_camelCase`
  - Constants: `PascalCase`

### 5.2 Architecture Patterns
- **Clean Architecture**: Separate layers (Domain, Application, Infrastructure, Web/Api)
- **Repository Pattern**: For data access (in Infrastructure layer)
- **Service Pattern**: Business logic in Application layer
- **DTO Pattern**: Separate request/response DTOs
- **Strategy Pattern**: For supplier adapters (`ISupplierAdapter`)
- **Dependency Injection**: Constructor injection only

### 5.3 Database Rules
- Use **EF Core Migrations** for schema changes
- All database operations must use **transactions** for multi-step operations
- Use **stored functions** for balance operations (per PRD):
  - `hold_balance()`
  - `debit_held_balance()`
  - `release_held_balance()`
  - `debit_supplier_balance()`
  - `credit_supplier_balance()`
- Use **views** for reporting: `v_profit_daily`, `v_profit_by_supplier`, etc.
- Implement **partitioning** for large tables (per PRD Section 10.2)

### 5.4 API Standards
- RESTful conventions
- Standard response format:
  ```json
  {
    "success": true/false,
    "message": "...",
    "data": { ... }
  }
  ```
- Error codes per PRD Section 8
- Idempotency via `X-Reference-Id` header (Sprint 6)

---

## 6. CHECKLIST BEFORE IMPLEMENTING

### 6.1 Pre-Implementation Checklist
- [ ] Read relevant PRD section(s) thoroughly
- [ ] Check if feature is in current sprint scope
- [ ] Use Context7 to verify library/API usage
- [ ] Review acceptance criteria in PRD
- [ ] Plan database schema changes if needed

### 6.2 Post-Implementation Checklist
- [ ] **RUN BUILD to verify code compiles without errors (MANDATORY)**
- [ ] Code compiles without warnings
- [ ] Follows naming conventions
- [ ] Has appropriate error handling
- [ ] Database migrations created (if schema changed)
- [ ] Authentication/authorization applied (if needed)
- [ ] Acceptance criteria met

---

## 7. WORKFLOW WITH MCP CONTEXT7

### 7.1 How to Query Documentation

**Step 1: Resolve Library ID**
```
Query: "Entity Framework Core 10"
Library Name: "Entity Framework Core"
```

**Step 2: Query Specific Topic**
```
Library ID: "/microsoft/efcore"
Query: "How to configure PostgreSQL connection with connection string"
```

### 7.2 Common Libraries to Reference
- ASP.NET Core MVC: `/microsoft/aspnetcore-mvc`
- Entity Framework Core: `/microsoft/efcore`
- ASP.NET Core Identity: `/microsoft/aspnetcore-identity`
- Hangfire: `/hangfire-io/hangfire`
- SignalR: `/microsoft/aspnetcore-signalr`
- JWT Authentication: `/microsoft/aspnetcore-authentication`
- Bootstrap: `/twbs/bootstrap`

---

## 8. FORBIDDEN ACTIONS

### 8.1 NEVER Do These
- ❌ Implement features not in PRD without user approval
- ❌ Skip authentication/authorization requirements
- ❌ Use synchronous database calls (always async)
- ❌ Hardcode configuration values (use appsettings.json)
- ❌ Commit secrets/credentials to code
- ❌ Skip database migrations for schema changes
- ❌ Implement without using Context7 for external libraries

### 8.2 Always Follow PRD
- Sprint 1-4: Admin Panel only
- Sprint 5-6: Member API
- Do NOT implement Sprint 5-6 features before completing Sprint 1-4
- Exception: Only if user explicitly requests different priority

---

## 9. ACCEPTANCE CRITERIA REFERENCE

Each sprint has specific acceptance criteria (PRD Section 12). Before marking any task complete:

### Sprint 1 (Foundation)
- [ ] Admin login/logout works
- [ ] Dashboard shows 8 KPI cards
- [ ] Layout responsive (Full HD, 2K, Tablet)
- [ ] Database migrations working

### Sprint 2 (User & Product)
- [ ] CRUD user (list, detail, edit level, suspend)
- [ ] CRUD products
- [ ] Set harga per level (inline edit)
- [ ] CRUD level user + config

### Sprint 3 (Supplier & Transaction)
- [ ] CRUD supplier + mapping ke produk
- [ ] Transaction manual via admin works
- [ ] Routing supplier seq works
- [ ] Retry mechanism works
- [ ] Transaction detail shows attempt timeline

### Sprint 4 (Topup, Finance)
- [ ] Approve/reject topup updates user balance
- [ ] Manual balance adjustment works
- [ ] Supplier deposit records balance
- [ ] Profit reports available
- [ ] Excel export works
- [ ] Referral bonus can be paid manually

---

## 10. KEY BUSINESS RULES TO REMEMBER

### 10.1 Balance Operations (CRITICAL)
- **Hold**: When transaction starts → move from `active` to `held`
- **Debit**: When transaction succeeds → deduct from `held`
- **Release**: When transaction fails → return to `active`
- Use **row-level locking** (`FOR UPDATE`) in PostgreSQL

### 10.2 Supplier Routing
- Each product has multiple suppliers with `seq` (1, 2, 3...)
- Try `seq=1` first, retry with `seq=2` if fails
- Record all attempts in `transaction_attempts`

### 10.3 Pricing
- Each product has different prices per user level
- Stored in `product_level_prices` table
- Validation: warn if sell_price < cost_price (soft warning)

### 10.4 Authentication
- **Admin**: Cookie + CSRF (ASP.NET Core Identity)
- **API**: JWT (15min access token, 7day refresh token)
- **PIN**: BCrypt hash, cost factor 12
- **Lockout**: After 3 failed PIN attempts

---

## 11. QUICK REFERENCE

### PRD Sections
- **Overview**: Section 1
- **Architecture**: Section 2
- **Sprint Planning**: Section 3
- **Database**: Referenced throughout, stored functions in Section 6
- **API Contracts**: Section 7
- **Error Codes**: Section 8
- **UI/UX**: Section 9
- **Performance**: Section 10

### File Locations (to be created)
- Domain entities: `PedagangPulsa.Domain/Entities/`
- DTOs: `PedagangPulsa.Application/DTOs/` and `PedagangPulsa.Api/DTOs/`
- Services: `PedagangPulsa.Application/Services/`
- Repositories: `PedagangPulsa.Infrastructure/Repositories/`
- Supplier adapters: `PedagangPulsa.Infrastructure/Suppliers/`
- Admin controllers: `PedagangPulsa.Web/Areas/Admin/Controllers/`
- API controllers: `PedagangPulsa.Api/Controllers/`

---

## 12. REMINDERS FOR CLAUDE

### When User Asks to Implement Something:
1. **ALWAYS** check PRD_v1.1.md first
2. **ALWAYS** use Context7 for external libraries
3. **ALWAYS** check if it's in the current sprint
4. **ASK** if something is unclear or not in PRD
5. **FOLLOW** the sprint priority (Admin Panel → API)

### Before Writing Code:
- Read the relevant PRD section
- Use Context7 to verify library usage
- Plan the implementation approach
- Consider database implications

### During Implementation:
- Follow Clean Architecture
- Use async/await for I/O
- Implement proper error handling
- Add appropriate validation

### After Implementation:
- **RUN BUILD to verify code compiles without errors (MANDATORY - ALWAYS DO THIS)**
- Verify acceptance criteria are met
- Check for security vulnerabilities
- Ensure authentication is applied
- Update checklist

---

**END OF PROJECT RULES**

This document is ACTIVE and must be followed for all development work.
