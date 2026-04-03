# PedagangPulsa API Documentation

## 📚 Available Documentation for Mobile Team

This folder contains complete API documentation for the PedagangPulsa Member Mobile API.

---

## 📄 Documentation Files

### 1. API_DOCUMENTATION.md
**Complete API reference in Markdown format**

- All endpoints with detailed descriptions
- Request/response examples
- Error codes and handling
- Rate limiting information
- Authentication flow
- Best practices

**Best for:** Reading, reference, sharing via GitHub/docs site

---

### 2. Postman_Collection.json
**Postman collection for API testing**

- Pre-configured requests for all endpoints
- Auto-save tokens (access_token, pin_session_token)
- Test scripts for validation
- Environment variables pre-configured
- Sandbox URL ready

**How to use:**
1. Open Postman
2. Click Import
3. Select `Postman_Collection.json`
4. Set environment variables:
   - `username`: testuser
   - `password`: your-password
   - `pin`: 123456
5. Start with "Login" request

**Best for:** Manual testing, integration testing, debugging

---

### 3. openapi.yaml
**OpenAPI 3.0 specification**

- Standard OpenAPI/Swagger format
- Can be imported into:
  - Swagger UI
  - Insomnia
  - REST Client (VS Code)
  - API clients that support OpenAPI
  - Code generators (AutoRest, OpenAPI Generator)

**Best for:** Code generation, documentation sites, API tools

---

## 🚀 Quick Start

### 1. Setup Environment

**Production:**
```
Base URL: https://api.pedagangpulsa.com/api/v1
```

**Sandbox (for testing):**
```
Base URL: https://sandbox-api.pedagangpulsa.com/api/v1
```

**Test Credentials (Sandbox):**
```
Username: testuser
Password: your-password
PIN: 123456
```

---

### 2. Authentication Flow

```
Step 1: POST /auth/login
{
  "username": "testuser",
  "password": "your-password"
}

Response:
{
  "access_token": "eyJhbGc...",
  "refresh_token": "eyJhbGc...",
  "expires_in": 900
}

Step 2: Use access_token for all requests
Authorization: Bearer {access_token}

Step 3: For sensitive operations (transaction, transfer):
POST /auth/pin/verify
{ "pin": "123456" }

Response:
{
  "pin_session_token": "uuid...",
  "expires_in": 300
}
```

---

### 3. Making Requests

**Simple Request (GET balance):**
```http
GET /api/v1/balance
Authorization: Bearer {access_token}
```

**Transaction Request (requires PIN session):**
```http
POST /api/v1/transactions
Authorization: Bearer {access_token}
X-Reference-Id: app-{user_id}-{timestamp}-{random}

{
  "product_id": "uuid-product",
  "destination": "08123456789",
  "pin_session_token": "{pin_session_token}"
}
```

---

## 📋 Endpoints Summary

| Category | Endpoints | Auth Required |
|---|---|---|
| **Auth** | Register, Login, Refresh, Verify PIN | No (except Verify PIN) |
| **Balance** | Get Balance, Get History | Yes |
| **Topup** | Request Topup, Get History | Yes |
| **Products** | List Products | Yes |
| **Transactions** | Create, List, Get Detail | Yes |
| **Transfers** | Transfer Balance | Yes |
| **Notifications** | List, Mark Read, Mark All Read | Yes |

---

## ⚠️ Important Notes

### Idempotency (Transactions & Transfers)

All transaction and transfer requests MUST include:

```http
X-Reference-Id: app-{user_id}-{timestamp}-{random}
```

**Purpose:** Prevents duplicate requests

**Example:** `X-Reference-Id: app-550e8400-1710234567-a1b2c3`

---

### PIN Session Token

For sensitive operations:
1. Call `POST /auth/pin/verify` once
2. Receive `pin_session_token` (valid for 5 minutes)
3. Reuse the token for multiple transactions
4. Don't ask PIN again if token still valid

---

### Rate Limiting

| Endpoint | Limit | Per |
|---|---|---|
| Login | 5 | 1 minute (IP) |
| PIN Verify | 10 | 1 minute (User) |
| Transactions | 10 | 1 minute (User) |
| Transfers | 10 | 1 minute (User) |
| Other GET | 60 | 1 minute (User) |

