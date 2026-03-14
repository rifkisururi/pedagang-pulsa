# SPRINT 3 - Supplier & Transaction Core
**Timeline:** Week 5-6
**Story Points:** 34
**Goal:** Sistem bisa proses transaksi dengan routing supplier otomatis

---

## STATUS: ✅ COMPLETED

---

## TASK BREAKDOWN

### S3-1: CRUD Supplier (3 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [x] Admin bisa tambah, edit supplier
- [x] Konfigurasi API URL, key, timeout
- [x] Status aktif/non-aktif

**Subtasks:**
- [x] Create `SupplierController` in `Web/Areas/Admin/Controllers/`
- [x] Create `SupplierService` in `Application/Services/`:
  - [x] `GetSuppliersAsync()`
  - [x] `CreateSupplierAsync(...)`
  - [x] `UpdateSupplierAsync(...)`
  - [x] `DeleteSupplierAsync(...)`
- [x] Create view models:
  - [x] `SupplierListViewModel`
  - [x] `SupplierViewModel` (create/edit)
  - [x] `SupplierDetailViewModel`
  - [x] `SupplierDeleteViewModel`
- [x] Create Supplier list view:
  - [x] Table: ID, Name, API URL, Timeout, Status, Balance, Actions
  - [x] "Add Supplier" button
- [x] Create Add/Edit Supplier view:
  - [x] Form fields: Name, API URL, API Key, API Secret, Timeout, Balance Thresholds
  - [x] Validation: required fields, URL format
- [ ] Implement API key encryption (deferred to future sprint)

**Supplier Form Fields:**
| Field | Type | Required | Validation |
|---|---|---|---|
| Name | Text | Yes | - |
| API URL | URL | Yes | Valid URL |
| API Key | Password | Yes | - |
| API Secret | Password | No | - |
| Timeout | Number | Yes | Min 10, Max 120 |
| Is Active | Checkbox | No | Default true |

---

### S3-2: Mapping Supplier ke Produk (5 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [x] Admin bisa mapping produk ke supplier
- [x] Set cost_price per supplier
- [x] Set urutan seq (1, 2, 3...)

**Subtasks:**
- [x] Create `SupplierProductController` in `Web/Areas/Admin/Controllers/`
- [x] Create `SupplierProductService` in `Application/Services/`:
  - [x] `GetSupplierProductsAsync(productId)`
  - [x] `AddSupplierProductAsync(...)`
  - [x] `UpdateSupplierProductAsync(...)`
  - [x] `DeleteSupplierProductAsync(...)`
  - [x] `ReorderSupplierProductsAsync(...)`
- [x] Create view models:
  - [x] `SupplierProductListViewModel`
  - [x] `SupplierProductViewModel`
  - [x] `SupplierProductDeleteViewModel`
- [x] Create Supplier Product mapping view:
  - [x] Product info header
  - [x] Table: Supplier | Cost Price | Supplier SKU | Seq | Status | Actions
  - [x] "Add Supplier" button
- [x] Create Add Supplier mapping view:
  - [x] Form fields: Supplier, Cost Price, Supplier SKU, Sequence, Is Active
  - [x] Validation: unique product+supplier combination
- [x] Implement reorder:
  - [x] Up/Down buttons with AJAX save

**Supplier Product Table:**
| Supplier | Cost Price | Supplier SKU | Seq | Status | Action |
|---|---|---|---|---|---|
| Digiflazz | Rp 5.200 | TSEL5 | 1 | Active | Edit |

---

### S3-3: Drag-and-Drop Seq Routing (5 SP)
**Status:** ✅ DONE (Manual Reorder)
**Acceptance Criteria:**
- [x] Ubah urutan seq dengan up/down buttons
- [x] Auto renumber seq after reorder

**Implementation:**
- [x] Up/Down buttons for each supplier row
- [x] AJAX save on reorder
- [x] Auto renumber after save
- Note: Drag-drop deferred to v1.1, manual reorder implemented

---

### S3-4: Supplier Adapter Pattern (8 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [x] Interface `ISupplierAdapter` dibuat
- [x] Implementasi 2 supplier real (Digiflazz, VIPReseller)
- [x] Strategy Pattern untuk multiple supplier

**Subtasks:**
- [x] Create `ISupplierAdapter` interface in `Infrastructure/Suppliers/`
- [x] Create DTOs in `Infrastructure/Suppliers/DTOs/`:
  - [x] `PurchaseRequest`
  - [x] `SupplierPurchaseResult`
  - [x] `SupplierBalanceResult`
  - [x] `SupplierPingResult`
- [x] Create base class `SupplierAdapterBase`:
  - [x] Common HTTP client logic
  - [x] Error handling
  - [x] Logging
- [x] Implement Digiflazz:
  - [x] `DigiflazzAdapter : ISupplierAdapter`
  - [x] Implement authentication (signature)
  - [x] Implement purchase endpoint
  - [x] Implement balance check
  - [x] Map error codes
