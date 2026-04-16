# SPRINT 5 - Member API Auth & Balance
**Timeline:** Week 9-10
**Story Points:** 21
**Goal:** Member bisa register, login, cek saldo, request topup

---

## STATUS: ✅ **COMPLETED** (2026-03-13)

**Summary:**
- All 5 user stories completed
- AuthController with Register, Login (PIN-based), Refresh Token, PIN Verify
- BalanceController with current balance & history
- TopupController with file upload
- Rate limiting middleware implemented
- **Build Status:** ✅ SUCCESS (0 errors, 16 warnings)

---

## TASK BREAKDOWN

### S5-1: Register + Referral (5 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [ ] POST `/api/v1/auth/register` dengan opsional referral_code
- [ ] Generate referral code otomatis
- [ ] Validasi username unik
- [ ] Hash PIN dengan BCrypt

**Subtasks:**
- [ ] Create `AuthController` in `PedagangPulsa.Api/Controllers/`
- [ ] Create DTOs in `PedagangPulsa.Api/DTOs/`:
  - [ ] `RegisterRequest`
  - [ ] `RegisterResponse`
  - [ ] `ErrorResponse`
- [ ] Implement register endpoint:
  - [ ] Route: `POST /api/v1/auth/register`
  - [ ] Request body:
    ```json
    {
      "username": "user123",
      "full_name": "John Doe",
      "email": "john@example.com",
      "phone": "08123456789",
      "pin": "123456",
      "referral_code": "AGUS2026"  // optional
    }
    ```
  - [ ] Validation:
    - [ ] Username unique (check users table)
    - [ ] Email format valid
    - [ ] Phone format valid (Indonesia: 08xxx or 628xxx)
    - [ ] PIN must be 6 digits
    - [ ] If referral_code provided: validate exists
  - [ ] Logic:
    - [ ] Generate UUID for user_id
    - [ ] Hash PIN using BCrypt (cost factor 12)
    - [ ] Generate unique referral code: `{username}{random}` + check uniqueness
    - [ ] Set default level: Get from `user_levels` WHERE is_default = true
    - [ ] Insert user with status='active'
    - [ ] Insert `user_balances` (active=0, held=0)
    - [ ] If referral valid:
      - [ ] Insert to `referrals` table
      - [ ] Insert to `referral_bonuses` (status='pending')
    - [ ] Return 201 Created
  - [ ] Response body:
    ```json
    {
      "success": true,
      "message": "Registrasi berhasil",
      "data": {
        "user_id": "uuid-user",
        "username": "user123",
        "referral_code": "USER123XYZ",
        "level": "member1"
      }
    }
    ```
  - [ ] Error responses:
    - [ ] 400: Validation error (USERNAME_TAKEN, EMAIL_INVALID, etc.)
    - [ ] 400: Referral code invalid
- [ ] Create referral code generator service:
  - [ ] Format: `{USERNAME}{4CHAR_RANDOM}` (uppercase)
  - [ ] Check uniqueness, regenerate if exists

**Error Codes:**
```json
// 400 USERNAME_TAKEN
{
  "success": false,
  "error_code": "USERNAME_TAKEN",
  "message": "Username sudah digunakan",
  "details": { "field": "username" }
}

// 400 REFERRAL_INVALID
{
  "success": false,
  "error_code": "REFERRAL_INVALID",
  "message": "Kode referral tidak valid"
}
```

---

### S5-2: Login + JWT (5 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] POST `/api/v1/auth/login` return access token + refresh token
- [ ] Access token expiry: 15 menit
- [ ] Refresh token expiry: 7 hari
- [ ] Validate username + PIN

**Subtasks:**
- [ ] Install JWT packages:
  - [ ] `Microsoft.AspNetCore.Authentication.JwtBearer`