**Rate Limit Response:**
```json
{
  "error_code": "RATE_LIMIT_EXCEEDED",
  "message": "Too many requests",
  "details": {
    "retry_after": 30
  }
}
```

---

## 🔧 Testing Tools

### Option 1: Postman (Recommended)

1. Import `Postman_Collection.json`
2. Set environment variables
3. Run requests in order:
   - Login → (token saved)
   - Verify PIN → (pin_session_token saved)
   - Get Products → (get product_id)
   - Create Transaction → (use saved tokens)

### Option 2: cURL

```bash
# Login
curl -X POST https://sandbox-api.pedagangpulsa.com/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"your-password"}'

# Get Balance (replace {token})
curl -X GET https://sandbox-api.pedagangpulsa.com/api/v1/balance \
  -H "Authorization: Bearer {token}"
```

### Option 3: Swagger UI

1. Import `openapi.yaml` into Swagger UI
2. Interactive API testing in browser
3. Auto-generated request examples

---

## 📱 Mobile Implementation Tips

### 1. Token Management

```javascript
// Store tokens securely
const storeTokens = (accessToken, refreshToken) => {
  await SecureStore.setItemAsync('access_token', accessToken);
  await SecureStore.setItemAsync('refresh_token', refreshToken);
};

// Auto-refresh before expiry
const refreshIfNeeded = async () => {
  const token = await SecureStore.getItemAsync('access_token');
  const decoded = jwt_decode(token);
  const expiry = decoded.exp * 1000;
  const now = Date.now();

  if (expiry - now < 60000) { // Less than 1 minute
    await refreshToken();
  }
};
```

### 2. Idempotency Key Generation

```javascript
const generateReferenceId = (userId) => {
  const timestamp = Date.now();
  const random = Math.random().toString(36).substring(2, 8);
  return `app-${userId}-${timestamp}-${random}`;
};

// Store locally for retry
const createTransaction = async (productId, destination) => {
  const referenceId = generateReferenceId(userId);
  await AsyncStorage.setItem(`ref_${referenceId}`, JSON.stringify({
    productId, destination, timestamp: Date.now()
  }));

  try {
    const response = await api.post('/transactions', {
      product_id: productId,
      destination,
      pin_session_token: pinSessionToken
    }, {
      headers: { 'X-Reference-Id': referenceId }
    });

    await AsyncStorage.removeItem(`ref_${referenceId}`);
    return response.data;
  } catch (error) {
    // Reference ID stored for retry
    throw error;
  }
};
```

### 3. Error Handling

```javascript
const handleApiError = (error) => {
  const errorCode = error.response?.data?.error_code;

  switch (errorCode) {
    case 'UNAUTHORIZED':
      // Refresh token and retry
      return refreshToken();
    case 'PIN_LOCKED':
      // Show lockout message with timer
      const lockoutEnds = error.response.data.details.lockout_ends_in;
      showLockoutMessage(lockoutEnds);
      break;
    case 'INSUFFICIENT_BALANCE':
      // Show topup prompt
      showTopupPrompt(error.response.data.details.required);
      break;
    case 'RATE_LIMIT_EXCEEDED':
      // Show countdown
      const retryAfter = error.response.data.details.retry_after;
      showRateLimitMessage(retryAfter);
      break;
    default:
      // Show generic error
      showErrorMessage(error.response?.data?.message);
  }
};
```

---

## 🆘 Support & Resources

- **API Documentation:** `API_DOCUMENTATION.md`
- **Postman Collection:** `Postman_Collection.json`
- **OpenAPI Spec:** `openapi.yaml`
- **Email Support:** api@pedagangpulsa.com
- **Status Page:** https://status.pedagangpulsa.com

---

## 📝 Changelog

| Version | Date | Changes |
|---|---|---|
| 1.0.0 | 2026-03-12 | Initial API documentation for mobile team |

---

## 🎯 Next Steps for Mobile Team

1. **Review Documentation:** Read `API_DOCUMENTATION.md`
2. **Import Postman Collection:** Test endpoints manually
3. **Set Up SDK:** Create HTTP client with interceptors
4. **Implement Auth Flow:** Login → Token management → Auto-refresh
5. **Implement Core Features:** Balance → Products → Transactions
6. **Test:** Use sandbox environment extensively
7. **Production Ready:** Switch to production URL after testing

---

**Good luck with your development! 🚀**

For questions or clarifications, don't hesitate to reach out to the backend team.
