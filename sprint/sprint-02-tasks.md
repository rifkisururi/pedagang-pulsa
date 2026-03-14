# SPRINT 2 - User & Product Management
**Timeline:** Week 3-4
**Story Points:** 26
**Goal:** Admin bisa mengelola user dan produk dengan harga per level

---

## STATUS: ✅ COMPLETED

---

## TASK BREAKDOWN

### S2-1: Daftar User + Filter (5 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [x] Tabel user server-side processing
- [x] Filter by level, status, tanggal register
- [x] Pagination, search, sorting

**Subtasks:**
- [x] Create `UserController` in `Web/Areas/Admin/Controllers/`
- [x] Create `UserService` in `Application/Services/`:
  - [x] `GetUsersPagedAsync(page, pageSize, filter)`
  - [x] `GetUserCountByFilterAsync(filter)`
- [x] Create `UserListViewModel`:
  - [x] Pagination info
  - [x] Filter parameters
  - [x] User list data
- [x] Setup DataTables.net:
  - [x] Add DataTables CSS/JS
  - [x] Configure server-side processing
  - [x] Add AJAX endpoint for data loading
- [x] Create User list view:
  - [x] DataTable with columns: ID, Username, Full Name, Email, Phone, Level, Status, Saldo, Registered Date, Actions
  - [x] Filter dropdown: Level, Status
  - [x] Date range picker: Register date
  - [x] Search box
  - [x] Pagination
- [x] Implement server-side endpoint:
  - [x] `GET /Admin/User/GetData` - Returns JSON for DataTables
  - [x] Query with filters: level, status, date range
  - [x] Apply pagination, sorting
- [x] Add action buttons:
  - [x] View detail
  - [x] Edit level (if authorized)
  - [x] Suspend (if authorized)

**Columns:**
| Column | Description |
|---|---|
| ID | User ID (UUID, truncated) |
| Username | Username (clickable to detail) |
| Full Name | Full name |
| Email | Email address |
| Phone | Phone number |
| Level | User level badge |
| Status | Active/Suspended badge |
| Saldo | Current balance (formatted) |
| Registered | Date registered |
| Actions | View, Edit, Suspend buttons |

---

### S2-2: Detail User + Tabs (5 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [x] Tab: Profil
- [x] Tab: Saldo
- [x] Tab: Transaksi (placeholder for sprint ini)
- [x] Tab: Referral (placeholder for sprint ini)

**Subtasks:**
- [x] Create `UserDetailViewModel`:
  - [x] User profile data
  - [x] Balance data
  - [x] Transaction summary (placeholder)
  - [x] Referral data (placeholder)
- [x] Create User detail view:
  - [x] User info card: Username, Full Name, Email, Phone, Level, Status, Registered Date, Last Login
  - [x] Tab navigation
- [x] Implement Profile tab:
  - [x] Display all user fields
  - [x] Level badge
  - [x] Status badge
  - [x] Dates formatted
- [x] Implement Saldo tab:
  - [x] Active balance
  - [x] Held balance
  - [x] Total balance
  - [x] Mini table: recent 10 balance mutations (from `balance_ledger`)
- [x] Implement Transaksi tab (placeholder):
  - [x] Message: "Transaction history will be available in Sprint 3"
- [x] Implement Referral tab (placeholder):
  - [x] Message: "Referral information will be available in Sprint 4"
- [x] Add action buttons (top right):
  - [x] Edit Level (admin/superadmin only)
  - [x] Suspend/Unsuspend (superadmin only)
  - [x] Back to list

**User Info Display:**
```
Username: user123
Full Name: John Doe
Email: john@example.com
Phone: 08123456789
Level: Member 1 (badge)
Status: Active (green badge)
Registered: 12 Mar 2026
Last Login: (from entity if available)
```

---

### S2-3: Edit Level & Suspend User (3 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [x] Admin bisa ubah level user
- [x] Superadmin bisa suspend user
- [x] Confirmation dialog before action

**Subtasks:**
- [x] Create `EditLevelUserViewModel`:
  - [x] User ID
  - [x] Current level
  - [x] New level dropdown
- [x] Create edit level modal/view:
  - [x] User info display
  - [x] Level dropdown (all levels)
  - [x] Confirm/Cancel buttons
- [x] Implement edit level logic:
  - [x] `POST /Admin/User/EditLevel/{id}`
  - [x] Update user level in database
  - [x] Show success/error message
- [x] Create suspend user modal/view:
  - [x] User info display
  - [x] Suspend reason (required)
  - [x] Confirm/Cancel buttons
- [x] Implement suspend logic:
  - [x] `POST /Admin/User/Suspend/{id}`
  - [x] Update user status to Suspended
  - [x] Show success/error message
- [x] Implement unsuspend logic:
  - [x] `POST /Admin/User/Suspend/{id}` (same endpoint, toggle based on status)
  - [x] Update user status to Active
