# Product Requirements Document (PRD)
## PedagangPulsa.com — Admin Panel & Member API
**Version:** 1.1  
**Date:** 2026-03-12  
**Author:** Product Team  
**Status:** Ready for Sprint Planning  
**Platform:** ASP.NET 10 MVC + Web API (Single Project)

---

## 1. OVERVIEW & TUJUAN

### 1.1 Latar Belakang
PedagangPulsa.com adalah platform B2B/B2C untuk penjualan pulsa, paket data, token listrik, game voucher, PPOB, dan e-money. Sistem memiliki lebih dari satu supplier dengan mekanisme routing otomatis dan retry.

### 1.2 Tujuan Dokumen
Dokumen ini mendefinisikan kebutuhan fungsional dan non-fungsional untuk:
1. **Admin Panel** — aplikasi web internal berbasis ASP.NET 10 MVC (PRIORITAS SPRINT 1-4)
2. **Member API** — REST API untuk aplikasi mobile member (SPRINT 5-6)

### 1.3 Stakeholder
| Role | Kepentingan |
|---|---|
| Super Admin | Full akses semua fitur |
| Admin | Operasional harian (topup, transaksi, notifikasi) |
| Finance | Laporan keuangan, rekonsiliasi, deposit supplier |
| Staff | Monitoring transaksi, cek status |
| Developer | Integrasi API mobile |

### 1.4 Strategi Eksekusi Sprint
**Fokus utama: Admin Panel dulu (Sprint 1-4), baru kemudian Member API (Sprint 5-6)**

Reasoning:
- Admin panel adalah "command center" — tanpa ini, bisnis tidak bisa operasional
- Admin bisa melakukan testing transaksi manual sebelum API dibuka ke member
- API design bisa dimatangkan sambil observasi kebutuhan dari admin panel
- Tim mobile bisa mulai design UI/UX dulu sambil menunggu API

---

## 2. ARSITEKTUR SISTEM

### 2.1 Struktur Project (Single Solution)
```
PedagangPulsa.sln
├── PedagangPulsa.Web/                  ← ASP.NET 10 MVC (Admin Panel)
│   ├── Areas/
│   │   └── Admin/
│   │       ├── Controllers/
│   │       ├── Views/
│   │       └── ViewModels/
│   ├── wwwroot/
│   │   ├── css/
│   │   ├── js/
│   │   └── lib/
│   └── Program.cs
│
├── PedagangPulsa.Api/                  ← ASP.NET 10 Web API (Member Mobile)
│   ├── Controllers/
│   ├── DTOs/
│   ├── Middleware/
│   └── Program.cs
│
├── PedagangPulsa.Application/          ← Business Logic (shared)
│   ├── Services/
│   ├── Interfaces/
│   └── DTOs/
│
├── PedagangPulsa.Domain/               ← Entities, Enums, Value Objects
│
├── PedagangPulsa.Infrastructure/       ← EF Core, Repository, External Services
│   ├── Data/
│   │   └── AppDbContext.cs
│   ├── Repositories/
│   ├── Suppliers/                      ← Adapter tiap supplier (Strategy Pattern)
│   └── Notifications/                  ← Email, SMS, WhatsApp adapter
│
└── PedagangPulsa.Tests/
```

### 2.2 Tech Stack
| Layer | Teknologi |
|---|---|
| Web Framework | ASP.NET 10 MVC + Minimal API |
| ORM | Entity Framework Core 10 |
| Database | PostgreSQL 16 |
| Caching | Redis (session, idempotency, realtime counter) |
| Background Job | Hangfire (retry, notifikasi, partisi cleanup) |
| Realtime Dashboard | SignalR |
| Auth Admin | ASP.NET Core Identity + Cookie |
| Auth API | JWT Bearer Token |
| Frontend Admin | Bootstrap 5.3 + Chart.js + DataTables |
| Upload File | MinIO / Local Storage |
| SMS Gateway | Twilio / ZenzivaSMS |
| WhatsApp | WhatsApp Business API / Fonnte |
| Email | SMTP / SendGrid |

---

## 3. SPRINT PLANNING & ROADMAP

### Sprint Overview (2 minggu per sprint)

| Sprint | Fokus | Story Points | Target |
|---|---|---|---|
| **Sprint 1** | Foundation + Auth + Dashboard | 21 | Admin bisa login, lihat dashboard dasar |
| **Sprint 2** | User & Product Management | 26 | Admin bisa kelola user, produk, harga |
| **Sprint 3** | Supplier & Transaction Core | 34 | Transaksi manual via admin, routing supplier |
| **Sprint 4** | Topup, Finance, Reports | 28 | Approve topup, laporan profit, rekonsiliasi |
| **Sprint 5** | Member API Auth & Balance | 21 | Mobile app bisa register, login, topup |
| **Sprint 6** | Member API Transaction | 26 | Mobile app bisa order produk |

