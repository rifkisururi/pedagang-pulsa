# SPRINT 6 - Member API Transaction
**Timeline:** Week 11-12
**Story Points:** 26
**Goal:** Member bisa order produk via API

---

## STATUS: ✅ **COMPLETED** (2026-03-13)

**Summary:**
- All 6 user stories completed
- ProductController with user-level pricing, categories, filters
- TransactionController with idempotency, PIN session validation
- TransferController with peer-to-peer transfer
- NotificationController with inbox, mark as read
- Rate limiting middleware (5-10 req/min per endpoint)
- **Build Status:** ✅ SUCCESS (0 errors, 16 warnings)

---

## TASK BREAKDOWN

### S6-1: Product Listing (3 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [ ] GET `/api/v1/products` dengan harga sesuai level user
- [ ] Filter by category, operator
- [ ] Pagination

**Subtasks:**
- [ ] Create `ProductController` in `PedagangPulsa.Api/Controllers/`
- [ ] Add `[Authorize]` attribute
- [ ] Create DTOs:
  - [ ] `ProductListResponse`
  - [ ] `ProductItemDto`
- [ ] Implement GET products endpoint:
  - [ ] Route: `GET /api/v1/products`
  - [ ] Query parameters: category, operator, type, page, pageSize
  - [ ] Get user_id from JWT claims
  - [ ] Get user's level_id
  - [ ] Query products with prices:
    ```sql
    SELECT
      p.id, p.sku, p.name, p.category, p.operator, p.type,
      p.description, p.image_url,
      plp.sell_price
    FROM products p
    LEFT JOIN product_level_prices plp
      ON plp.product_id = p.id
      AND plp.level_id = ?
    WHERE p.is_active = true
    ORDER BY p.category, p.name
    LIMIT ? OFFSET ?
    ```
  - [ ] Response 200 OK:
    ```json
    {
      "success": true,
      "data": {
        "items": [
          {
            "id": "uuid",
            "sku": "TSEL5",
            "name": "Pulsa Telkomsel 5.000",
            "category": "Pulsa",
            "operator": "Telkomsel",
            "type": "Pulsa",
            "description": "Pulsa Telkomsel 5.000",
            "image_url": "https://...",
            "sell_price": 5600,
            "currency": "IDR"
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
  - [ ] Cache results in Redis (TTL: 5 minutes)
  - [ ] Cache key format: `products:{level_id}:{category}:{operator}:{page}`

---

### S6-2: Create Transaction (8 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] POST `/api/v1/transactions` dengan idempotency (X-Reference-Id)
- [ ] Hold balance, routing supplier, debit/release
- [ ] Return transaction status immediately (async processing)

**Subtasks:**
- [ ] Create `TransactionController` in `PedagangPulsa.Api/Controllers/`
- [ ] Add `[Authorize]` attribute
- [ ] Create DTOs:
  - [ ] `CreateTransactionRequest`
  - [ ] `CreateTransactionResponse`
- [ ] Implement idempotency middleware:
  - [ ] Extract `X-Reference-Id` from header
  - [ ] Check Redis: `GET idempotency:{reference_id}`
  - [ ] If exists, return existing transaction
  - [ ] Check PostgreSQL `idempotency_keys` table as backup
- [ ] Implement POST transaction endpoint:
  - [ ] Route: `POST /api/v1/transactions`
  - [ ] Headers:
    - [ ] `Authorization: Bearer <jwt>`
    - [ ] `X-Reference-Id: app-{user_id}-{timestamp}-{random}`
  - [ ] Request body:
    ```json
    {
      "product_id": "uuid-produk-indosat-5k",
      "destination": "08123456789",
      "pin_session_token": "pin-session-xyz123"
    }
    ```
  - [ ] Validation:
    - [ ] pin_session_token valid (check Redis)
    - [ ] product_id exists and active
    - [ ] destination format valid (phone number for pulsa/data)
    - [ ] idempotency key unique
  - [ ] Logic:
    1. **Validate PIN session**: Check Redis `pin_session:{user_id}:{token}`
    2. **Get price**: Query `product_level_prices` for user's level
    3. **Check balance**: `SELECT active_balance FROM user_balances WHERE user_id = ?`
    4. **Begin transaction**:
       - [ ] `SELECT ... FROM user_balances WHERE user_id = ? FOR UPDATE`
       - [ ] If balance < price: ROLLBACK, return 400 INSUFFICIENT_BALANCE
       - [ ] Call `hold_balance(user_id, price, 'transaction', transaction_id)`
       - [ ] Insert to `transactions` (status='pending')
       - [ ] Insert to `idempotency_keys`
       - [ ] Set Redis: `idempotency:{reference_id}` with transaction_id (TTL 24h)
       - [ ] COMMIT
    5. **Enqueue background job**: Hangfire `ProcessTransactionJob.Enqueue(transaction_id)`
    6. **Return response** (async)
  - [ ] Response 201 Created:
    ```json
    {
      "success": true,
      "message": "Transaksi berhasil dibuat",
      "data": {
        "transaction_id": "uuid-transaksi",
        "reference_id": "app-uuid-user-1710234567-a1b2c3",
        "status": "pending",
        "product": {
          "id": "uuid-produk-indosat-5k",
          "name": "Pulsa Indosat 5.000",
          "operator": "Indosat"
        },
        "destination": "08123456789",
        "price": 5600,
        "created_at": "2026-03-12T04:55:00Z"
      }
    }
    ```
  - [ ] Error responses:
    - [ ] 400 INSUFFICIENT_BALANCE (required, available)
    - [ ] 400 PIN_SESSION_INVALID
    - [ ] 400 PRODUCT_INACTIVE
    - [ ] 409 DUPLICATE_TRANSACTION (existing_transaction_id, status)

**Transaction Flow (API Side):**
```
POST /transactions with X-Reference-Id
  ↓