- [x] Add authorization checks:
  - [x] Edit level: Admin, Superadmin
  - [x] Suspend: Superadmin only

**Authorization:**
```csharp
[Authorize(Roles = "SuperAdmin,Admin")]
public async Task<IActionResult> EditLevel(Guid id)

[Authorize(Roles = "SuperAdmin")]
public async Task<IActionResult> Suspend(Guid id)
```

---

### S2-4: CRUD Produk (5 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [x] Tambah produk baru
- [x] Edit produk
- [x] Hapus produk
- [x] List produk dengan filter kategori

**Subtasks:**
- [x] Create `ProductController` in `Web/Areas/Admin/Controllers/`
- [x] Create `ProductService` in `Application/Services/`:
  - [x] `GetProductsPagedAsync(...)`
  - [x] `GetProductByIdAsync(id)`
  - [x] `CreateProductAsync(...)`
  - [x] `UpdateProductAsync(...)`
  - [x] `DeleteProductAsync(...)`
- [x] Create Product view models:
  - [x] `ProductListViewModel`
  - [x] `ProductViewModel` (create/edit)
  - [x] `ProductDetailViewModel`
  - [x] `ProductDeleteViewModel`
- [x] Create Product list view:
  - [x] DataTable with columns: Code, Name, Category, Price, Cost Price, Status, Actions
  - [x] Filter by category, status
  - [x] Search by code/name/sku
  - [x] "Add Product" button
- [x] Create Add Product view:
  - [x] Form fields:
    - [x] Code (required)
    - [x] SKU
    - [x] Name (required)
    - [x] Category (dropdown)
    - [x] Price (required)
    - [x] Cost Price (required)
    - [x] Description (optional)
    - [x] Sort Order
    - [x] Is Available checkbox
    - [x] Is Active checkbox
    - [x] Level prices table
  - [x] Validation: required fields
  - [x] Submit/Cancel buttons
- [x] Create Edit Product view:
  - [x] Same fields as Add Product
  - [x] Pre-populated with existing data
  - [x] Update/Cancel buttons
- [x] Implement delete confirmation modal:
  - [x] Warning message
  - [x] Confirm/Cancel buttons

**Product Form Fields:**
| Field | Type | Required | Validation |
|---|---|---|---|
| Code | Text | Yes | - |
| SKU | Text | No | - |
| Name | Text | Yes | - |
| Category | Dropdown | Yes | - |
| Price | Number | Yes | >= 0 |
| Cost Price | Number | Yes | >= 0 |
| Description | Textarea | No | - |
| Sort Order | Number | No | - |
| Is Available | Checkbox | No | Default true |
| Is Active | Checkbox | No | Default true |

---

### S2-5: Kelola Harga per Level (5 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [x] Inline edit table harga untuk semua level
- [x] Validasi: warning jika harga jual < cost price (visual indicator with red text)
- [x] Simpan harga per level

**Subtasks:**
- [x] Price management integrated into Product Detail view
- [x] Create view model:
  - [x] `ProductDetailViewModel` - product info + prices per level
  - [x] List of all user levels with margin calculation
- [x] Create Product Detail view with price table:
  - [x] Product info header
  - [x] Table: Level | Sell Price | Margin | Margin % |
  - [x] Inline edit for sell price
- [x] Implement inline edit:
  - [x] Click price → input field appears
  - [x] Enter new price
  - [x] Auto-save on blur/enter
- [x] Implement validation:
  - [x] Visual indicator (red) if margin < 0
  - [x] Error if sell_price <= 0
- [x] Implement save logic:
  - [x] `POST /Admin/Product/UpdatePrice` - AJAX endpoint
  - [x] Upsert to `product_level_prices`
  - [x] Calculate margin: sell_price - cost_price
- [x] Create AJAX endpoint:
  - [x] `POST /Admin/Product/UpdatePrice` - Save price per level
- [x] Add cost price column:
  - [x] Display base price and cost price
  - [x] Show margin for each level

**Price Table Structure:**
| Level | Sell Price | Margin | Margin % |
|---|---|---|---|
| Member 1 | Rp 5.500 | Rp 300 | 5.77% |
| Member 2 | Rp 5.400 | Rp 200 | 3.85% |
| Member 3 | Rp 5.300 | Rp 100 | 1.92% |

**Warning Logic:**
```javascript
// Margin displayed in red if negative
const margin = sellPrice - costPrice;
marginClass = margin < 0 ? "text-danger" : "text-success";
```

---

### S2-6: CRUD Level User (3 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [x] Tambah level user baru
- [x] Edit level (nama, config)
- [x] Hapus level (jika tidak ada user)
- [x] Config stored as columns in user_levels table

