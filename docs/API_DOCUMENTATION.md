# PedagangPulsa API Documentation
## Member Mobile API v1.0

**Base URL:** `https://api.pedagangpulsa.com/api/v1`
**Documentation Version:** 1.0
**Last Updated:** 2026-03-12

---

## Table of Contents

1. [Authentication](#authentication)
2. [Common Headers](#common-headers)
3. [Error Handling](#error-handling)
4. [Rate Limiting](#rate-limiting)
5. [API Endpoints](#api-endpoints)
   - [Auth](#auth)
   - [Balance](#balance)
   - [Topup](#topup)
   - [Products](#products)
   - [Transactions](#transactions)
   - [Transfers](#transfers)
   - [Notifications](#notifications)

---

## Authentication

### Overview

The API uses **JWT (JSON Web Token)** based authentication with a **2-step verification process**:

1. **Login** → Get Access Token + Refresh Token
2. **PIN Verify** → Get PIN Session Token (for sensitive operations)

### Authentication Flow

```
Step 1: Login with username + PIN
  ↓
Receive: access_token (15min) + refresh_token (7days)
  ↓
Use access_token in Authorization header for all requests
  ↓
Step 2: For sensitive operations (transaction, transfer):
  Verify PIN → Get pin_session_token (5min)
  ↓
Include pin_session_token in request body
```

### Token Types

| Token | Purpose | Expiry | Usage |
|---|---|---|---|
| `access_token` | API authentication | 15 minutes | Header: `Authorization: Bearer {token}` |
| `refresh_token` | Get new access token | 7 days | POST `/auth/refresh` |
| `pin_session_token` | Sensitive operations | 5 minutes | Request body for transaction/transfer |

---

## Common Headers

### Required Headers

All API requests must include:

```http
Authorization: Bearer {access_token}
Content-Type: application/json
X-Device-ID: {unique_device_id}
X-App-Version: 1.0.0
```

### Idempotency Header (For Transactions & Transfers)

```http
X-Reference-Id: app-{user_id}-{timestamp}-{random}
```

**Example:** `X-Reference-Id: app-123e4567-e89b-12d3-a456-426614174000-1710234567-a1b2c3`

**Purpose:** Prevents duplicate transaction/transfer requests. Must be unique per request.

---

## Error Handling

### Standard Error Response Format

All errors follow this format:

```json
{
  "success": false,
  "error_code": "ERROR_CODE",
  "message": "Human readable error message",
  "details": {
    "additional_info": "Optional additional details"
  }
}
```

### Error Codes Reference

| Error Code | HTTP Status | Description | Retry |
|---|---|---|---|
| `INVALID_CREDENTIALS` | 401 | Username or PIN is incorrect | No |
| `UNAUTHORIZED` | 401 | Token is invalid or expired | Yes (refresh token) |
| `PIN_INVALID` | 400 | PIN is incorrect | No |
| `PIN_LOCKED` | 403 | Account locked (3 failed PIN attempts) | No (wait 15 min) |
| `INSUFFICIENT_BALANCE` | 400 | Balance is not enough | No |
| `PRODUCT_NOT_FOUND` | 404 | Product does not exist | No |
| `PRODUCT_INACTIVE` | 400 | Product is not active | No |
| `DUPLICATE_TRANSACTION` | 409 | Reference ID already exists | No (check existing) |
| `TRANSFER_DISABLED` | 403 | User account cannot transfer | No |
| `RATE_LIMIT_EXCEEDED` | 429 | Too many requests | Yes (wait) |
| `SUPPLIER_TIMEOUT` | 503 | All suppliers are timeout | Yes (auto retry) |
| `VALIDATION_ERROR` | 400 | Invalid input data | No |
| `INTERNAL_ERROR` | 500 | Server error | Yes |

---

## Rate Limiting

### Rate Limits per Endpoint

| Endpoint | Limit | Per | Based On |
|---|---|---|---|
| `POST /auth/login` | 5 requests | 1 minute | IP Address |
| `POST /auth/pin/verify` | 10 requests | 1 minute | User ID |
| `POST /transactions` | 10 requests | 1 minute | User ID |
| `POST /transfer` | 10 requests | 1 minute | User ID |
| `GET /` (any GET) | 60 requests | 1 minute | User ID |

### Rate Limit Response Headers

```http
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 7
X-RateLimit-Reset: 1710234567
```

### Rate Limit Error Response

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

---

## API Endpoints

---

## Auth

### 1. Register

Create a new user account.

**Endpoint:** `POST /auth/register`
**Authentication:** No
**Rate Limit:** 5 requests per minute per IP

#### Request

```http
POST /api/v1/auth/register
Content-Type: application/json

{
  "username": "user123",
  "full_name": "John Doe",
  "email": "john@example.com",
  "phone": "08123456789",
  "pin": "123456",
  "referral_code": "AGUS2026"
}
```

#### Request Fields

| Field | Type | Required | Validation | Description |
|---|---|---|---|---|
| `username` | string | Yes | 3-20 chars, alphanumeric | Unique username |
| `full_name` | string | Yes | Max 100 chars | User's full name |
| `email` | string | Yes | Valid email format | Email address |
| `phone` | string | Yes | Indonesia format (08xxx or 628xxx) | Phone number |
| `pin` | string | Yes | Exactly 6 digits | 6-digit PIN |
| `referral_code` | string | No | Must exist if provided | Referral code |

#### Success Response (201 Created)

```json
{
  "success": true,
  "message": "Registrasi berhasil",
  "data": {
    "user_id": "550e8400-e29b-41d4-a716-446655440000",
    "username": "user123",
    "referral_code": "USER123ABCD",
    "level": "member1"
  }
}
```

#### Error Responses

**400 USERNAME_TAKEN**
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

**400 REFERRAL_INVALID**
```json
{
  "success": false,
  "error_code": "REFERRAL_INVALID",
  "message": "Kode referral tidak valid"
}
```

---

### 2. Login

Authenticate with username and PIN.

**Endpoint:** `POST /auth/login`
**Authentication:** No
**Rate Limit:** 5 requests per minute per IP

#### Request

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "username": "user123",
  "pin": "123456"
}
```

#### Success Response (200 OK)

```json
{
  "success": true,
  "message": "Login berhasil",
  "data": {
    "access_token": "<REDACTED_JWT_TOKEN>",
    "refresh_token": "<REDACTED_JWT_TOKEN>",
    "expires_in": 900,
    "token_type": "Bearer",
    "user": {
      "user_id": "550e8400-e29b-41d4-a716-446655440000",
      "username": "user123",
      "full_name": "John Doe",
      "email": "john@example.com",
      "phone": "08123456789",
      "level": "member1",
      "can_transfer": true
    }
  }
}
```

#### Error Responses

**401 INVALID_CREDENTIALS**
```json
{
  "success": false,
  "error_code": "INVALID_CREDENTIALS",
  "message": "Username atau PIN salah"
}
```

**403 ACCOUNT_SUSPENDED**
```json
{
  "success": false,
  "error_code": "ACCOUNT_SUSPENDED",
  "message": "Akun Anda ditangguhkan. Silakan hubungi admin."
}
```

---

### 3. Refresh Token

Get a new access token using refresh token.

**Endpoint:** `POST /auth/refresh`
**Authentication:** No
**Rate Limit:** 20 requests per minute per IP

#### Request

```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refresh_token": "<REDACTED_JWT_TOKEN>"
}
```

#### Success Response (200 OK)

```json
{
  "success": true,
  "message": "Token berhasil diperbarui",
  "data": {
    "access_token": "<REDACTED_JWT_TOKEN>",
    "refresh_token": "<REDACTED_JWT_TOKEN>",
    "expires_in": 900,
    "token_type": "Bearer"
  }
}
```

---

### 4. Verify PIN

Verify PIN for sensitive operations (transaction, transfer).

**Endpoint:** `POST /auth/pin/verify`
**Authentication:** Required (Bearer token)
**Rate Limit:** 10 requests per minute per user

#### Request

```http
POST /api/v1/auth/pin/verify
Authorization: Bearer {access_token}
Content-Type: application/json

{
  "pin": "123456"
}
```

#### Success Response (200 OK)

```json
{
  "success": true,
  "message": "PIN verified",
  "data": {
    "pin_session_token": "550e8400-e29b-41d4-a716-446655440001",
    "expires_in": 300
  }
}
```

#### Error Responses

**400 PIN_INVALID**
```json
{
  "success": false,
  "error_code": "PIN_INVALID",
  "message": "PIN salah",
  "details": {
    "remaining_attempts": 2
  }
}
```

**403 PIN_LOCKED**
```json
{
  "success": false,
  "error_code": "PIN_LOCKED",
  "message": "Akun terkunci karena terlalu banyak percobaan PIN yang salah",
  "details": {
    "lockout_ends_in": 900,
    "lockout_ends_at": "2026-03-12T10:45:00Z"
  }
}
```

---

## Balance

### 5. Get Balance

Get current user balance.

**Endpoint:** `GET /balance`
**Authentication:** Required (Bearer token)

#### Request

```http
GET /api/v1/balance
Authorization: Bearer {access_token}
```

#### Success Response (200 OK)

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

### 6. Get Balance History

Get balance mutation history.

**Endpoint:** `GET /balance/history`
**Authentication:** Required (Bearer token)

#### Request

```http
GET /api/v1/balance/history?page=1&page_size=20
Authorization: Bearer {access_token}
```

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `page` | integer | No | 1 | Page number |
| `page_size` | integer | No | 20 | Items per page (max 100) |

#### Success Response (200 OK)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440002",
        "type": "transaction",
        "amount": -5600,
        "balance_after": 150000,
        "description": "Pembelian Pulsa Indosat 5.000 ke 08123456789",
        "ref_type": "transaction",
        "ref_id": "550e8400-e29b-41d4-a716-446655440003",
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

#### Balance Types

| Type | Amount | Description |
|---|---|---|
| `topup` | Positive | Topup approved |
| `transaction` | Negative | Product purchase |
| `refund` | Positive | Transaction refunded |
| `adjustment` | Positive/Negative | Manual adjustment by admin |
| `referral` | Positive | Referral bonus received |
| `transfer_in` | Positive | Transfer received |
| `transfer_out` | Negative | Transfer sent |

---

## Topup

### 7. Request Topup

Submit a topup request with proof of transfer.

**Endpoint:** `POST /topup`
**Authentication:** Required (Bearer token)

#### Request

```http
POST /api/v1/topup
Authorization: Bearer {access_token}
Content-Type: multipart/form-data

amount: 100000
bank_code: BCA
transfer_proof: [file]
notes: Optional notes
```

#### Form Fields

| Field | Type | Required | Validation |
|---|---|---|---|
| `amount` | decimal | Yes | Min: 10,000, Max: 10,000,000 |
| `bank_code` | string | Yes | Valid bank code |
| `transfer_proof` | file | Yes | Image (JPG/PNG), Max 5MB |
| `notes` | string | No | Max 500 chars |

#### Success Response (201 Created)

```json
{
  "success": true,
  "message": "Permintaan topup berhasil dibuat",
  "data": {
    "request_id": "550e8400-e29b-41d4-a716-446655440004",
    "amount": 100000,
    "status": "pending",
    "bank_code": "BCA",
    "transfer_proof_url": "https://cdn.pedagangpulsa.com/proof/abc123.jpg",
    "created_at": "2026-03-12T10:30:00Z"
  }
}
```

---

### 8. Get Topup History

Get user's topup request history.

**Endpoint:** `GET /topup/history`
**Authentication:** Required (Bearer token)

#### Request

```http
GET /api/v1/topup/history?page=1&page_size=20&status=pending
Authorization: Bearer {access_token}
```

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `page` | integer | No | 1 | Page number |
| `page_size` | integer | No | 20 | Items per page |
| `status` | string | No | all | Filter: pending, approved, rejected |

#### Success Response (200 OK)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "request_id": "550e8400-e29b-41d4-a716-446655440004",
        "amount": 100000,
        "status": "pending",
        "bank_code": "BCA",
        "transfer_proof_url": "https://cdn.pedagangpulsa.com/proof/abc123.jpg",
        "notes": null,
        "reject_reason": null,
        "created_at": "2026-03-12T10:30:00Z",
        "approved_at": null,
        "rejected_at": null
      }
    ],
    "pagination": {
      "page": 1,
      "page_size": 20,
      "total_items": 5,
      "total_pages": 1
    }
  }
}
```

---

## Products

### 9. List Products

Get list of available products with prices for user's level.

**Endpoint:** `GET /products`
**Authentication:** Required (Bearer token)
**Cache:** 5 minutes

#### Request

```http
GET /api/v1/products?category=Pulsa&page=1&page_size=20
Authorization: Bearer {access_token}
```

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `category` | string | No | all | Product category filter |
| `operator` | string | No | all | Operator filter |
| `type` | string | No | all | Type filter: Pulsa, Data, PLN, Game, PPOB, E-Money |
| `page` | integer | No | 1 | Page number |
| `page_size` | integer | No | 20 | Items per page (max 100) |

#### Success Response (200 OK)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440005",
        "sku": "TSEL5",
        "name": "Pulsa Telkomsel 5.000",
        "category": "Pulsa",
        "operator": "Telkomsel",
        "type": "Pulsa",
        "description": "Pulsa Telkomsel 5.000",
        "image_url": "https://cdn.pedagangpulsa.com/products/tsel5.jpg",
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

#### Product Categories

- `Pulsa` - Mobile credit/topup
- `Data` - Data packages
- `PLN` - Electricity tokens
- `Game` - Game vouchers
- `PPOB` - Bill payments
- `E-Money` - E-wallet topups

---

## Transactions

### 10. Create Transaction

Purchase a product.

**⚠️ IMPORTANT:** This endpoint requires both `X-Reference-Id` header (for idempotency) AND `pin_session_token` in request body.

**Endpoint:** `POST /transactions`
**Authentication:** Required (Bearer token)
**Idempotency:** Required
**Rate Limit:** 10 requests per minute per user

#### Request

```http
POST /api/v1/transactions
Authorization: Bearer {access_token}
X-Reference-Id: app-550e8400-1710234567-a1b2c3
Content-Type: application/json

{
  "product_id": "550e8400-e29b-41d4-a716-446655440005",
  "destination": "08123456789",
  "pin_session_token": "550e8400-e29b-41d4-a716-446655440001"
}
```

#### Request Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `product_id` | UUID | Yes | Product ID from `/products` |
| `destination` | string | Yes | Destination number (phone, meter number, etc.) |
| `pin_session_token` | string | Yes | Token from `/auth/pin/verify` |

#### X-Reference-Id Format

Must be unique per request. Suggested format:
```
app-{user_id}-{timestamp}-{random}

Example: app-550e8400-e29b-41d4-a716-446655440000-1710234567-a1b2c3
```

#### Success Response (201 Created)

```json
{
  "success": true,
  "message": "Transaksi berhasil dibuat",
  "data": {
    "transaction_id": "550e8400-e29b-41d4-a716-446655440006",
    "reference_id": "app-550e8400-1710234567-a1b2c3",
    "status": "pending",
    "product": {
      "id": "550e8400-e29b-41d4-a716-446655440005",
      "name": "Pulsa Telkomsel 5.000",
      "operator": "Telkomsel",
      "type": "Pulsa"
    },
    "destination": "08123456789",
    "price": 5600,
    "created_at": "2026-03-12T04:55:00Z"
  }
}
```

**Note:** Transaction is processed asynchronously. Status can be checked via `GET /transactions/{id}`.

#### Error Responses

**400 INSUFFICIENT_BALANCE**
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

**400 PIN_SESSION_INVALID**
```json
{
  "success": false,
  "error_code": "PIN_SESSION_INVALID",
  "message": "Sesi PIN tidak valid atau telah kadaluarsa"
}
```

**409 DUPLICATE_TRANSACTION**
```json
{
  "success": false,
  "error_code": "DUPLICATE_TRANSACTION",
  "message": "Transaksi dengan reference_id ini sudah pernah dibuat",
  "details": {
    "existing_transaction_id": "550e8400-e29b-41d4-a716-446655440006",
    "status": "success"
  }
}
```

---

### 11. List Transactions

Get user's transaction history.

**Endpoint:** `GET /transactions`
**Authentication:** Required (Bearer token)

#### Request

```http
GET /api/v1/transactions?status=success&page=1&page_size=20
Authorization: Bearer {access_token}
```

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `status` | string | No | all | Filter: pending, processing, success, failed |
| `start_date` | date | No | 90 days ago | Filter start date (ISO 8601) |
| `end_date` | date | No | today | Filter end date (ISO 8601) |
| `page` | integer | No | 1 | Page number |
| `page_size` | integer | No | 20 | Items per page |

#### Success Response (200 OK)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "transaction_id": "550e8400-e29b-41d4-a716-446655440006",
        "reference_id": "app-550e8400-1710234567-a1b2c3",
        "status": "success",
        "product": {
          "id": "550e8400-e29b-41d4-a716-446655440005",
          "name": "Pulsa Telkomsel 5.000",
          "operator": "Telkomsel"
        },
        "destination": "08123456789",
        "price": 5600,
        "sn": "1234567890123456789",
        "created_at": "2026-03-12T10:30:00Z",
        "completed_at": "2026-03-12T10:30:25Z"
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

#### Transaction Status Values

| Status | Description |
|---|---|
| `pending` | Transaction created, waiting to process |
| `processing` | Being processed by supplier |
| `success` | Transaction completed successfully |
| `failed` | All suppliers failed |
| `refunded` | Refunded by admin |

---

### 12. Get Transaction Detail

Get detailed transaction information including attempt timeline.

**Endpoint:** `GET /transactions/{id}`
**Authentication:** Required (Bearer token)

#### Request

```http
GET /api/v1/transactions/550e8400-e29b-41d4-a716-446655440006
Authorization: Bearer {access_token}
```

#### Success Response (200 OK)

```json
{
  "success": true,
  "data": {
    "transaction_id": "550e8400-e29b-41d4-a716-446655440006",
    "reference_id": "app-550e8400-1710234567-a1b2c3",
    "status": "success",
    "product": {
      "id": "550e8400-e29b-41d4-a716-446655440005",
      "name": "Pulsa Telkomsel 5.000",
      "sku": "TSEL5",
      "operator": "Telkomsel",
      "category": "Pulsa",
      "type": "Pulsa"
    },
    "destination": "08123456789",
    "price": 5600,
    "cost_price": 5200,
    "profit": 400,
    "sn": "1234567890123456789",
    "created_at": "2026-03-12T10:30:00Z",
    "completed_at": "2026-03-12T10:30:25Z",
    "attempts": [
      {
        "attempt_id": "550e8400-e29b-41d4-a716-446655440007",
        "supplier_name": "Digiflazz",
        "seq": 1,
        "status": "success",
        "created_at": "2026-03-12T10:30:05Z",
        "completed_at": "2026-03-12T10:30:25Z",
        "supplier_trx_id": "DGX123456",
        "sn": "1234567890123456789",
        "error_code": null,
        "error_message": null
      }
    ]
  }
}
```

---

## Transfers

### 13. Transfer Balance

Transfer balance to another user.

**⚠️ IMPORTANT:** Requires both `X-Reference-Id` header AND `pin_session_token`.

**Endpoint:** `POST /transfer`
**Authentication:** Required (Bearer token)
**Idempotency:** Required
**Rate Limit:** 10 requests per minute per user

#### Request

```http
POST /api/v1/transfer
Authorization: Bearer {access_token}
X-Reference-Id: transfer-550e8400-1710234567-d4e5f6
Content-Type: application/json

{
  "to_username": "user456",
  "amount": 50000,
  "notes": "Bayar utang",
  "pin_session_token": "550e8400-e29b-41d4-a716-446655440001"
}
```

#### Request Fields

| Field | Type | Required | Validation |
|---|---|---|---|
| `to_username` | string | Yes | Must exist and not be same as sender |
| `amount` | decimal | Yes | Min: 1,000, Max: 1,000,000 |
| `notes` | string | No | Max 200 chars |
| `pin_session_token` | string | Yes | Valid token from `/auth/pin/verify` |

#### Success Response (201 Created)

```json
{
  "success": true,
  "message": "Transfer berhasil",
  "data": {
    "transfer_id": "550e8400-e29b-41d4-a716-446655440008",
    "from": "user123",
    "to": "user456",
    "amount": 50000,
    "notes": "Bayar utang",
    "created_at": "2026-03-12T05:10:00Z"
  }
}
```

#### Error Responses

**403 TRANSFER_DISABLED**
```json
{
  "success": false,
  "error_code": "TRANSFER_DISABLED",
  "message": "Akun Anda tidak diizinkan melakukan transfer"
}
```

**400 SAME_USER**
```json
{
  "success": false,
  "error_code": "SAME_USER",
  "message": "Tidak dapat transfer ke akun sendiri"
}
```

**404 USER_NOT_FOUND**
```json
{
  "success": false,
  "error_code": "USER_NOT_FOUND",
  "message": "Pengguna tujuan tidak ditemukan"
}
```

---

## Notifications

### 14. List Notifications

Get user's notifications.

**Endpoint:** `GET /notifications`
**Authentication:** Required (Bearer token)

#### Request

```http
GET /api/v1/notifications?is_read=false&page=1&page_size=20
Authorization: Bearer {access_token}
```

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `is_read` | boolean | No | all | Filter read/unread |
| `page` | integer | No | 1 | Page number |
| `page_size` | integer | No | 20 | Items per page |

#### Success Response (200 OK)

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440009",
        "type": "transaction_success",
        "title": "Transaksi Berhasil",
        "message": "Pembelian Pulsa Telkomsel 5.000 ke 08123456789 berhasil. SN: 1234567890123456789",
        "is_read": false,
        "created_at": "2026-03-12T10:30:30Z"
      }
    ],
    "unread_count": 5,
    "pagination": {
      "page": 1,
      "page_size": 20,
      "total_items": 50,
      "total_pages": 3
    }
  }
}
```

#### Notification Types

| Type | Description |
|---|---|
| `transaction_success` | Transaction successful |
| `transaction_failed` | Transaction failed |
| `topup_approved` | Topup approved by admin |
| `topup_rejected` | Topup rejected by admin |
| `balance_adjustment` | Balance adjusted by admin |
| `referral_bonus` | Referral bonus received |
| `transfer_in` | Transfer received |
| `transfer_out` | Transfer sent |

---

### 15. Mark Notification as Read

Mark a single notification as read.

**Endpoint:** `POST /notifications/{id}/read`
**Authentication:** Required (Bearer token)

#### Request

```http
POST /api/v1/notifications/550e8400-e29b-41d4-a716-446655440009/read
Authorization: Bearer {access_token}
```

#### Success Response (200 OK)

```json
{
  "success": true,
  "message": "Notifikasi ditandai sebagai sudah dibaca"
}
```

---

### 16. Mark All Notifications as Read

Mark all unread notifications as read.

**Endpoint:** `POST /notifications/read-all`
**Authentication:** Required (Bearer token)

#### Request

```http
POST /api/v1/notifications/read-all
Authorization: Bearer {access_token}
```

#### Success Response (200 OK)

```json
{
  "success": true,
  "message": "Semua notifikasi ditandai sebagai sudah dibaca",
  "data": {
    "marked_count": 15
  }
}
```

---

## Additional Notes for Mobile Team

### Best Practices

1. **Token Management**
   - Store access_token securely (Keychain/Keystore)
   - Implement automatic token refresh before expiry
   - Handle 401 errors by refreshing token

2. **Idempotency Keys**
   - Generate unique X-Reference-Id for each transaction/transfer
   - Store reference_id locally in case of network failure
   - Retry with same reference_id if request fails

3. **PIN Session**
   - Verify PIN only once per session (5 minutes)
   - Re-use pin_session_token for multiple transactions
   - Don't ask PIN again if token still valid

4. **Error Handling**
   - Display user-friendly messages based on error_code
   - Implement retry logic for 5xx errors
   - Don't retry for 4xx errors (except 429)

5. **Rate Limiting**
   - Implement exponential backoff for 429 errors
   - Display countdown timer when rate limited
   - Queue requests when approaching limit

### Testing

**Sandbox Environment:** `https://sandbox-api.pedagangpulsa.com/api/v1`

**Test Credentials:**
- Username: `testuser`
- PIN: `123456`

### Support

For API-related questions, contact:
- Email: api@pedagangpulsa.com
- Documentation: https://docs.pedagangpulsa.com
- Status Page: https://status.pedagangpulsa.com

---

## Changelog

| Version | Date | Changes |
|---|---|---|
| 1.0 | 2026-03-12 | Initial API documentation for mobile team |

---

**END OF DOCUMENTATION**

© 2026 PedagangPulsa.com. All rights reserved.