**Total estimasi: 12 minggu (3 bulan)**

---

### SPRINT 1: Foundation + Auth + Dashboard (Priority: P0)
**Timeline:** Week 1-2  
**Goal:** Admin bisa login dan melihat overview bisnis

#### User Stories
| ID | Story | Acceptance Criteria | SP |
|---|---|---|---|
| S1-1 | Setup project structure | Solution dengan 5 project terbuat, EF migrations jalan | 5 |
| S1-2 | Database schema implementation | Semua tabel dari schema SQL termigrasi | 3 |
| S1-3 | Admin login/logout | Admin bisa login dengan username/password, logout | 3 |
| S1-4 | Dashboard layout | Topbar + sidebar responsive (Full HD / 2K / Tablet) | 5 |
| S1-5 | Dashboard KPI cards | 8 KPI cards: transaksi hari ini, omzet, profit, user baru, dll | 3 |
| S1-6 | Dashboard chart basic | 1 line chart (transaksi per jam) + 1 bar chart (omzet 7 hari) | 2 |

#### Deliverables
- Admin bisa login ke `/admin`
- Dashboard tampil KPI cards (data dummy/hardcoded OK untuk sprint ini)
- Layout responsive sudah jalan di 3 target layar

#### Technical Notes
- Gunakan ASP.NET Core Identity untuk admin auth
- Setup Hangfire dashboard di `/hangfire` (secured)
- Seed 1 superadmin user di migration

---

### SPRINT 2: User & Product Management (Priority: P0)
**Timeline:** Week 3-4  
**Goal:** Admin bisa mengelola user dan produk dengan harga per level

#### User Stories
| ID | Story | Acceptance Criteria | SP |
|---|---|---|---|
| S2-1 | Daftar user + filter | Tabel user server-side, filter by level/status/tanggal | 5 |
| S2-2 | Detail user + tabs | Tab profil, saldo, transaksi, referral (tab kosong OK) | 5 |
| S2-3 | Edit level & suspend user | Admin bisa ubah level user dan suspend | 3 |
| S2-4 | CRUD produk | Tambah, edit, hapus produk (kategori, operator, dll) | 5 |
| S2-5 | Kelola harga per level | Inline edit table harga untuk semua level | 5 |
| S2-6 | CRUD level user | Tambah, edit level + config limit (key-value table) | 3 |

#### Deliverables
- Admin bisa kelola user: lihat, edit level, suspend
- Admin bisa kelola produk: CRUD + set harga per level
- Validasi: warning jika harga jual < cost supplier (soft warning)

#### Technical Notes
- DataTables server-side processing untuk tabel user
- Harga per level disimpan di `product_level_prices` (constraint UNIQUE)
- Tab transaksi/referral user masih kosong (akan diisi sprint 3-4)

---

### SPRINT 3: Supplier & Transaction Core (Priority: P0)
**Timeline:** Week 5-6  
**Goal:** Sistem bisa proses transaksi dengan routing supplier otomatis

#### User Stories
| ID | Story | Acceptance Criteria | SP |
|---|---|---|---|
| S3-1 | CRUD supplier | Admin bisa tambah, edit supplier (API URL, key, timeout) | 3 |
| S3-2 | Mapping supplier ke produk | Admin bisa mapping produk ke supplier + set cost_price + seq | 5 |
| S3-3 | Drag-and-drop seq routing | Ubah urutan seq dengan drag-drop (atau input manual jika drag-drop masuk v1.1) | 5 |
| S3-4 | Supplier adapter pattern | Implementasi 1 supplier real (contoh: Digiflazz/VIPReseller) | 8 |
| S3-5 | Transaction orchestrator | Service untuk: hold balance → order supplier → retry → debit/release | 8 |
| S3-6 | Daftar transaksi + detail | Tabel transaksi, detail transaksi dengan timeline attempt | 5 |

#### Deliverables
- Admin bisa mapping produk ke supplier dengan urutan seq
- Transaksi bisa dibuat manual via admin (form input: user, produk, tujuan)
- Sistem otomatis routing ke supplier seq=1, retry ke seq=2 jika gagal
- Detail transaksi menampilkan timeline attempt per supplier

#### Technical Notes
- **Supplier adapter:** Gunakan Strategy Pattern, 1 interface `ISupplierAdapter`
- Implementasi stored function: `hold_balance`, `debit_held_balance`, `release_held_balance`
- Simpan semua callback supplier di `supplier_callbacks` sebelum proses

---

### SPRINT 4: Topup, Finance, Reports (Priority: P0)
**Timeline:** Week 7-8  
**Goal:** Admin bisa approve topup, lihat laporan profit, deposit ke supplier