**Subtasks:**
- [x] Create `UserLevelController` in `Web/Areas/Admin/Controllers/`
- [x] Create `UserLevelService` in `Application/Services/`:
  - [x] `GetLevelsPagedAsync(...)`
  - [x] `GetLevelByIdAsync(...)`
  - [x] `CreateLevelAsync(...)`
  - [x] `UpdateLevelAsync(...)`
  - [x] `DeleteLevelAsync(...)`
- [x] Create view models:
  - [x] `UserLevelListViewModel`
  - [x] `UserLevelViewModel` (create/edit)
  - [x] `UserLevelDetailViewModel`
  - [x] `UserLevelDeleteViewModel`
- [x] Create User Level list view:
  - [x] DataTable: Level, Markup Type, Markup Value, Min Deposit, Status, Actions
  - [x] "Add Level" button
- [x] Create Add Level view:
  - [x] Form fields:
    - [x] Name
    - [x] Markup Type (Percentage/Fixed)
    - [x] Markup Value
    - [x] Min Deposit
    - [x] Description (optional)
    - [x] Is Active checkbox
  - [x] Validation: required fields
- [x] Create Edit Level view:
  - [x] Same fields as Add
  - [x] Pre-populated
  - [x] Update/Cancel buttons
- [x] Implement delete validation:
  - [x] Check if any users have this level
  - [x] Show error if users exist
  - [x] Allow delete only if user_count = 0

**Level Config (stored as columns):**
```
user_levels table columns:
- id
- name
- markup_type (enum: Percentage, Fixed)
- markup_value (decimal)
- min_deposit (decimal)
- description
- is_active
- created_at
- updated_at
```

---

## TECHNICAL NOTES

- DataTables server-side processing untuk tabel user dan produk
- Harga per level disimpan di `product_level_prices` (constraint UNIQUE)
- Tab transaksi/referral user masih placeholder (akan diisi sprint 3-4)
- Validasi: margin ditampilkan dengan warna merah jika negatif

---

## DELIVERABLES

- [x] Admin bisa kelola user: lihat, edit level, suspend
- [x] Admin bisa kelola produk: CRUD + set harga per level
- [x] Validasi: warning jika margin negatif (text merah)
- [x] DataTables server-side processing implemented

---

## DEFINITION OF DONE

- [x] All CRUD operations work
- [x] DataTables with server-side processing functional
- [x] Filters and search work
- [x] Inline price editing functional
- [x] Validation warnings displayed correctly
- [x] Authorization checks in place
- [x] No critical bugs

---

## IMPLEMENTATION NOTES

### Files Created:
**Application Layer:**
- `UserService.cs` - User queries and operations
- `ProductService.cs` - Product CRUD operations
- `UserLevelService.cs` - UserLevel CRUD operations

**Web Layer (Controllers):**
- `Areas/Admin/Controllers/UserController.cs` - User management
- `Areas/Admin/Controllers/ProductController.cs` - Product management
- `Areas/Admin/Controllers/UserLevelController.cs` - UserLevel management

**Web Layer (ViewModels):**
**User:**
- `UserListViewModel.cs`
- `UserDetailViewModel.cs`
- `EditLevelUserViewModel.cs`
- `SuspendUserViewModel.cs`

**Product:**
- `ProductListViewModel.cs`
- `ProductViewModel.cs`
- `ProductDetailViewModel.cs`
- `ProductDeleteViewModel.cs`
- `UpdatePriceViewModel.cs`

**UserLevel:**
- `UserLevelListViewModel.cs`
- `UserLevelViewModel.cs`
- `UserLevelDetailViewModel.cs`
- `UserLevelDeleteViewModel.cs`

**Web Layer (Views):**
**User:**
- `Areas/Admin/Views/User/Index.cshtml` - User list with DataTables
- `Areas/Admin/Views/User/Details.cshtml` - User detail with tabs
- `Areas/Admin/Views/User/EditLevel.cshtml` - Edit user level
- `Areas/Admin/Views/User/Suspend.cshtml` - Suspend/unsuspend user

**Product:**
- `Areas/Admin/Views/Product/Index.cshtml` - Product list with DataTables
- `Areas/Admin/Views/Product/Details.cshtml` - Product detail with inline price editing
- `Areas/Admin/Views/Product/Create.cshtml` - Create product
- `Areas/Admin/Views/Product/Edit.cshtml` - Edit product
- `Areas/Admin/Views/Product/Delete.cshtml` - Delete product

**UserLevel:**
- `Areas/Admin/Views/UserLevel/Index.cshtml` - Level list with DataTables
- `Areas/Admin/Views/UserLevel/Details.cshtml` - Level detail
- `Areas/Admin/Views/UserLevel/Create.cshtml` - Create level
- `Areas/Admin/Views/UserLevel/Edit.cshtml` - Edit level
- `Areas/Admin/Views/UserLevel/Delete.cshtml` - Delete level

---

**NEXT SPRINT:** Sprint 3 - Supplier & Transaction Core