- [ ] Configure JWT in `Program.cs`:
  - [ ] Add JWT authentication scheme
  - [ ] Configure token parameters:
    - [ ] Secret key (from appsettings)
    - [ ] Issuer, Audience
    - [ ] Access token expiry: 15 minutes
    - [ ] Refresh token expiry: 7 days
- [ ] Create DTOs:
  - [ ] `LoginRequest`
  - [ ] `LoginResponse`
- [ ] Implement login endpoint:
  - [ ] Route: `POST /api/v1/auth/login`
  - [ ] Request body:
    ```json
    {
      "username": "user123",
      "pin": "123456"
    }
    ```
  - [ ] Validation:
    - [ ] Username exists
    - [ ] Verify PIN hash (BCrypt)
    - [ ] Check user status = active
  - [ ] Generate tokens:
    - [ ] Access token (JWT)
    - [ ] Refresh token (store in DB or encrypted JWT)
  - [ ] Response 200 OK:
    ```json
    {
      "success": true,
      "message": "Login berhasil",
      "data": {
        "access_token": "<REDACTED_JWT_TOKEN>",
        "refresh_token": "<REDACTED_JWT_TOKEN>",
        "expires_in": 900,
        "user": {
          "user_id": "uuid",
          "username": "user123",
          "level": "member1"
        }
      }
    }
    ```
  - [ ] Error responses:
    - [ ] 401: INVALID_CREDENTIALS
    - [ ] 403: ACCOUNT_SUSPENDED
- [ ] Create refresh token endpoint:
  - [ ] Route: `POST /api/v1/auth/refresh`
  - [ ] Request body:
    ```json
    {
      "refresh_token": "<REDACTED_JWT_TOKEN>"
    }
    ```
  - [ ] Validate refresh token
  - [ ] Generate new access token
  - [ ] Return new tokens (optional: rotate refresh token)
- [ ] Implement token storage (optional):
  - [ ] Table: `refresh_tokens` (user_id, token, expires_at)
  - [ ] Or use encrypted JWT with claims

**JWT Configuration:**
```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "PedagangPulsa",
            ValidAudience = "PedagangPulsa",
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });
```

---

### S5-3: PIN Verify Flow (5 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] POST `/api/v1/auth/pin/verify` return pin_session_token
- [ ] pin_session_token valid 5 menit
- [ ] Lockout setelah 3x salah

**Subtasks:**
- [ ] Create Redis service for PIN lockout:
  - [ ] Key format: `pin_lockout:{user_id}`
  - [ ] Value: attempt count
  - [ ] TTL: 15 minutes
- [ ] Create pin_session_token service:
  - [ ] Key format: `pin_session:{user_id}:{session_id}`
  - [ ] Value: `{ "user_id": "uuid", "created_at": "timestamp" }`
  - [ ] TTL: 5 minutes
- [ ] Create DTOs:
  - [ ] `PinVerifyRequest`
  - [ ] `PinVerifyResponse`
- [ ] Implement PIN verify endpoint:
  - [ ] Route: `POST /api/v1/auth/pin/verify`
  - [ ] Require authentication: `[Authorize]`
  - [ ] Request body:
    ```json
    {
      "pin": "123456"
    }
    ```
  - [ ] Validation logic:
    - [ ] Check Redis for lockout: `GET pin_lockout:{user_id}`
    - [ ] If locked (attempts >= 3): Return 403 PIN_LOCKED
    - [ ] Verify PIN hash from DB
    - [ ] If wrong:
      - [ ] Increment Redis counter: `INCR pin_lockout:{user_id}`
      - [ ] Set expiry 15 minutes
      - [ ] If count == 3: Set lockout, return 403 PIN_LOCKED
      - [ ] Else: Return 400 PIN_INVALID with remaining attempts
    - [ ] If correct:
      - [ ] Clear lockout counter: `DEL pin_lockout:{user_id}`
      - [ ] Generate session token: GUID
      - [ ] Store in Redis: `SET pin_session:{user_id}:{token}` with TTL 5 min
      - [ ] Return 200 OK
  - [ ] Response 200 OK:
    ```json
    {
      "success": true,
      "message": "PIN verified",
      "data": {
        "pin_session_token": "session-guid-xyz",
        "expires_in": 300
      }
    }
    ```
  - [ ] Error responses:
    - [ ] 400 PIN_INVALID (with remaining_attempts: 2)
    - [ ] 403 PIN_LOCKED (lockout_ends_in: 900)
