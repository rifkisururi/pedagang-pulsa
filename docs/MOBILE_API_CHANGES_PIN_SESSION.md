# Perubahan API: PIN Session & Transaksi

## Breaking Changes

Update ini berisi perubahan **breaking** pada alur transaksi. Wajib diimplementasikan oleh team mobile.

---

## 1. Header `X-Reference-Id` Sekarang Wajib

### Sebelum
Header `X-Reference-Id` bersifat opsional. Jika tidak dikirim, transaksi tetap diproses.

### Sesudah
Header `X-Reference-Id` **wajib** dikirim pada setiap request `POST /api/transaction`.

**Jika tidak dikirim, response:**
```json
HTTP 400
{
  "message": "X-Reference-Id header is required",
  "errorCode": "REFERENCE_ID_REQUIRED"
}
```

### Implementasi Mobile

Generate `X-Reference-Id` (GUID/UUID) **sebelum** request pertama. Jika request gagar karena network error atau timeout, **gunakan `X-Reference-Id` yang sama** untuk retry. Server akan mengembalikan response yang sama (idempotent).

```
PSEUDO CODE:

1. User tap "Beli"
2. referenceId = generateUUID()  // simpan ini
3. Kirim POST /api/transaction dengan X-Reference-Id = referenceId
4. Jika timeout / network error:
   - JANGAN generate UUID baru
   - Retry dengan X-Reference-Id = referenceId yang sama
5. Jika berhasil, hapus referenceId
```

**PENTING:** Satu `X-Reference-Id` hanya boleh dipakai untuk **satu kombinasi (ProductId, DestinationNumber)**. Jangan reuse reference ID untuk transaksi yang berbeda.

---

## 2. PIN Session Token Hanya Berlaku Sekali

### Sebelum
Setiap panggilan `POST /api/auth/pin/verify` membuat token baru. Token lama tetap valid sampai expired (5 menit). Token yang sama bisa dipakai berkali-kali untuk CreateTransaction.

### Sesudah
- Setiap panggilan `POST /api/auth/pin/verify` **meng-invalidate semua token sebelumnya**.
- Hanya **token terakhir** yang valid.
- Token hanya bisa dipakai **satu kali** pada CreateTransaction (sudah single-use sebelumnya, sekarang lebih ketat karena token lama langsung invalid).

### Skenario Duplikasi yang Dicegah

```
SEBELUM (bermasalah):
1. PIN verify -> token_A
2. PIN verify -> token_B          (token_A masih valid!)
3. CreateTransaction(token_A) -> transaksi 1
4. CreateTransaction(token_B) -> transaksi 2  (DUPLIKAT!)

SESUDAH (diperbaiki):
1. PIN verify -> token_A
2. PIN verify -> token_B          (token_A SEKARANG INVALID)
3. CreateTransaction(token_A) -> REJECTED: "PIN session has been superseded"
4. CreateTransaction(token_B) -> transaksi 1 (OK)
```

### Implementasi Mobile

- Simpan **hanya satu** `PinSessionToken` per user di memory.
- Setiap kali dapat response dari PIN verify, **replace** token yang disimpan.
- Jangan simpan multiple token atau queue token.
- Jika CreateTransaction gagal dengan `INVALID_PIN_SESSION`, **minta user verifikasi PIN lagi** — jangan auto-retry.

---

## 3. Rate Limiting Aktif

### Sebelum
Rate limiting tidak aktif.

### Sesudah
Rate limiting aktif dengan limit berikut per user:

| Endpoint | Limit |
|----------|-------|
| `POST /api/transaction` | 10 request / menit |
| `POST /api/auth/pin/verify` | 10 request / menit |

**Response saat limit tercapai:**
```json
HTTP 429
{
  "success": false,
  "message": "Rate limit exceeded. Maximum 10 requests per 1 minute(s).",
  "errorCode": "RATE_LIMIT_EXCEEDED"
}
```

### Headers Response (selalu dikirim)