#### User Stories
| ID | Story | Acceptance Criteria | SP |
|---|---|---|---|
| S4-1 | Daftar topup pending | Tabel topup pending dengan preview bukti transfer | 3 |
| S4-2 | Approve/reject topup | Admin bisa approve (tambah saldo) atau reject dengan alasan | 5 |
| S4-3 | Adjustment saldo manual | Superadmin/finance bisa adjustment saldo user + catatan | 3 |
| S4-4 | Supplier balance tracking | Tampil saldo supplier, history mutasi, form deposit | 5 |
| S4-5 | Profit ledger & reports | View: profit harian, per supplier, per produk | 5 |
| S4-6 | Export laporan Excel | Export laporan profit, transaksi ke .xlsx | 3 |
| S4-7 | Referral management | Admin lihat pending referral bonus + beri bonus manual | 4 |

#### Deliverables
- Admin bisa approve/reject topup → saldo user bertambah otomatis
- Admin bisa deposit ke supplier → saldo supplier bertambah
- Laporan profit tersedia: harian, per supplier, per produk
- Export laporan ke Excel berfungsi
- Admin bisa beri bonus referral manual

#### Technical Notes
- Implementasi stored function: `debit_supplier_balance`, `credit_supplier_balance`
- View database: `v_profit_daily`, `v_profit_by_supplier`, `v_profit_by_product`
- Export Excel pakai EPPlus / ClosedXML
- Insert ke `profit_ledger` dan `supplier_balance_ledger` setiap transaksi sukses

---

### SPRINT 5: Member API — Auth & Balance (Priority: P1)
**Timeline:** Week 9-10  
**Goal:** Member bisa register, login, cek saldo, request topup

#### User Stories
| ID | Story | Acceptance Criteria | SP |
|---|---|---|---|
| S5-1 | Register + referral | POST `/auth/register` dengan opsional referral_code | 5 |
| S5-2 | Login + JWT | POST `/auth/login` return access token + refresh token | 5 |
| S5-3 | PIN verify flow | POST `/auth/pin/verify` return pin_session_token (valid 5 menit) | 5 |
| S5-4 | Balance endpoints | GET `/balance`, GET `/balance/history` | 3 |
| S5-5 | Topup request | POST `/topup` dengan upload bukti, GET `/topup/history` | 3 |

#### Deliverables
- Member bisa register via API (dengan/tanpa referral)
- Member bisa login → dapat JWT token
- Member bisa cek saldo + history mutasi
- Member bisa submit request topup (tunggu approve admin)

#### Technical Notes
- JWT expiry: 15 menit (access token), 7 hari (refresh token)
- PIN hash pakai BCrypt cost factor 12
- Lockout setelah PIN salah 3x
- Idempotency belum wajib di sprint ini (masuk sprint 6)

---

### SPRINT 6: Member API — Transaction (Priority: P1)
**Timeline:** Week 11-12  
**Goal:** Member bisa order produk via API

#### User Stories
| ID | Story | Acceptance Criteria | SP |
|---|---|---|---|
| S6-1 | Product listing | GET `/products` dengan harga sesuai level user | 3 |
| S6-2 | Create transaction | POST `/transactions` dengan idempotency (X-Reference-Id) | 8 |
| S6-3 | Transaction history | GET `/transactions`, GET `/transactions/{id}` | 3 |
| S6-4 | Transfer saldo | POST `/transfer` antar member | 5 |
| S6-5 | Notification inbox | GET `/notifications`, POST `/notifications/{id}/read` | 3 |
| S6-6 | Rate limiting | Implementasi rate limit: login 5x/menit, order 10x/menit | 4 |

#### Deliverables
- Member bisa lihat produk dengan harga sesuai levelnya
- Member bisa order produk → otomatis hold saldo, routing supplier, debit/release
- Idempotency key (`X-Reference-Id`) mencegah double order
- Member bisa transfer saldo ke member lain (jika `can_transfer = true`)
- Rate limiting aktif untuk endpoint kritis

#### Technical Notes
- Gunakan middleware rate limit berbasis Redis
- Simpan idempotency key di Redis (expire 24 jam) + PostgreSQL
- Notifikasi dikirim via background job (Hangfire)
- Transfer saldo: validasi `can_transfer_override` atau `level.can_transfer`

---

## 4. KONFIGURASI DEFAULT

| Parameter | Nilai | Configurable? |
|---|---|---|
| PIN salah maksimal sebelum lock | 3 kali | ✅ Di `appsettings.json` |
| Durasi pin_session_token | 5 menit | ✅ |
| Access token expiry (JWT) | 15 menit | ✅ |
| Refresh token expiry (JWT) | 7 hari | ✅ |
| Timeout transaksi dianggap stuck | 2 menit | ✅ |
| Dashboard realtime refresh interval | 10 detik (SignalR push) | ✅ |
| Threshold saldo supplier HIJAU | > Rp 1.000.000 | ✅ Di database `suppliers` table |
| Threshold saldo supplier KUNING | Rp 100.000 - 1.000.000 | ✅ |
| Threshold saldo supplier MERAH | < Rp 100.000 | ✅ |
| Idempotency key TTL | 24 jam | ✅ |
| Rate limit: Login API | 5 request/menit per IP | ✅ |
| Rate limit: Order API | 10 request/menit per user | ✅ |