- [ ] Create middleware to validate pin_session_token:
  - [ ] Check Redis: `GET pin_session:{user_id}:{token}`
  - [ ] Return 401 if not found or expired

**PIN Verify Flow:**
```
User submits PIN
  ↓
Check lockout (Redis)
  ↓
Locked? → Yes → Return 403 PIN_LOCKED
No ↓
Verify PIN (BCrypt)
  ↓
Wrong? → Yes → Increment counter (Redis)
           Count >= 3? → Yes → Lockout 15 min
           Return 400 PIN_INVALID (2 attempts left)
Correct ↓
Clear lockout counter
Generate session token (GUID)
Store in Redis (TTL 5 min)
Return 200 OK with token
```

---

### S5-4: Balance Endpoints (3 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] GET `/api/v1/balance` - Current balance
- [ ] GET `/api/v1/balance/history` - Balance history with pagination

**Subtasks:**
- [ ] Create `BalanceController` in `PedagangPulsa.Api/Controllers/`
- [ ] Add `[Authorize]` attribute
- [ ] Create DTOs:
  - [ ] `BalanceResponse`
  - [ ] `BalanceHistoryResponse`
- [ ] Implement GET balance endpoint:
  - [ ] Route: `GET /api/v1/balance`
  - [ ] Require authentication: `[Authorize]`
  - [ ] Get user_id from JWT claims
  - [ ] Query `user_balances` table
  - [ ] Response 200 OK:
    ```json
    {
      "success": true,
      "data": {
        "active_balance": 150000,
        "held_balance": 5600,
        "total_balance": 155600,
        "currency": "IDR"
      }
    }
    ```
- [ ] Implement GET balance history endpoint:
  - [ ] Route: `GET /api/v1/balance/history`
  - [ ] Query parameters: page, pageSize (default: 1, 20)
  - [ ] Query `balance_ledger` WHERE user_id = ?
  - [ ] Order by created_at DESC
  - [ ] Pagination (max 90 days)
  - [ ] Response 200 OK:
    ```json
    {
      "success": true,
      "data": {
        "items": [
          {
            "id": "uuid",
            "type": "transaction",
            "amount": -5600,
            "balance_after": 150000,
            "description": "Pembelian Pulsa Indosat 5.000",
            "created_at": "2026-03-12T10:30:00Z"
          }
        ],
        "pagination": {
          "page": 1,
          "page_size": 20,
          "total_items": 150,
          "total_pages": 8
        }
      }
    }
    ```
- [ ] Validate: max pageSize = 100

**Balance Types:**
- `topup` - Positive
- `transaction` - Negative
- `refund` - Positive
- `adjustment` - Positive or Negative
- `referral` - Positive
- `transfer_in` - Positive
- `transfer_out` - Negative

---

### S5-5: Topup Request (3 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] POST `/api/v1/topup` dengan upload bukti
- [ ] GET `/api/v1/topup/history` - List topup requests
- [ ] Upload bukti transfer ke MinIO/local storage

**Subtasks:**
- [ ] Create `TopupController` in `PedagangPulsa.Api/Controllers/`
- [ ] Add `[Authorize]` attribute
- [ ] Setup file upload:
  - [ ] Configure MinIO or local storage
  - [ ] Add multipart/form-data support
- [ ] Create DTOs:
  - [ ] `TopupRequest`
  - [ ] `TopupResponse`
  - [ ] `TopupHistoryResponse`