- [x] Implement VIPReseller:
  - [x] `VIPResellerAdapter : ISupplierAdapter`
  - [x] Implement authentication
  - [x] Implement purchase endpoint
  - [x] Implement balance check
- [x] Create `SupplierAdapterFactory`:
  - [x] Factory to create adapter based on supplier type
  - [x] `CreateAdapter(string supplierCode, ILoggerFactory)`

**Supplier Adapter Structure:**
```
Infrastructure/Suppliers/
├── ISupplierAdapter.cs
├── SupplierAdapterBase.cs
├── SupplierAdapterFactory.cs
├── DTOs/
│   ├── SupplierPurchaseRequest.cs
│   └── SupplierPurchaseResult.cs
├── Digiflazz/
│   └── DigiflazzAdapter.cs
└── VIPReseller/
    └── VIPResellerAdapter.cs
```

---

### S3-5: Transaction Orchestrator (8 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [x] Service untuk: hold balance → order supplier → retry → debit/release
- [x] Background job processing (Note: Hangfire setup configured, job integration ready)

**Subtasks:**
- [x] Create stored functions:
  - [x] `HoldBalanceAsync` - Hold balance from active to held
  - [x] `DebitHeldBalanceAsync` - Debit held balance after success
  - [x] `ReleaseHeldBalanceAsync` - Release held balance on failure
- [x] Create `TransactionService` in `Application/Services/`:
  - [x] `CreateTransactionAsync(...)` - Main entry point
  - [x] `ProcessTransactionAsync(transactionId)` - Background job method
  - [x] `HoldBalanceAsync(...)`
  - [x] `DebitHeldBalanceAsync(...)`
  - [x] `ReleaseHeldBalanceAsync(...)`
  - [x] `GetTransactionByIdAsync(id)`
  - [x] `GetTransactionsPagedAsync(...)`
- [x] Implement transaction flow:
  1. **Hold Balance**:
     - [x] Check if active_balance >= amount
     - [x] Move amount from active to held
     - [x] Insert to balance_ledger (type='PurchaseHold')
  2. **Create Transaction**:
     - [x] Insert to transactions (status='pending')
     - [x] Generate reference_id
  3. **Process Transaction**:
     - [x] Get supplier routing list (ORDER BY seq ASC)
     - [x] Loop through suppliers:
       - [x] Create transaction_attempt record
       - [x] Call supplier adapter.Purchase()
       - [x] **If Success**:
         - [x] Update attempt status='success'
         - [x] Update transaction status='success'
         - [x] Debit held balance
         - [x] Break loop
       - [x] **If Failed**:
         - [x] Update attempt status='failed'
         - [x] Continue to next supplier
     - [x] **If all suppliers failed**:
       - [x] Update transaction status='failed'
       - [x] Release held balance
- [x] Hangfire configured in Program.cs (ready for job integration)

**Transaction Flow Diagram:**
```
User Request → Hold Balance → Create Transaction (pending)
                                            ↓
                           Background Job (Ready for Hangfire)
                                            ↓
              ┌─────────────────────────────────┐
              │  Loop Supplier by Seq (1,2,3...) │
              └─────────────────────────────────┘
                        ↓
              Success? ──Yes──→ Debit Balance → Done
                        ↓
                       No
                        ↓
              Next Supplier Available?
                        ↓
           Yes ──→ Try Next Supplier
           No ──→ Release Balance → Failed
```

---

### S3-6: Daftar Transaksi + Detail (5 SP)
**Status:** ✅ DONE
**Acceptance Criteria:**
- [x] Tabel transaksi dengan filter
- [x] Detail transaksi dengan timeline attempt
- [x] Tab Transaksi user terisi

**Subtasks:**
- [x] Create `TransactionController` in `Web/Areas/Admin/Controllers/`
- [x] Create `TransactionService` methods:
  - [x] `GetTransactionsPagedAsync(...)`
  - [x] `GetTransactionByIdAsync(id)`
- [x] Create view models:
  - [x] `TransactionListViewModel`
  - [x] `TransactionDetailViewModel`
- [x] Create Transaction list view:
  - [x] DataTable with columns: Reference ID, Date/Time, Username, Product, Destination, Sell Price, Status, Supplier, Actions
  - [x] Filters: date range, status
  - [x] Search by destination, SN
- [x] Create Transaction detail view:
  - [x] Transaction info card
  - [x] Timeline attempt:
    - [x] Vertical timeline
    - [x] Each attempt shows: Supplier name, Seq number, Status, Timestamp, Error message, Supplier transaction ID
- [x] Implement fill transaction tab in user detail:
  - [x] Show user's transactions
  - [x] Link to transaction detail