---

## 5. ROLE-BASED ACCESS MATRIX

### Admin Panel Access Control

| Modul | Aksi | superadmin | admin | finance | staff |
|---|---|---|---|---|---|
| **Dashboard** | View | ✅ | ✅ | ✅ | ✅ |
| **User Management** | View list | ✅ | ✅ | ✅ | ✅ |
| | View detail | ✅ | ✅ | ✅ | ✅ |
| | Edit level | ✅ | ✅ | ❌ | ❌ |
| | Suspend user | ✅ | ❌ | ❌ | ❌ |
| | Balance adjustment | ✅ | ❌ | ✅ | ❌ |
| **Product** | View | ✅ | ✅ | ✅ | ✅ |
| | Create/Edit | ✅ | ✅ | ❌ | ❌ |
| | Set harga per level | ✅ | ✅ | ❌ | ❌ |
| | Mapping supplier | ✅ | ✅ | ❌ | ❌ |
| **Supplier** | View | ✅ | ✅ | ✅ | ✅ |
| | Create/Edit | ✅ | ✅ | ❌ | ❌ |
| | Deposit | ✅ | ❌ | ✅ | ❌ |
| **Transaction** | View | ✅ | ✅ | ✅ | ✅ |
| | Create manual | ✅ | ✅ | ❌ | ❌ |
| | Refund | ✅ | ❌ | ✅ | ❌ |
| | Check status | ✅ | ✅ | ✅ | ✅ |
| **Topup** | View | ✅ | ✅ | ✅ | ✅ |
| | Approve/Reject | ✅ | ✅ | ✅ | ❌ |
| **Referral** | View | ✅ | ❌ | ✅ | ❌ |
| | Beri bonus | ✅ | ❌ | ✅ | ❌ |
| **Reports** | View | ✅ | ❌ | ✅ | ❌ |
| | Export | ✅ | ❌ | ✅ | ❌ |
| **Settings** | View | ✅ | ❌ | ❌ | ❌ |
| | Edit | ✅ | ❌ | ❌ | ❌ |

---

## 6. USE CASE FLOW DETAIL

### 6.1 Flow: Approve Topup

**Trigger:** Admin melihat notifikasi ada topup pending

**Preconditions:**
- User sudah submit topup request dengan bukti transfer
- Status topup = `pending`

**Steps:**
1. Admin buka `/admin/topup`
2. Klik baris topup pending → redirect ke `/admin/topup/{id}`
3. Admin lihat:
   - Data user (nama, level, saldo saat ini)
   - Nominal request
   - Bukti transfer (preview image)
4. Admin klik **[Approve]**
5. Modal muncul:
   - Input nominal final (pre-fill dari request, bisa diubah jika transfer kurang/lebih)
   - Input catatan (opsional)
   - Tombol [Konfirmasi Approve]
6. Sistem validasi: nominal > 0
7. Sistem eksekusi (dalam 1 DB transaction):
   - `SELECT ... FROM user_balances WHERE user_id = ? FOR UPDATE`
   - `UPDATE user_balances SET active_balance = active_balance + nominal`
   - `INSERT INTO balance_ledger (type='topup', amount=nominal, ...)`
   - `UPDATE topup_requests SET status='approved', approved_by=admin_id, approved_at=NOW()`
   - Queue job Hangfire: kirim notifikasi (email + WhatsApp)
8. Success message: "Topup berhasil disetujui"
9. Redirect ke `/admin/topup` (pending list)

**Postconditions:**
- Saldo user bertambah
- `balance_ledger` tercatat 1 baris baru
- User terima notifikasi

**Alternative Flow — Reject:**
- Step 4: Admin klik **[Reject]**
- Modal: Input alasan penolakan (wajib)
- Sistem: `UPDATE topup_requests SET status='rejected', reject_reason=...`
- Notifikasi reject dikirim ke user

---

### 6.2 Flow: Buat Transaksi (Member Order via API)

**Trigger:** User di mobile app klik "Beli Pulsa"

**Preconditions:**
- User sudah login (punya JWT token)
- User sudah verify PIN (punya `pin_session_token`)
- Saldo user >= harga produk

**Steps:**
1. Mobile app kirim:
   ```
   POST /api/v1/transactions
   Headers:
     Authorization: Bearer <jwt>
     X-Reference-Id: "app-{user_id}-{timestamp}-{random}"
   Body:
     {
       "product_id": "uuid-produk",
       "destination": "08123456789",
       "pin_session_token": "xxx"
     }
   ```

2. API Middleware validasi:
   - JWT valid?
   - `X-Reference-Id` unik? (cek Redis + DB `idempotency_keys`)
   - Rate limit: user belum exceed 10 order/menit?