- [ ] Implement POST topup endpoint:
  - [ ] Route: `POST /api/v1/topup`
  - [ ] Content-Type: `multipart/form-data`
  - [ ] Form fields:
    - [ ] `amount` (decimal, required)
    - [ ] `bank_code` (string, required)
    - [ ] `transfer_proof` (file, required, image only: jpg/png)
    - [ ] `notes` (string, optional)
  - [ ] Validation:
    - [ ] Amount >= min_topup (from user_level_config)
    - [ ] Amount <= max_topup
    - [ ] Bank code valid
    - [ ] File is image (jpg/png)
    - [ ] File size <= 5MB
  - [ ] Logic:
    - [ ] Upload file to storage
    - [ ] Generate URL
    - [ ] Insert to `topup_requests` (status='pending')
    - [ ] Queue notification to admin
  - [ ] Response 201 Created:
    ```json
    {
      "success": true,
      "message": "Permintaan topup berhasil dibuat",
      "data": {
        "request_id": "uuid",
        "amount": 100000,
        "status": "pending",
        "created_at": "2026-03-12T10:30:00Z"
      }
    }
    ```
  - [ ] Error responses:
    - [ ] 400 VALIDATION_ERROR (amount below/above limit)
    - [ ] 400 INVALID_FILE (not image or too large)
- [ ] Implement GET topup history endpoint:
  - [ ] Route: `GET /api/v1/topup/history`
  - [ ] Query parameters: page, pageSize, status
  - [ ] Query `topup_requests` WHERE user_id = ?
  - [ ] Order by created_at DESC
  - [ ] Response 200 OK:
    ```json
    {
      "success": true,
      "data": {
        "items": [
          {
            "request_id": "uuid",
            "amount": 100000,
            "status": "pending",
            "bank_code": "BCA",
            "created_at": "2026-03-12T10:30:00Z",
            "approved_at": null
          }
        ],
        "pagination": { ... }
      }
    }
    ```

**File Upload Configuration:**
```csharp
services.Configure<FormOptions>(options => {
    options.MultipartBodyLengthLimit = 10485760; // 10MB
});
```

---

## TECHNICAL NOTES (from PRD)

- JWT expiry: 15 menit (access token), 7 hari (refresh token)
- PIN hash pakai BCrypt cost factor 12
- Lockout setelah PIN salah 3x
- Idempotency belum wajib di sprint ini (masuk sprint 6)
- pin_session_token TTL: 5 menit

---

## API ENDPOINTS SUMMARY

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/v1/auth/register` | No | Register new user |
| POST | `/api/v1/auth/login` | No | Login with username+PIN |
| POST | `/api/v1/auth/refresh` | No | Refresh access token |
| POST | `/api/v1/auth/pin/verify` | Yes | Verify PIN for sensitive operations |
| GET | `/api/v1/balance` | Yes | Get current balance |
| GET | `/api/v1/balance/history` | Yes | Get balance ledger history |
| POST | `/api/v1/topup` | Yes | Request topup with proof |
| GET | `/api/v1/topup/history` | Yes | Get topup request history |

---

## DELIVERABLES

- [ ] Member bisa register via API (dengan/tanpa referral)
- [ ] Member bisa login → dapat JWT token
- [ ] Member bisa verify PIN → dapat pin_session_token
- [ ] Member bisa cek saldo + history mutasi
- [ ] Member bisa submit request topup (tunggu approve admin)

---

## DEFINITION OF DONE

- [ ] Register endpoint working with referral logic
- [ ] Login generates valid JWT tokens
- [ ] Refresh token flow working
- [ ] PIN verify with lockout mechanism
- [ ] Balance endpoints return correct data
- [ ] Topup request with file upload working
- [ ] All endpoints have proper error handling
- [ ] JWT authentication configured correctly
- [ ] Redis integration for PIN lockout and session tokens
- [ ] No critical bugs

---

**NEXT SPRINT:** Sprint 6 - Member API Transaction