| Header | Deskripsi |
|--------|-----------|
| `X-RateLimit-Limit` | Maksimal request per window |
| `X-RateLimit-Remaining` | Sisa request yang tersedia |
| `X-RateLimit-Reset` | Waktu reset (ISO 8601 UTC) |

### Implementasi Mobile

- Baca header `X-RateLimit-Remaining` dari response.
- Jika `X-RateLimit-Remaining == 0`, disable button transaksi sampai waktu reset.
- Jika dapat 429, baca header `Retry-After` (dalam detik) dan tampilkan countdown ke user.

---

## 4. Error Code Baru

| Error Code | HTTP Status | Deskripsi | Aksi Mobile |
|------------|-------------|-----------|-------------|
| `REFERENCE_ID_REQUIRED` | 400 | Header `X-Reference-Id` tidak dikirim | Generate UUID dan kirim ulang |
| `INVALID_PIN_SESSION` | 401 | Token PIN sudah expired, sudah dipakai, atau sudah di-supersede | Minta user verifikasi PIN lagi |
| `RATE_LIMIT_EXCEEDED` | 429 | Terlalu banyak request | Tampilkan countdown, disable button |

---

## 5. Alur Lengkap Transaksi (Updated)

```
[Mobile App]                          [API Server]

1. User tap "Beli"
   |
2. POST /api/auth/pin/verify          --->  Verify PIN
   { "pin": "123456" }                     |
                                          v
   <---  200 OK                          Return pinSessionToken
   { "pinSessionToken": "abc...", "expiresIn": 300 }
   |
3. Generate X-Reference-Id (UUID)
4. POST /api/transaction               --->  Validate + consume PIN session
   Header: X-Reference-Id: <uuid>            Check idempotency
   Body: { productId, destinationNumber,     Hold balance
           pinSessionToken }                 Create transaction
                                             Consume PIN session
                                          v
   <---  201 Created                      Return transaction
   { "referenceId": "...", "status": "pending", ... }
   |
5. Tampilkan status transaksi ke user

--- JIKA ERROR ---

A. Network timeout / 5xx:
   -> Retry dengan X-Reference-Id YANG SAMA (jangan generate baru)
   -> Jika sudah 3x gagal, tampilkan error ke user

B. INVALID_PIN_SESSION (401):
   -> Token sudah tidak valid
   -> JANGAN auto-retry CreateTransaction
   -> Minta user verifikasi PIN lagi (kembali ke step 2)

C. REFERENCE_ID_REQUIRED (400):
   -> Bug di mobile, harus selalu kirim header
   -> Generate UUID dan kirim ulang

D. RATE_LIMIT_EXCEEDED (429):
   -> Tampilkan: "Terlalu banyak request. Coba lagi dalam X detik."
   -> Baca header Retry-After untuk countdown
   -> Disable button sampai countdown selesai

E. INSUFFICIENT_BALANCE (400):
   -> Tampilkan: "Saldo tidak mencukupi"
   -> Arahkan ke halaman topup

F. Response 200 dengan data transaksi yang sama:
   -> Ini artinya idempotency berhasil (retry yang kedua)
   -> Tampilkan transaksi seperti biasa, ini BUKAN duplikasi
```

---

## 6. Checklist Integration

- [ ] Setiap request `POST /api/transaction` mengirim header `X-Reference-Id` (UUID)
- [ ] Generate `X-Reference-Id` sebelum request pertama, reuse untuk retry
- [ ] Simpan hanya satu `PinSessionToken` di memory, replace setiap verify
- [ ] Tangani error `INVALID_PIN_SESSION` dengan meminta user verify PIN lagi
- [ ] Tangani error `RATE_LIMIT_EXCEEDED` dengan countdown berdasarkan header `Retry-After`
- [ ] Tangani response 200 pada CreateTransaction sebagai idempotent success
- [ ] Jangan auto-retry pada 4xx error (kecuali 429 setelah countdown selesai)
- [ ] Hanya auto-retry pada network timeout / 5xx dengan `X-Reference-Id` yang sama