3. API Controller:
   - Validasi `pin_session_token` (cek Redis, valid < 5 menit?)
   - Ambil harga produk sesuai level user:
     ```sql
     SELECT plp.sell_price
     FROM product_level_prices plp
     JOIN users u ON u.level_id = plp.level_id
     WHERE plp.product_id = ? AND u.id = ?
     ```

4. Service layer — `TransactionService.CreateTransaction()`:
   - BEGIN DB TRANSACTION
   - Cek saldo: `SELECT active_balance FROM user_balances WHERE user_id = ? FOR UPDATE`
   - Jika saldo < harga → ROLLBACK, return error `INSUFFICIENT_BALANCE`
   - Call `hold_balance(user_id, sell_price, 'transaction', trx_id)`
   - `INSERT INTO transactions (reference_id, user_id, product_id, destination, sell_price, status='pending')`
   - `INSERT INTO idempotency_keys (key=reference_id, user_id, transaction_id)`
   - COMMIT

5. Background job (Hangfire) — `ProcessTransactionJob`:
   - Ambil supplier routing: `SELECT * FROM supplier_products WHERE product_id=? ORDER BY seq ASC`
   - Loop supplier (seq 1, 2, 3, ...):
     - `INSERT INTO transaction_attempts (transaction_id, supplier_id, seq, status='processing')`
     - Panggil `ISupplierAdapter.Purchase()`
     - **SUKSES:**
       - `UPDATE transaction_attempts SET status='success', supplier_trx_id, sn, completed_at`
       - `UPDATE transactions SET status='success', cost_price, sn, completed_at`
       - Call `debit_held_balance(user_id, sell_price)`
       - `INSERT INTO profit_ledger`
       - Call `debit_supplier_balance(supplier_id, cost_price)`
       - Queue notifikasi sukses → BREAK loop
     - **GAGAL:**
       - `UPDATE transaction_attempts SET status='failed', error_code, error_message`
       - Lanjut ke supplier seq berikutnya
   - Jika semua supplier gagal:
     - `UPDATE transactions SET status='failed'`
     - Call `release_held_balance(user_id, sell_price)`
     - Queue notifikasi gagal

6. API response (langsung setelah step 4 commit):
   ```json
   {
     "success": true,
     "data": {
       "transaction_id": "uuid",
       "reference_id": "...",
       "status": "pending",
       "product_name": "Pulsa Indosat 5.000",
       "destination": "08123456789",
       "price": 5600
     }
   }
   ```

**Postconditions:**
- Saldo user ditahan (`held_balance += harga`)
- Transaksi tercatat dengan status `pending` → background job proses asinkron
- User bisa cek status via `GET /transactions/{id}`

---

### 6.3 Flow: Deposit ke Supplier

**Trigger:** Admin melihat saldo supplier menipis (merah/kuning)

**Steps:**
1. Admin buka `/admin/suppliers/{id}` → Tab Deposit
2. Klik **[Tambah Deposit]**
3. Form:
   - Nominal (IDR)
   - Upload bukti transfer
   - Catatan
4. Submit → sistem:
   - `INSERT INTO supplier_deposits (supplier_id, amount, transfer_proof_url, confirmed_by=admin_id, confirmed_at=NOW())`
   - Call `credit_supplier_balance(supplier_id, amount, 'deposit', 'supplier_deposit', deposit_id)`
   - Flash success: "Deposit berhasil dicatat"
5. Widget saldo supplier di dashboard berubah warna (merah → kuning/hijau)

**Postconditions:**
- Saldo supplier bertambah
- `supplier_balance_ledger` tercatat 1 baris baru (type=`deposit`)

---

### 6.4 Flow: Refund Manual

**Trigger:** Customer komplain transaksi gagal tapi saldo kena potong (edge case, seharusnya otomatis refund)

**Preconditions:**
- Transaksi status = `failed` atau `success` (salah proses)
- Admin role = superadmin atau finance

**Steps:**
1. Admin buka `/admin/transactions/{id}`
2. Klik **[Refund Manual]**
3. Modal konfirmasi:
   - Nominal refund (pre-fill = sell_price, bisa diubah)
   - Alasan refund (wajib diisi)
   - Password admin untuk konfirmasi
4. Submit → sistem validasi password admin
5. Sistem eksekusi:
   - BEGIN TRANSACTION
   - `SELECT ... FROM user_balances WHERE user_id = ? FOR UPDATE`
   - `UPDATE user_balances SET active_balance = active_balance + nominal`
   - `INSERT INTO balance_ledger (type='refund', ref_type='transaction', ref_id=trx_id, notes=alasan)`
   - `UPDATE transactions SET status='refunded', updated_at=NOW()`
   - `INSERT INTO audit_logs (actor_type='admin', actor_id=admin_id, action='transaction.refund', entity='transaction', entity_id=trx_id)`
   - COMMIT