Check idempotency (Redis + DB)
  ↓
Exists? → Yes → Return existing transaction
No ↓
Validate PIN session (Redis)
  ↓
Valid? → No → Return 401
Yes ↓
Get product price for user level
  ↓
Check balance (DB FOR UPDATE)
  ↓
Sufficient? → No → Return 400 INSUFFICIENT_BALANCE
Yes ↓
Hold balance (DB transaction)
Insert transaction (pending)
Insert idempotency key
Set Redis cache
  ↓
Enqueue Hangfire job (async)
  ↓
Return 201 with transaction_id
```

---

### S6-3: Transaction History (3 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] GET `/api/v1/transactions` - List transactions
- [ ] GET `/api/v1/transactions/{id}` - Detail transaction

**Subtasks:**
- [ ] Implement GET transactions endpoint:
  - [ ] Route: `GET /api/v1/transactions`
  - [ ] Query parameters: status, page, pageSize, startDate, endDate
  - [ ] Get user_id from JWT claims
  - [ ] Query with filters (max 90 days)
  - [ ] Response 200 OK:
    ```json
    {
      "success": true,
      "data": {
        "items": [
          {
            "transaction_id": "uuid",
            "reference_id": "app-...",
            "status": "success",
            "product": {
              "name": "Pulsa Indosat 5.000",
              "operator": "Indosat"
            },
            "destination": "08123456789",
            "price": 5600,
            "sn": "1234567890",
            "created_at": "2026-03-12T10:30:00Z",
            "completed_at": "2026-03-12T10:30:25Z"
          }
        ],
        "pagination": { ... }
      }
    }
    ```
- [ ] Implement GET transaction detail endpoint:
  - [ ] Route: `GET /api/v1/transactions/{id}`
  - [ ] Validate: transaction belongs to user
  - [ ] Include attempt timeline:
    ```json
    {
      "success": true,
      "data": {
        "transaction_id": "uuid",
        "reference_id": "app-...",
        "status": "success",
        "product": { ... },
        "destination": "08123456789",
        "price": 5600,
        "cost_price": 5200,
        "profit": 400,
        "sn": "1234567890",
        "created_at": "2026-03-12T10:30:00Z",
        "completed_at": "2026-03-12T10:30:25Z",
        "attempts": [
          {
            "supplier_name": "Digiflazz",
            "seq": 1,
            "status": "success",
            "created_at": "2026-03-12T10:30:05Z",
            "completed_at": "2026-03-12T10:30:25Z",
            "supplier_trx_id": "DGX123"
          }
        ]
      }
    }
    ```

---

### S6-4: Transfer Saldo (5 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] POST `/api/v1/transfer` antar member
- [ ] Validasi `can_transfer` dari user level
- [ ] Idempotency dengan X-Reference-Id

**Subtasks:**
- [ ] Create `TransferController` in `PedagangPulsa.Api/Controllers/`
- [ ] Add `[Authorize]` attribute
- [ ] Create DTOs:
  - [ ] `TransferRequest`
  - [ ] `TransferResponse`
- [ ] Implement POST transfer endpoint:
  - [ ] Route: `POST /api/v1/transfer`
  - [ ] Headers:
    - [ ] `Authorization: Bearer <jwt>`
    - [ ] `X-Reference-Id: transfer-{user_id}-{timestamp}-{random}`
  - [ ] Request body:
    ```json
    {
      "to_username": "user456",
      "amount": 50000,
      "notes": "Bayar utang",
      "pin_session_token": "pin-session-xyz123"
    }
    ```
  - [ ] Validation:
    - [ ] pin_session_token valid
    - [ ] to_username exists and not same as from_user
    - [ ] amount > 0 and within transfer limits
    - [ ] from_user can_transfer = true (check user_level)
    - [ ] Sufficient balance
    - [ ] idempotency key unique
  - [ ] Logic:
    1. Get to_user by username
    2. Check from_user can_transfer
    3. Begin transaction:
       - [ ] `SELECT ... FROM user_balances WHERE user_id IN (?, ?) FOR UPDATE`
       - [ ] Check from_user balance >= amount
       - [ ] Debit from_user: `UPDATE user_balances SET active_balance = active_balance - ? WHERE user_id = ?`
       - [ ] Credit to_user: `UPDATE user_balances SET active_balance = active_balance + ? WHERE user_id = ?`
       - [ ] Insert balance_ledger for from_user (type='transfer_out')
       - [ ] Insert balance_ledger for to_user (type='transfer_in')
       - [ ] Insert idempotency key
       - [ ] COMMIT
    4. Queue notifications to both users
  - [ ] Response 201 Created:
    ```json
    {
      "success": true,
      "message": "Transfer berhasil",
      "data": {
        "transfer_id": "uuid",
        "from": "user123",
        "to": "user456",
        "amount": 50000,
        "notes": "Bayar utang",
        "created_at": "2026-03-12T05:10:00Z"
      }
    }
    ```
  - [ ] Error responses:
    - [ ] 403 TRANSFER_DISABLED (user level not allowed)
    - [ ] 400 INSUFFICIENT_BALANCE
    - [ ] 404 USER_NOT_FOUND (to_username)
    - [ ] 400 SAME_USER (cannot transfer to self)
    - [ ] 409 DUPLICATE_TRANSACTION

---

### S6-5: Notification Inbox (3 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] GET `/api/v1/notifications` - List notifications
- [ ] POST `/api/v1/notifications/{id}/read` - Mark as read

**Subtasks:**
- [ ] Create `NotificationController` in `PedagangPulsa.Api/Controllers/`
- [ ] Add `[Authorize]` attribute
- [ ] Create DTOs:
  - [ ] `NotificationListResponse`
  - [ ] `NotificationItemDto`
- [ ] Implement GET notifications endpoint:
  - [ ] Route: `GET /api/v1/notifications`
  - [ ] Query parameters: is_read (true/false/all), page, pageSize
  - [ ] Get user_id from JWT claims
  - [ ] Query `notifications` WHERE user_id = ?
  - [ ] Order by created_at DESC
  - [ ] Response 200 OK:
    ```json
    {
      "success": true,
      "data": {
        "items": [
          {
            "id": "uuid",
            "type": "transaction_success",
            "title": "Transaksi Berhasil",
            "message": "Pembelian Pulsa Indosat 5.000 ke 08123456789 berhasil",
            "is_read": false,
            "created_at": "2026-03-12T10:30:00Z"
          }
        ],
        "unread_count": 5,
        "pagination": { ... }
      }
    }
    ```
- [ ] Implement POST mark read endpoint:
  - [ ] Route: `POST /api/v1/notifications/{id}/read`
  - [ ] Validate: notification belongs to user
  - [ ] Update: `is_read = true, read_at = NOW()`
  - [ ] Response 200 OK:
    ```json
    {
      "success": true,
      "message": "Notifikasi ditandai sebagai sudah dibaca"
    }
    ```
- [ ] Implement POST mark all read endpoint:
  - [ ] Route: `POST /api/v1/notifications/read-all`
  - [ ] Update all unread notifications for user
  - [ ] Response 200 OK

**Notification Types:**
- `transaction_success` - Transaction successful
- `transaction_failed` - Transaction failed
- `topup_approved` - Topup approved
- `topup_rejected` - Topup rejected
- `balance_adjustment` - Manual balance adjustment
- `referral_bonus` - Referral bonus received
- `transfer_in` - Transfer received
- `transfer_out` - Transfer sent

---

### S6-6: Rate Limiting (4 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] Implementasi rate limit: login 5x/menit, order 10x/menit
- [ ] Redis-based rolling window
- [ ] Return 429 when limit exceeded

**Subtasks:**
- [ ] Create rate limit middleware:
  - [ ] `RateLimitMiddleware` class
  - [ ] Check Redis for request count
  - [ ] Return 429 if exceeded
- [ ] Configure rate limit rules (appsettings.json):
  ```json
    "RateLimit": {
      "Login": {
        "PerMinute": 5,
        "PerHour": 20
      },
      "Transaction": {
        "PerMinute": 10,
        "PerHour": 50
      },
      "Default": {
        "PerMinute": 60,
        "PerHour": 1000
      }
    }
  ```
- [ ] Implement rate limit logic:
  - [ ] Key format: `ratelimit:{endpoint}:{user_id/ip}:{minute}`
  - [ ] Use Redis INCR with EXPIRE
  - [ ] Rolling window: use sorted sets or sliding window
- [ ] Apply rate limiting to endpoints:
  - [ ] `POST /api/v1/auth/login` - 5/minute per IP
  - [ ] `POST /api/v1/auth/pin/verify` - 10/minute per user
  - [ ] `POST /api/v1/transactions` - 10/minute per user
  - [ ] `POST /api/v1/transfer` - 10/minute per user
  - [ ] Default: 60/minute per user
- [ ] Response 429 Too Many Requests:
  ```json
  {
    "success": false,
    "error_code": "RATE_LIMIT_EXCEEDED",
    "message": "Terlalu banyak request. Silakan coba lagi dalam 30 detik.",
    "details": {
      "retry_after": 30
    }
  }
  ```
- [ ] Add headers to response:
  - [ ] `X-RateLimit-Limit: 10`
  - [ ] `X-RateLimit-Remaining: 7`
  - [ ] `X-RateLimit-Reset: 1710234567`

**Rate Limit Algorithm (Sliding Window Log):**
```csharp
// Redis sorted set approach
var key = $"ratelimit:{endpoint}:{user_id}:{DateTime.UtcNow.Ticks/60000}";
var count = await db.SortedSetAddAsync(key, request_id, timestamp);
await db.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
if (count > limit) {
  return StatusCode(429, ...);
}
```

---

## TECHNICAL NOTES (from PRD)

- Gunakan middleware rate limit berbasis Redis
- Simpan idempotency key di Redis (expire 24 jam) + PostgreSQL
- Notifikasi dikirim via background job (Hangfire)
- Transfer saldo: validasi `can_transfer_override` atau `level.can_transfer`
- Idempotency key TTL: 24 jam

---

## API ENDPOINTS SUMMARY

| Method | Endpoint | Auth | Rate Limit | Description |
|---|---|---|---|---|
| GET | `/api/v1/products` | Yes | 60/min | List products with prices |
| POST | `/api/v1/transactions` | Yes | 10/min | Create transaction |
| GET | `/api/v1/transactions` | Yes | 60/min | List transactions |
| GET | `/api/v1/transactions/{id}` | Yes | 60/min | Transaction detail |
| POST | `/api/v1/transfer` | Yes | 10/min | Transfer balance |
| GET | `/api/v1/notifications` | Yes | 60/min | List notifications |
| POST | `/api/v1/notifications/{id}/read` | Yes | 60/min | Mark as read |
| POST | `/api/v1/notifications/read-all` | Yes | 60/min | Mark all read |

---

## COMPLETE API CONTRACT SUMMARY

### Auth Endpoints (Sprint 5)
- POST `/api/v1/auth/register` - Register
- POST `/api/v1/auth/login` - Login (5/min per IP)
- POST `/api/v1/auth/refresh` - Refresh token
- POST `/api/v1/auth/pin/verify` - Verify PIN (10/min per user)

### Balance Endpoints (Sprint 5)
- GET `/api/v1/balance` - Get balance
- GET `/api/v1/balance/history` - Balance history

### Topup Endpoints (Sprint 5)
- POST `/api/v1/topup` - Request topup
- GET `/api/v1/topup/history` - Topup history

### Product Endpoints (Sprint 6)
- GET `/api/v1/products` - List products

### Transaction Endpoints (Sprint 6)
- POST `/api/v1/transactions` - Create transaction (10/min per user)
- GET `/api/v1/transactions` - List transactions
- GET `/api/v1/transactions/{id}` - Transaction detail

### Transfer Endpoints (Sprint 6)
- POST `/api/v1/transfer` - Transfer balance (10/min per user)

### Notification Endpoints (Sprint 6)
- GET `/api/v1/notifications` - List notifications
- POST `/api/v1/notifications/{id}/read` - Mark as read
- POST `/api/v1/notifications/read-all` - Mark all read

---

## DELIVERABLES

- [ ] Member bisa lihat produk dengan harga sesuai levelnya
- [ ] Member bisa order produk → otomatis hold saldo, routing supplier, debit/release
- [ ] Idempotency key (`X-Reference-Id`) mencegah double order
- [ ] Member bisa transfer saldo ke member lain (jika `can_transfer = true`)
- [ ] Rate limiting aktif untuk endpoint kritis
- [ ] Notification inbox berfungsi

---

## DEFINITION OF DONE

- [ ] Product listing returns correct prices per level
- [ ] Transaction creation working end-to-end
- [ ] Idempotency prevents duplicate requests
- [ ] Background job processes transactions correctly
- [ ] Transfer functional with proper validation
- [ ] Notification inbox creates and displays notifications
- [ ] Rate limiting implemented and tested
- [ ] All endpoints have proper error handling
- [ ] API documentation complete (Swagger/OpenAPI)
- [ ] No critical bugs

---

## POST-SPRINT ACTIVITIES

- [ ] API Performance Testing (target: 200 TPS)
- [ ] Load testing with k6 or similar
- [ ] Security audit (SQL injection, XSS, CSRF)
- [ ] API documentation with Swagger
- [ ] Integration testing with mobile team
- [ ] Production deployment preparation

---

**PROJECT COMPLETE** 🎉

All 6 sprints finished. Admin Panel and Member API are fully functional.
