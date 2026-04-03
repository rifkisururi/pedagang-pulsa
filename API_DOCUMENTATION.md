# PedagangPulsa API Documentation

## Version: 1.0.0
## Last Updated: 2026-04-03

---

## Table of Contents
1. [Base URL](#base-url)
2. [Authentication](#authentication)
3. [Common Response Format](#common-response-format)
4. [Error Codes](#error-codes)
5. [API Endpoints](#api-endpoints)
   - [Authentication](#authentication-endpoints)
   - [Products](#product-endpoints)
   - [Transactions](#transaction-endpoints)
   - [Balance](#balance-endpoints)
   - [Top-up](#top-up-endpoints)
   - [Transfer](#transfer-endpoints)
6. [Rate Limiting](#rate-limiting)
7. [Webhooks](#webhooks)

---

## Base URL

**Production**: `https://api.pedagangpulsa.com/api`
**Staging**: `https://staging-api.pedagangpulsa.com/api`
**Development**: `https://dev-api.pedagangpulsa.com/api`

---

## Interactive API Reference

For local development, the API now exposes a Scalar reference that is easier to read and explore for mobile integration work.

- `https://localhost:7202/scalar`
- `http://localhost:5079/scalar`

Scalar is only enabled when `ASPNETCORE_ENVIRONMENT=Development`. The underlying OpenAPI JSON is still available from Swagger at `/swagger/v1/swagger.json`.

---

## Authentication

The API uses JWT (JSON Web Token) based authentication. All endpoints except `/api/auth/register` and `/api/auth/login` require authentication.

### Authentication Flow

1. **Register**: Create account with password and PIN
2. **Login**: Use username and password to get access token and refresh token
3. **Access Token**: Use in `Authorization: Bearer {token}` header
4. **PIN Verify**: Use PIN only for sensitive operations
5. **Token Refresh**: Use refresh token to get new access token

### Token Expiration

- **Access Token**: 15 minutes (900 seconds)
- **Refresh Token**: 30 days
- **PIN Session Token**: 5 minutes (300 seconds)

---

## Common Response Format

### Success Response

```json
{
  "success": true,
  "message": "Operation successful",
  "data": { ... }
}
```

### Error Response

```json
{
  "message": "Error message",
  "errorCode": "ERROR_CODE",
  "errors": ["Detailed error messages"]
}
```

---

## Error Codes

| Error Code | HTTP Status | Description |
|------------|-------------|-------------|
| `INVALID_TOKEN` | 401 | Access token is invalid or expired |
| `INVALID_CREDENTIALS` | 401 | Username or password is incorrect |
| `ACCOUNT_INACTIVE` | 401 | User account is not active |
| `ACCOUNT_LOCKED` | 429 | Account locked due to multiple failed attempts |
| `INVALID_PIN` | 401 | PIN verification failed |
| `INVALID_PIN_SESSION` | 401 | PIN session token is invalid or expired |
| `DUPLICATE_FIELD` | 400 | Username, email, or phone already exists |
| `VALIDATION_ERROR` | 400 | Request validation failed |
| `USER_NOT_FOUND` | 404 | User not found |
| `PRODUCT_NOT_FOUND` | 404 | Product not found |
| `PRODUCT_NOT_AVAILABLE` | 400 | Product not available for user's level |
| `INSUFFICIENT_BALANCE` | 400 | Insufficient balance for transaction |
| `BANK_ACCOUNT_NOT_FOUND` | 404 | Bank account not found |
| `INVALID_FILE_TYPE` | 400 | Invalid file type for upload |
| `FILE_TOO_LARGE` | 400 | File size exceeds limit |
| `RECIPIENT_NOT_FOUND` | 404 | Transfer recipient not found |
| `INVALID_TRANSFER` | 400 | Invalid transfer operation |
| `INVALID_AMOUNT` | 400 | Invalid amount specified |
| `TRANSACTION_NOT_FOUND` | 404 | Transaction not found |
| `TRANSACTION_ERROR` | 500 | Error processing transaction |
| `TOPUP_NOT_FOUND` | 404 | Topup request not found |
| `INVALID_TOPUP_STATUS` | 400 | Cannot upload proof for current status |
| `PROOF_ALREADY_UPLOADED` | 400 | Transfer proof already uploaded |
| `CREATE_TOPUP_FAILED` | 400 | Failed to create topup request |
| `UPLOAD_ERROR` | 500 | Error uploading transfer proof |

---

## API Endpoints

### Authentication Endpoints

#### 1. Register New User

**Endpoint**: `POST /api/auth/register`

**Description**: Register a new user account

**Request Body**:
```json
{
  "username": "string (required, 3-50 chars)",
  "fullName": "string (required, 2-100 chars)",
  "email": "string (required, valid email)",
  "phone": "string (required, 10-15 digits)",
  "password": "string (required, 8-100 chars)",
  "pin": "string (required, 6 digits)",
  "referralCode": "string (optional)"
}
```

**Success Response** (201):
```json
{
  "success": true,
  "message": "Registration successful",
  "user": {
    "id": "guid",
    "username": "string",
    "email": "string",
    "fullName": "string",
    "phone": "string",
    "level": "string",
    "levelId": "integer",
    "balance": "decimal",
    "referralCode": "string",
    "createdAt": "datetime"
  }
}
```

**Error Response** (400):
```json
{
  "message": "Username already exists",
  "errorCode": "DUPLICATE_FIELD"
}
```

---

#### 2. Login

**Endpoint**: `POST /api/auth/login`

**Description**: Authenticate user and receive tokens

**Request Body**:
```json
{
  "username": "string (required)",
  "password": "string (required, 8-100 chars)"
}
```

**Success Response** (200):
```json
{
  "success": true,
  "message": "Login successful",
  "accessToken": "string",
  "refreshToken": "string",
  "expiresIn": 900,
  "user": {
    "id": "guid",
    "username": "string",
    "email": "string",
    "fullName": "string",
    "phone": "string",
    "level": "string",
    "levelId": "integer",
    "balance": "decimal",
    "referralCode": "string",
    "createdAt": "datetime"
  }
}
```

**Error Response** (401):
```json
{
  "message": "Invalid username or password",
  "errorCode": "INVALID_CREDENTIALS"
}
```

---

#### 3. Refresh Token

**Endpoint**: `POST /api/auth/refresh`

**Description**: Get new access token using refresh token

**Request Body**:
```json
{
  "refreshToken": "string (required)"
}
```

**Success Response** (200):
```json
{
  "success": true,
  "message": "Token refreshed successfully",
  "accessToken": "string",
  "refreshToken": "string",
  "expiresIn": 900
}
```

**Error Response** (401):
```json
{
  "message": "Invalid or expired refresh token",
  "errorCode": "INVALID_REFRESH_TOKEN"
}
```

---

#### 4. Verify PIN

**Endpoint**: `POST /api/auth/pin/verify`

**Description**: Verify user PIN for sensitive operations

**Headers**:
- `Authorization: Bearer {access_token}`

**Request Body**:
```json
{
  "pin": "string (required, 6 digits)"
}
```

**Success Response** (200):
```json
{
  "success": true,
  "message": "PIN verified successfully",
  "pinSessionToken": "string",
  "expiresIn": 300
}
```

**Error Response** (401):
```json
{
  "message": "Invalid PIN. 2 attempts remaining",
  "errorCode": "INVALID_PIN"
}
```

**Error Response** (429):
```json
{
  "message": "Too many failed attempts. Account locked for 900 seconds",
  "errorCode": "ACCOUNT_LOCKED"
}
```

---

### Product Endpoints

#### 1. Get Product Categories

**Endpoint**: `GET /api/product/categories`

**Description**: Get all product categories

**Headers**:
- `Authorization: Bearer {access_token}`

**Success Response** (200):
```json
{
  "success": true,
  "data": [
    {
      "id": "integer",
      "name": "string",
      "code": "string",
      "icon": "string (url)"
    }
  ]
}
```

---

#### 2. Get Products

**Endpoint**: `GET /api/product`

**Description**: Get products with user-level pricing

**Headers**:
- `Authorization: Bearer {access_token}`

**Query Parameters**:
- `categoryId` (optional): Filter by category ID
- `operatorParam` (optional): Filter by operator name
- `page` (optional, default: 1): Page number
- `pageSize` (optional, default: 50): Items per page

**Success Response** (200):
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "name": "string",
      "code": "string",
      "categoryName": "string",
      "operator": "string",
      "denomination": "integer",
      "description": "string",
      "price": "decimal",
      "available": "boolean"
    }
  ],
  "totalRecords": "integer",
  "page": "integer",
  "pageSize": "integer"
}
```

---

#### 3. Get Product Price

**Endpoint**: `GET /api/product/{id}/price`

**Description**: Get price for a specific product for user's level

**Headers**:
- `Authorization: Bearer {access_token}`

**Path Parameters**:
- `id` (required): Product GUID

**Success Response** (200):
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "name": "string",
    "code": "string",
    "categoryName": "string",
    "operator": "string",
    "denomination": "integer",
    "description": "string",
    "price": "decimal",
    "levelId": "integer",
    "message": "string"
  }
}
```

---

#### 4. Get Product Suppliers

**Endpoint**: `GET /api/product/{id}/suppliers`

**Description**: Get available suppliers for a product

**Headers**:
- `Authorization: Bearer {access_token}`

**Path Parameters**:
- `id` (required): Product GUID

**Success Response** (200):
```json
{
  "success": true,
  "data": [
    {
      "supplierId": "integer",
      "supplierName": "string",
      "supplierCode": "string",
      "productCode": "string",
      "productName": "string",
      "costPrice": "decimal",
      "sequence": "integer"
    }
  ]
}
```

---

### Transaction Endpoints

#### 1. Create Transaction

**Endpoint**: `POST /api/transaction`

**Description**: Create a new transaction (purchase)

**Headers**:
- `Authorization: Bearer {access_token}`
- `X-Reference-Id`: Unique reference for idempotency (optional)

**Request Body**:
```json
{
  "productId": "guid (required)",
  "destinationNumber": "string (required, 10-15 digits)",
  "pinSessionToken": "string (required, from PIN verify endpoint)"
}
```

**Success Response** (201):
```json
{
  "success": true,
  "message": "Transaction created successfully",
  "referenceId": "string",
  "status": "pending",
  "product": {
    "name": "string",
    "code": "string",
    "operator": "string",
    "denomination": "integer"
  },
  "destination": "string",
  "sellPrice": "decimal",
  "createdAt": "datetime"
}
```

**Error Response** (400):
```json
{
  "message": "Insufficient balance",
  "errorCode": "INSUFFICIENT_BALANCE"
}
```

**Idempotency**: Include `X-Reference-Id` header to ensure the same transaction is not created multiple times.

---

#### 2. Get Transaction Details

**Endpoint**: `GET /api/transaction/{referenceId}`

**Description**: Get details of a specific transaction

**Headers**:
- `Authorization: Bearer {access_token}`

**Path Parameters**:
- `referenceId` (required): Transaction reference ID

**Success Response** (200):
```json
{
  "success": true,
  "data": {
    "referenceId": "string",
    "status": "string",
    "productName": "string",
    "productCode": "string",
    "categoryName": "string",
    "destination": "string",
    "sellPrice": "decimal",
    "createdAt": "datetime"
  }
}
```

---

#### 3. Get Transaction History

**Endpoint**: `GET /api/transaction`

**Description**: Get user's transaction history

**Headers**:
- `Authorization: Bearer {access_token}`

**Query Parameters**:
- `status` (optional): Filter by status (pending, success, failed)
- `page` (optional, default: 1): Page number
- `pageSize` (optional, default: 20): Items per page

**Success Response** (200):
```json
{
  "success": true,
  "data": [
    {
      "referenceId": "string",
      "status": "string",
      "productName": "string",
      "productCode": "string",
      "categoryName": "string",
      "destination": "string",
      "sellPrice": "decimal",
      "createdAt": "datetime"
    }
  ],
  "totalRecords": "integer",
  "page": "integer",
  "pageSize": "integer"
}
```

---

### Balance Endpoints

#### 1. Get Balance

**Endpoint**: `GET /api/balance`

**Description**: Get user's current balance

**Headers**:
- `Authorization: Bearer {access_token}`

**Success Response** (200):
```json
{
  "success": true,
  "activeBalance": "decimal",
  "heldBalance": "decimal",
  "totalBalance": "decimal"
}
```

**Balance Types**:
- `activeBalance`: Available balance for transactions
- `heldBalance`: Balance held for pending transactions
- `totalBalance`: Sum of active and held balance

---

#### 2. Get Balance History

**Endpoint**: `GET /api/balance/history`

**Description**: Get user's balance transaction history

**Headers**:
- `Authorization: Bearer {access_token}`

**Query Parameters**:
- `page` (optional, default: 1): Page number
- `pageSize` (optional, default: 20): Items per page

**Success Response** (200):
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "createdAt": "datetime",
      "type": "string",
      "amount": "decimal",
      "activeBefore": "decimal",
      "activeAfter": "decimal",
      "heldBefore": "decimal",
      "heldAfter": "decimal",
      "description": "string"
    }
  ],
  "totalRecords": "integer",
  "page": "integer",
  "pageSize": "integer"
}
```

**Transaction Types**:
- `Topup`: Balance added from top-up
- `PurchaseHold`: Balance held for purchase
- `PurchaseDebit`: Balance deducted for purchase
- `PurchaseRelease`: Held balance released
- `TransferOut`: Balance sent to another user
- `TransferIn`: Balance received from another user
- `Refund`: Balance refunded
- `Adjustment`: Manual adjustment by admin
- `ReferralBonus`: Bonus from referral program

---

### Top-up Endpoints

**New 2-Step Topup Flow:**

1. **Request Topup** → Get payment details with unique code
2. **Upload Proof** → Submit transfer proof for admin verification

---

#### 1. Get Bank Accounts

**Endpoint**: `GET /api/topup/banks`

**Description**: Get list of available bank accounts for top-up

**Headers**:
- `Authorization: Bearer {access_token}`

**Success Response** (200):
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "bankName": "BCA",
      "accountNumber": "1234567890",
      "accountName": "PT PedagangPulsa",
      "isActive": true
    }
  ]
}
```

---

#### 2. Request Topup (Step 1)

**Endpoint**: `POST /api/topup/request`

**Description**: Create a topup request and get payment details with unique code

**Headers**:
- `Authorization: Bearer {access_token}`
- `Content-Type: application/json`

**Request Body**:
```json
{
  "bankAccountId": 1,
  "amount": 100000
}
```

**Success Response** (201):
```json
{
  "success": true,
  "message": "Topup request created. Please transfer the specified amount.",
  "topupId": "guid",
  "status": "pending",
  "payment": {
    "bankName": "BCA",
    "accountNumber": "1234567890",
    "accountName": "PT PedagangPulsa",
    "originalAmount": 100000,
    "uniqueCode": 123,
    "totalAmount": 100123,
    "expiresAt": "2026-04-04T10:30:00Z"
  },
  "createdAt": "2026-04-03T10:30:00Z"
}
```

**About Unique Code:**
- Range: 1-999 (3 digits)
- Purpose: Verify transfer amount matches request
- Generated randomly from unused codes today
- Total Amount = Original Amount + Unique Code
- Request expires in 24 hours

---

#### 3. Upload Transfer Proof (Step 2)

**Endpoint**: `POST /api/topup/{id}/proof`

**Description**: Upload transfer proof for the created topup request

**Headers**:
- `Authorization: Bearer {access_token}`
- `Content-Type: multipart/form-data`

**Request Body**:
```
transferProof: file (required, JPG/PNG/PDF, max 5MB)
```

**Success Response** (200):
```json
{
  "success": true,
  "message": "Transfer proof uploaded successfully. Waiting for admin approval.",
  "topupId": "guid",
  "status": "pending",
  "uploadedAt": "2026-04-03T11:00:00Z"
}
```

**Error Response** (400):
```json
{
  "message": "Transfer proof already uploaded",
  "errorCode": "PROOF_ALREADY_UPLOADED"
}
```

---

#### 4. Get Topup Detail

**Endpoint**: `GET /api/topup/{id}`

**Description**: Get details of a specific topup request

**Headers**:
- `Authorization: Bearer {access_token}`

**Success Response** (200):
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "amount": 100000,
    "uniqueCode": 123,
    "totalAmount": 100123,
    "status": "pending",
    "transferProofUrl": "string",
    "bankName": "BCA",
    "accountNumber": "1234567890",
    "accountName": "PT PedagangPulsa",
    "rejectReason": "string",
    "createdAt": "datetime",
    "updatedAt": "datetime"
  }
}
```

---

#### 5. Get Top-up History

**Endpoint**: `GET /api/topup/history`

**Description**: Get user's top-up request history

**Headers**:
- `Authorization: Bearer {access_token}`

**Query Parameters**:
- `page` (optional, default: 1): Page number
- `pageSize` (optional, default: 20): Items per page

**Success Response** (200):
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "amount": 100000,
      "uniqueCode": 123,
      "totalAmount": 100123,
      "status": "pending",
      "transferProofUrl": "string",
      "bankName": "string",
      "bankAccountNumber": "string",
      "rejectReason": "string",
      "createdAt": "datetime",
      "updatedAt": "datetime"
    }
  ],
  "totalRecords": "integer",
  "page": "integer",
  "pageSize": "integer"
}
```

**Top-up Statuses**:
- `pending`: Waiting for transfer proof or admin approval
- `approved`: Top-up approved and balance added
- `rejected`: Top-up rejected

---

### Transfer Endpoints

#### 1. Transfer Balance

**Endpoint**: `POST /api/transfer`

**Description**: Transfer balance to another user

**Headers**:
- `Authorization: Bearer {access_token}`

**Request Body**:
```json
{
  "toUsername": "string (required)",
  "amount": "decimal (required, min: 1000)",
  "notes": "string (optional)"
}
```

**Success Response** (200):
```json
{
  "success": true,
  "message": "Transfer successful",
  "data": {
    "transferId": "guid",
    "from": "string",
    "to": "string",
    "amount": "decimal",
    "notes": "string",
    "createdAt": "datetime"
  }
}
```

**Error Response** (403):
```json
{
  "message": "User level does not allow transfers",
  "errorCode": "FORBIDDEN"
}
```

**Note**: Transfer capability depends on user level configuration.

---

#### 2. Get Transfer History

**Endpoint**: `GET /api/transfer/history`

**Description**: Get user's transfer history

**Headers**:
- `Authorization: Bearer {access_token}`

**Query Parameters**:
- `page` (optional, default: 1): Page number
- `pageSize` (optional, default: 20): Items per page

**Success Response** (200):
```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "from": "string",
      "to": "string",
      "amount": "decimal",
      "notes": "string",
      "status": "Success",
      "direction": "out",
      "createdAt": "datetime"
    }
  ],
  "totalRecords": "integer",
  "page": "integer",
  "pageSize": "integer"
}
```

**Direction Values**:
- `out`: Sent transfer
- `in`: Received transfer

---

## Rate Limiting

The API implements rate limiting to prevent abuse:

- **Default**: 100 requests per minute per IP
- **Authenticated**: 1000 requests per minute per user
- **Login/Register**: 10 requests per minute per IP

Rate limit headers are included in responses:
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1617234567
```

---

## Webhooks

### Transaction Status Update

When a transaction status changes, a webhook will be sent to the configured URL.

**Webhook Payload**:
```json
{
  "event": "transaction.status_updated",
  "data": {
    "referenceId": "string",
    "status": "string",
    "productId": "guid",
    "destination": "string",
    "sellPrice": "decimal",
    "updatedAt": "datetime"
  }
}
```

**Status Values**:
- `pending`: Transaction initiated
- `processing`: Being processed by supplier
- `success`: Transaction completed successfully
- `failed`: Transaction failed

---

## Integration Examples

### Example 1: Complete Purchase Flow

```javascript
// 1. Login
const loginResponse = await fetch('https://api.pedagangpulsa.com/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    username: 'user123',
    password: 'your-password'
  })
});
const { accessToken } = await loginResponse.json();

// 2. Get products
const productsResponse = await fetch('https://api.pedagangpulsa.com/api/product?categoryId=1', {
  headers: { 'Authorization': `Bearer ${accessToken}` }
});
const products = await productsResponse.json();

// 3. Verify PIN before purchase
const pinResponse = await fetch('https://api.pedagangpulsa.com/api/auth/pin/verify', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${accessToken}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({ pin: '123456' })
});
const { pinSessionToken } = await pinResponse.json();

// 4. Create transaction
const transactionResponse = await fetch('https://api.pedagangpulsa.com/api/transaction', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${accessToken}`,
    'Content-Type': 'application/json',
    'X-Reference-Id': 'unique-uuid-v4'
  },
  body: JSON.stringify({
    productId: products.data[0].id,
    destinationNumber: '08123456789',
    pinSessionToken: pinSessionToken
  })
});
const transaction = await transactionResponse.json();
```

### Example 2: Token Refresh

```javascript
// Check if token is expired
const isTokenExpired = (token) => {
  const payload = JSON.parse(atob(token.split('.')[1]));
  return Date.now() >= payload.exp * 1000;
};

// Refresh token
const refreshToken = async (refreshToken) => {
  const response = await fetch('https://api.pedangpulsa.com/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken })
  });
  const { accessToken, refreshToken: newRefreshToken } = await response.json();
  // Store new tokens
  localStorage.setItem('accessToken', accessToken);
  localStorage.setItem('refreshToken', newRefreshToken);
};
```

---

## Testing

### Test Credentials

**Staging Environment**:
- URL: `https://staging-api.pedagangpulsa.com/api`
- Test User: `testuser`
- Test Password: `your-password`
- Test PIN: `123456`

### Postman Collection

A Postman collection is available at:
`https://docs.pedagangpulsa.com/postman-collection.json`

---

## Support

For API integration support:
- Email: `api-support@pedagangpulsa.com`
- Documentation: `https://docs.pedagangpulsa.com`
- Status Page: `https://status.pedagangpulsa.com`

---

## Changelog

### v1.1.0 (2026-04-03)
- **BREAKING**: Topup flow changed to 2-step process
  - Step 1: `POST /api/topup/request` - Get payment details with unique code
  - Step 2: `POST /api/topup/{id}/proof` - Upload transfer proof
  - Unique code (1-999) generated for verification
  - Request expires in 24 hours

### v1.0.0 (2026-04-03)
- Initial API release
- Authentication endpoints
- Product catalog endpoints
- Transaction management
- Balance operations
- Top-up requests
- Peer-to-peer transfers
- Added Scalar-based interactive API reference for local development
- Login now uses username and password, while PIN remains for sensitive operations

---

**Note**: This API is in active development. Additional endpoints and features will be added in future versions.