6. Queue notifikasi refund ke user
7. Flash success + redirect ke detail transaksi

**Postconditions:**
- Saldo user bertambah
- Transaksi status berubah jadi `refunded`
- Audit log tercatat

---

## 7. API CONTRACT EXAMPLES

### 7.1 POST /api/v1/auth/register

**Request:**
```json
POST /api/v1/auth/register
Content-Type: application/json

{
  "username": "user123",
  "full_name": "John Doe",
  "email": "john@example.com",
  "phone": "08123456789",
  "pin": "123456",
  "referral_code": "AGUS2026"  // opsional
}
```

**Response Success (201):**
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

**Response Error (400):**
```json
{
  "success": false,
  "error_code": "USERNAME_TAKEN",
  "message": "Username sudah digunakan",
  "details": {
    "field": "username"
  }
}
```

---

### 7.2 POST /api/v1/transactions

**Request:**
```json
POST /api/v1/transactions
Authorization: Bearer <REDACTED_JWT_TOKEN>
X-Reference-Id: app-uuid-user-1710234567-a1b2c3
Content-Type: application/json

{
  "product_id": "uuid-produk-indosat-5k",
  "destination": "08123456789",
  "pin_session_token": "pin-session-xyz123"
}
```

**Response Success (201):**
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

**Response Error (400 - Insufficient Balance):**
```json
{
  "success": false,
  "error_code": "INSUFFICIENT_BALANCE",
  "message": "Saldo tidak mencukupi",
  "details": {
    "required": 5600,
    "available": 3000
  }
}
```

**Response Error (409 - Duplicate):**
```json
{
  "success": false,
  "error_code": "DUPLICATE_TRANSACTION",
  "message": "Transaksi dengan reference_id ini sudah pernah dibuat",
  "details": {
    "existing_transaction_id": "uuid-transaksi-sebelumnya",
    "status": "success"
  }
}
```

---

### 7.3 GET /api/v1/balance

**Request:**
```
GET /api/v1/balance
Authorization: Bearer <REDACTED_JWT_TOKEN>
```

**Response Success (200):**
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

---

### 7.4 POST /api/v1/transfer

**Request:**
```json
POST /api/v1/transfer
Authorization: Bearer <REDACTED_JWT_TOKEN>
X-Reference-Id: transfer-uuid-user-1710234567-d4e5f6
Content-Type: application/json

{
  "to_username": "user456",
  "amount": 50000,
  "notes": "Bayar utang",
  "pin_session_token": "pin-session-xyz123"
}
```

**Response Success (201):**
```json
{
  "success": true,
  "message": "Transfer berhasil",
  "data": {
    "transfer_id": "uuid-transfer",
    "from": "user123",
    "to": "user456",
    "amount": 50000,
    "notes": "Bayar utang",
    "created_at": "2026-03-12T05:10:00Z"
  }
}
```

**Response Error (403 - Transfer Disabled):**
```json
{
  "success": false,
  "error_code": "TRANSFER_DISABLED",
  "message": "Akun Anda tidak diizinkan melakukan transfer"
}
```

---

## 8. ERROR CODE STANDARD (API)

| Code | HTTP Status | Deskripsi | Contoh Skenario |
|---|---|---|---|
| `INVALID_CREDENTIALS` | 401 | Username/password salah | Login gagal |
| `UNAUTHORIZED` | 401 | Token tidak valid/expired | JWT expired |
| `PIN_INVALID` | 400 | PIN salah | Verify PIN gagal |
| `PIN_LOCKED` | 403 | Akun terkunci karena PIN salah berkali-kali | Salah PIN 3x |
| `INSUFFICIENT_BALANCE` | 400 | Saldo tidak cukup | Order tapi saldo kurang |
| `PRODUCT_NOT_FOUND` | 404 | Produk tidak ditemukan | Product ID salah |
| `PRODUCT_INACTIVE` | 400 | Produk tidak aktif | Produk di-disable admin |
| `DUPLICATE_TRANSACTION` | 409 | Reference ID sudah pernah dipakai | Idempotency hit |
| `TRANSFER_DISABLED` | 403 | User tidak boleh transfer | `can_transfer = false` |
| `RATE_LIMIT_EXCEEDED` | 429 | Terlalu banyak request | Login > 5x/menit |
| `SUPPLIER_TIMEOUT` | 503 | Semua supplier timeout | Supplier down |
| `VALIDATION_ERROR` | 400 | Input tidak valid | Field wajib kosong |
| `INTERNAL_ERROR` | 500 | Server error | Unhandled exception |

---

## 9. UI/UX REQUIREMENTS (ADMIN PANEL)

### 9.1 Target Layar & Breakpoint