**Timeline Attempt UI:**
```
┌─────────────────────────────────────┐
│ Transaction Timeline               │
├─────────────────────────────────────┤
│                                     │
│  ✅ Digiflazz (Seq 1)              │
│     Success - 12 Mar 10:35:22     │
│     SN: 1234567890                 │
│     Supplier Trx ID: DGX123        │
│                                     │
│  ❌ VIPReseller (Seq 2)            │
│     Failed: Timeout - 10:35:10    │
│     Error: Connection timeout      │
│                                     │
└─────────────────────────────────────┘
```

---

## TECHNICAL NOTES

- **Supplier adapter:** Strategy Pattern dengan interface `ISupplierAdapter`
- Implementasi stored functions: `HoldBalanceAsync`, `DebitHeldBalanceAsync`, `ReleaseHeldBalanceAsync`
- Background job menggunakan Hangfire (configured, ready for job enqueue)
- Timeout transaksi: configurable per supplier

---

## DELIVERABLES

- [x] Admin bisa mapping produk ke supplier dengan urutan seq
- [x] Transaksi bisa dibuat manual via service layer
- [x] Sistem otomatis routing ke supplier seq=1, retry ke seq=2 jika gagal
- [x] Detail transaksi menampilkan timeline attempt per supplier
- [x] Balance operations working (hold, debit, release)

---

## DEFINITION OF DONE

- [x] Supplier CRUD functional
- [x] Product-supplier mapping working
- [x] 2 real supplier adapters implemented (Digiflazz, VIPReseller)
- [x] Transaction orchestrator working end-to-end
- [x] Hold → Debit/Release flow working
- [x] Background jobs ready for Hangfire integration
- [x] Transaction list and detail views working
- [x] Timeline attempts displayed correctly
- [x] User transaction tab populated
- [x] No critical bugs

---

## IMPLEMENTATION NOTES

### Files Created:

**Application Layer:**
- `SupplierService.cs` - Supplier CRUD operations
- `SupplierProductService.cs` - Product-supplier mapping
- `TransactionService.cs` - Transaction orchestrator with balance operations

**Infrastructure Layer:**
- `Suppliers/ISupplierAdapter.cs` - Supplier adapter interface
- `Suppliers/SupplierAdapterBase.cs` - Base adapter with common functionality
- `Suppliers/SupplierAdapterFactory.cs` - Factory for creating adapters
- `Suppliers/DTOs/SupplierPurchaseRequest.cs` - Purchase request DTO
- `Suppliers/DTOs/SupplierPurchaseResult.cs` - Purchase result DTOs
- `Suppliers/Digiflazz/DigiflazzAdapter.cs` - Digiflazz implementation
- `Suppliers/VIPReseller/VIPResellerAdapter.cs` - VIPReseller implementation

**Web Layer (Controllers):**
- `Areas/Admin/Controllers/SupplierController.cs` - Supplier management
- `Areas/Admin/Controllers/SupplierProductController.cs` - Product-supplier mapping
- `Areas/Admin/Controllers/TransactionController.cs` - Transaction list/detail

**Web Layer (ViewModels):**
**Supplier:**
- `SupplierListViewModel.cs`
- `SupplierViewModel.cs`
- `SupplierDetailViewModel.cs`
- `SupplierDeleteViewModel.cs`

**SupplierProduct:**
- `SupplierProductListViewModel.cs`
- `SupplierProductViewModel.cs`
- `SupplierProductDeleteViewModel.cs`
- `SupplierProductReorderViewModel.cs`

**Transaction:**
- `TransactionListViewModel.cs`
- `TransactionDetailViewModel.cs`

**Web Layer (Views):**
**Supplier:**
- `Areas/Admin/Views/Supplier/Index.cshtml` - Supplier list with DataTables
- `Areas/Admin/Views/Supplier/Details.cshtml` - Supplier detail
- `Areas/Admin/Views/Supplier/Create.cshtml` - Create supplier
- `Areas/Admin/Views/Supplier/Edit.cshtml` - Edit supplier
- `Areas/Admin/Views/Supplier/Delete.cshtml` - Delete supplier

**SupplierProduct:**
- `Areas/Admin/Views/SupplierProduct/Index.cshtml` - Supplier mappings with reorder
- `Areas/Admin/Views/SupplierProduct/Add.cshtml` - Add supplier mapping
- `Areas/Admin/Views/SupplierProduct/Edit.cshtml` - Edit supplier mapping
- `Areas/Admin/Views/SupplierProduct/Delete.cshtml` - Delete supplier mapping

**Transaction:**
- `Areas/Admin/Views/Transaction/Index.cshtml` - Transaction list with DataTables
- `Areas/Admin/Views/Transaction/Details.cshtml` - Transaction detail with timeline

**Updated:**
- `Areas/Admin/Views/User/Details.cshtml` - Now populates Transactions tab
- `Areas/Admin/Views/Product/Details.cshtml` - Added Suppliers link

---

**NEXT SPRINT:** Sprint 4 - Topup, Finance, Reports