| Target | Resolusi | Prioritas | Perilaku |
|---|---|---|---|
| Primary | 1920×1080 (Full HD) | ⭐ Utama | Sidebar expanded (240px), layout normal |
| Secondary | 2560×1440 (2K) | ✅ Didukung | Sidebar expanded, content max-width 1600px |
| Tertiary | 768×1024 (Tablet) | ✅ Didukung | Sidebar collapse (icon only), hover expand |
| Optional | 375×812 (Mobile) | ⚠️ Basic | Sidebar drawer overlay |

### 9.2 Layout Utama

```
┌──────────────────────────────────────────────────────────┐
│  TOPBAR: Logo | Breadcrumb             Bell | Avatar ▼   │
├──────────────┬───────────────────────────────────────────┤
│              │                                           │
│   SIDEBAR    │   MAIN CONTENT AREA                       │
│   (240px)    │                                           │
│              │   ┌─────────────────────────────────┐    │
│  ▸ Dashboard │   │  Page Title + Action Buttons    │    │
│  ▸ User      │   ├─────────────────────────────────┤    │
│  ▸ Produk    │   │                                 │    │
│  ▸ Supplier  │   │  Content (Table / Form / Cards) │    │
│  ▸ Transaksi │   │                                 │    │
│  ▸ Keuangan  │   └─────────────────────────────────┘    │
│  ▸ Laporan   │                                           │
│  ▸ Referral  │                                           │
│  ▸ Notifikasi│                                           │
│  ▸ Setting   │                                           │
│              │                                           │
└──────────────┴───────────────────────────────────────────┘
```

### 9.3 Dashboard Components

**KPI Cards Row 1:**
- Total Transaksi Hari Ini (count + perbandingan kemarin)
- Omzet Hari Ini (IDR + trend ↑↓)
- Profit Hari Ini (IDR + margin %)
- Transaksi Gagal (count + % dari total)

**KPI Cards Row 2:**
- Total User Aktif
- User Baru Hari Ini
- Topup Pending (count + total nominal)
- Total Saldo Semua User

**Grafik:**
- Line Chart: Transaksi per jam (hari ini vs kemarin)
- Bar Chart: Omzet 7 hari terakhir per kategori produk

**Tabel:**
- Top 5 Produk Terlaris Hari Ini
- 10 Transaksi Terbaru (realtime via SignalR)

**Widget Saldo Supplier:**
- Card per supplier: Nama | Saldo | Status (🟢🟡🔴)

### 9.4 Error Handling UI

**Timeout ke Supplier:**
- Button "Cek Status" → loading state max 30 detik
- Jika timeout: "Supplier tidak merespon, silakan coba lagi"
- Tombol [Retry]

**Validation Error:**
- Inline error message di bawah field
- Border merah pada field yang error
- Focus otomatis ke field pertama yang error

**Success Notification:**
- Toast (top-right corner), auto-hide 3 detik
- Warna hijau, icon checkmark

**Error Notification:**
- Toast (top-right corner), auto-hide 5 detik
- Warna merah, icon warning

---

## 10. PERFORMANCE & SCALABILITY

### 10.1 Database Query Guidelines

**WAJIB:**
- Semua listing admin menggunakan **server-side pagination** (DataTables)
- Query transaksi/ledger WAJIB filter tanggal (max 90 hari per query)
- Dashboard KPI menggunakan materialized view atau Redis cache (refresh tiap 1 menit)
- Laporan gunakan view: `v_profit_daily`, `v_profit_by_supplier`, dll

**DILARANG:**
- `SELECT * FROM transactions` tanpa WHERE clause
- `SELECT * FROM balance_ledger` tanpa WHERE clause
- Load semua user ke memory untuk dropdown (gunakan autocomplete API)

### 10.2 Partitioning Strategy

Tabel dengan partisi bulanan (sudah ada di schema):
- `transactions`
- `balance_ledger`
- `profit_ledger`
- `supplier_balance_ledger`
- `notification_logs`
- `audit_logs`

Gunakan `pg_partman` untuk otomasi pembuatan partisi 3 bulan ke depan.

### 10.3 Indexing Critical Paths

Hot-path queries (sudah dicover di schema SQL):
- Routing supplier: `idx_supplier_routing` (product_id, seq)
- Harga lookup: `idx_prices_lookup` (product_id, level_id)
- Transaksi pending/processing: `idx_trx_processing` (partial index)
- Balance user: primary key pada `user_balances`

### 10.4 Caching Strategy

| Data | Cache Type | TTL | Invalidation |
|---|---|---|---|
| KPI Dashboard | Redis | 1 menit | Time-based |
| Product list untuk API | Redis | 5 menit | Event-based (product update) |
| Harga per level | Redis | 10 menit | Event-based (price update) |
| Idempotency keys | Redis | 24 jam | TTL |
| Rate limit counter | Redis | 1 menit | Rolling window |
| Pin session token | Redis | 5 menit | TTL |

---

## 11. NON-FUNCTIONAL REQUIREMENTS

### 11.1 Performa
- API P95 response time < 300ms (excluding supplier call)
- Admin page load FCP < 2 detik (wired connection)
- Mendukung **ratusan transaksi per detik** (target: 200 TPS)
- Dashboard KPI realtime update via SignalR (push setiap 10 detik)

### 11.2 Keamanan
- Admin: Cookie Auth + CSRF protection (built-in ASP.NET)
- API: JWT (HS256) + Refresh Token rotation
- PIN: BCrypt hash (cost factor 12)
- API Key supplier: AES-256 encryption (app-level)
- Rate limiting: Login 5x/menit per IP, Order 10x/menit per user
- Database: REVOKE UPDATE/DELETE pada `balance_ledger`, `profit_ledger`
- Audit: Semua aksi admin tercatat di `audit_logs`

### 11.3 Reliability
- Uptime target: 99.5%
- Callback supplier disimpan raw sebelum proses (recoverable)
- Background job retry: max 3x dengan exponential backoff
- Transaksi gagal semua supplier → auto refund held balance

### 11.4 Monitoring
- Hangfire dashboard: `/hangfire` (secured, superadmin only)
- Application Insights / Serilog untuk logging
- Alert email jika:
  - Saldo supplier < threshold merah
  - Transaction stuck > 5 menit
  - Error rate API > 5%

---

## 12. ACCEPTANCE CRITERIA

### Sprint 1 ✓
- [ ] Admin bisa login/logout
- [ ] Dashboard tampil 8 KPI cards (data dummy OK)
- [ ] Layout responsive di 3 target layar
- [ ] Database migrations jalan

### Sprint 2 ✓
- [ ] CRUD user (list, detail, edit level, suspend)
- [ ] CRUD produk
- [ ] Set harga per level (inline edit table)
- [ ] CRUD level user + config

### Sprint 3 ✓
- [ ] CRUD supplier + mapping ke produk
- [ ] Transaksi manual via admin berfungsi
- [ ] Routing supplier seq berjalan
- [ ] Retry ke supplier berikutnya saat gagal
- [ ] Detail transaksi tampil timeline attempt

### Sprint 4 ✓
- [ ] Approve/reject topup mengubah saldo user
- [ ] Adjustment saldo manual berfungsi
- [ ] Deposit supplier mencatat saldo
- [ ] Laporan profit tersedia (harian, per supplier, per produk)
- [ ] Export Excel berjalan
- [ ] Referral bonus bisa dibayar manual

### Sprint 5 ✓
- [ ] Member bisa register (dengan/tanpa referral)
- [ ] Member bisa login → dapat JWT
- [ ] Member bisa cek saldo + history
- [ ] Member bisa submit topup request

### Sprint 6 ✓
- [ ] Member bisa order produk via API
- [ ] Idempotency mencegah double order
- [ ] Transfer antar member berfungsi
- [ ] Rate limiting aktif
- [ ] Notifikasi terkirim via email/SMS/WhatsApp

---

## 13. OUT OF SCOPE (v1.0)

- Payment gateway otomatis (Midtrans, Xendit)
- Multi-currency
- Downline/reseller tree
- iOS/Android native app (API sudah siap, app terpisah)
- Two-factor authentication (2FA) login admin
- Customer support ticket system
- Scheduled report email otomatis
- Mobile app push notification (gunakan polling API dulu)

---

## 14. DEPENDENCIES & RISKS

| Dependency | Risk | Mitigation |
|---|---|---|
| Supplier API stability | Supplier sering down | Minimal 2 supplier per produk, timeout 30s |
| SMS gateway quota | Habis di tengah bulan | Monitor usage harian, alert di 80% |
| Redis availability | Cache miss → performa turun | Fallback ke DB jika Redis down |
| Background job processing | Hangfire server crash | Auto-restart via systemd/Windows Service |
| Database growth | Partisi penuh | pg_partman auto-create 3 bulan ahead |

---

## 15. GLOSSARY

| Term | Definisi |
|---|---|
| **Seq** | Urutan prioritas supplier untuk 1 produk (1 = dicoba pertama) |
| **Hold Balance** | Saldo dipindahkan dari `active` ke `held` saat transaksi dimulai |
| **Debit Balance** | Saldo `held` dipotong final saat transaksi sukses |
| **Release Balance** | Saldo `held` dikembalikan ke `active` saat transaksi gagal |
| **Cost Price** | Harga beli dari supplier |
| **Sell Price** | Harga jual ke member (sudah termasuk markup) |
| **Pin Session Token** | Token temporary (5 menit) setelah PIN diverifikasi |
| **Idempotency Key** | Reference ID untuk mencegah request duplikat |
| **Supplier Adapter** | Interface untuk komunikasi ke supplier (Strategy Pattern) |

---

**END OF DOCUMENT**

Version: 1.1  
Last Updated: 2026-03-12  
Next Review: After Sprint 2 retrospective
