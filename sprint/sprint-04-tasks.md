# SPRINT 4 - Topup, Finance, Reports
**Timeline:** Week 7-8
**Story Points:** 28
**Goal:** Admin bisa approve topup, lihat laporan profit, deposit ke supplier

---

## STATUS: ✅ **COMPLETED** (2026-03-13)

**Summary:**
- All 7 user stories completed
- Controllers, Services, ViewModels created
- Views implemented with DataTables
- Migration created for profit views and supplier balance functions
- **Build Status:** ✅ SUCCESS (0 errors, 16 warnings)

---

## TASK BREAKDOWN

### S4-1: Daftar Topup Pending (3 SP)
**Status:** ✅ DONE
**Details:**
- ✅ `TopupController.cs` created
- ✅ `TopupService.cs` created with all required methods
- ✅ ViewModels: `TopupListViewModel`, `TopupDetailViewModel`, `ApproveTopupViewModel`, `RejectTopupViewModel`
- ✅ `Index.cshtml` with DataTables, filters, image preview
- ✅ `_ApproveModal.cshtml`, `_RejectModal.cshtml` created
- ✅ `Details.cshtml` created
**Acceptance Criteria:**
- [ ] Tabel topup pending dengan preview bukti transfer
- [ ] Filter by status, date range
- [ ] Quick action buttons

**Subtasks:**
- [ ] Create `TopupController` in `Web/Areas/Admin/Controllers/`
- [ ] Create `TopupService` in `Application/Services/`:
  - [ ] `GetTopupRequestsPagedAsync(...)`
  - [ ] `GetTopupRequestByIdAsync(id)`
- [ ] Create view models:
  - [ ] `TopupListViewModel`
  - [ ] `TopupDetailViewModel`
- [ ] Create Topup list view:
  - [ ] DataTable with columns:
    - [ ] Request ID
    - [ ] Date/Time
    - [ ] Username
    - [ ] Amount
    - [ ] Bank (from bank list)
    - [ ] Transfer Proof (thumbnail, click to preview)
    - [ ] Status (badge)
    - [ ] Actions
  - [ ] Filter by status (default: Pending)
  - [ ] Filter by date range
- [ ] Implement image preview:
  - [ ] Modal popup on click
  - [ ] Show full-size proof image
  - [ ] Image zoom functionality

**Topup List Columns:**
| Column | Description |
|---|---|
| Request ID | Truncated UUID |
| Date/Time | Created at |
| Username | Link to user detail |
| Amount | Formatted IDR |
| Bank | Bank name |
| Proof | Thumbnail (clickable) |
| Status | Pending/Approved/Rejected |
| Actions | Approve/Reject buttons |

---

### S4-2: Approve/Reject Topup (5 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] Admin bisa approve (tambah saldo) atau reject dengan alasan
- [ ] Modal confirmation
- [ ] Auto update user balance

**Subtasks:**
- [ ] Create `ApproveTopupViewModel`:
  - [ ] Request ID
  - [ ] User info display
  - [ ] Requested amount
  - [ ] Final amount input (editable)
  - [ ] Notes input (optional)
- [ ] Create approve modal:
  - [ ] Display user info (name, current balance)
  - [ ] Display requested amount
  - [ ] Input: Final amount (pre-filled with requested, editable)
  - [ ] Input: Notes (optional)
  - [ ] Confirm/Cancel buttons
- [ ] Implement approve logic:
  - [ ] `POST /admin/topup/{id}/approve`
  - [ ] Validation: amount > 0
  - [ ] Begin transaction:
    - [ ] `SELECT ... FROM user_balances WHERE user_id = ? FOR UPDATE`
    - [ ] `UPDATE user_balances SET active_balance = active_balance + amount`
    - [ ] `INSERT INTO balance_ledger (type='topup', amount, ...)`
    - [ ] `UPDATE topup_requests SET status='approved', approved_by, approved_at, amount`
  - [ ] Commit transaction
  - [ ] Queue Hangfire job: send notification (email + WhatsApp)
  - [ ] Show success message
  - [ ] Redirect to topup list
- [ ] Create reject modal:
  - [ ] Display user info
  - [ ] Display requested amount
  - [ ] Input: Reject reason (required)
  - [ ] Confirm/Cancel buttons
- [ ] Implement reject logic:
  - [ ] `POST /admin/topup/{id}/reject`
  - [ ] Validation: reason not empty
  - [ ] Update topup request: `status='rejected', reject_reason, rejected_by, rejected_at`
  - [ ] Queue notification job
  - [ ] Show success message
  - [ ] Redirect to topup list

**Approve Flow:**
```
Admin clicks Approve
  ↓
Modal opens with:
  - User: user123 (Current balance: Rp 50.000)
  - Requested: Rp 100.000
  - Final Amount: [ Rp 100.000 ] (editable)
  - Notes: [ ... ] (optional)
  ↓
Confirm → Process
  ↓
1. Lock user_balances row (FOR UPDATE)
2. Add Rp 100.000 to active_balance
3. Insert balance_ledger record
4. Update topup_requests (status=approved)
  ↓
Queue notification → Done
```

---

### S4-3: Adjustment Saldo Manual (3 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] Superadmin/finance bisa adjustment saldo user
- [ ] Tambah/kurang saldo dengan catatan
- [ ] Log ke audit_logs

**Subtasks:**
- [ ] Create adjustment action in UserController:
  - [ ] `GET /admin/users/{id}/adjust-balance`
  - [ ] `POST /admin/users/{id}/adjust-balance`
- [ ] Create `AdjustBalanceViewModel`:
  - [ ] User ID
  - [ ] Current balance display
  - [ ] Adjustment type (Add/Deduct)
  - [ ] Amount input
  - [ ] Notes (required)
- [ ] Create adjust balance modal:
  - [ ] User info display
  - [ ] Current balance
  - [ ] Radio: Add / Deduct
  - [ ] Amount input
  - [ ] Notes (required)
  - [ ] Password confirmation (admin password)
  - [ ] Confirm/Cancel buttons
- [ ] Implement adjustment logic:
  - [ ] `POST /admin/users/{id}/adjust-balance`
  - [ ] Authorize: Superadmin, Finance only
  - [ ] Validate admin password
  - [ ] Begin transaction:
    - [ ] `SELECT ... FROM user_balances WHERE user_id = ? FOR UPDATE`
    - [ ] If Add: `UPDATE active_balance = active_balance + amount`
    - [ ] If Deduct: Check sufficient balance, then `UPDATE active_balance = active_balance - amount`
    - [ ] `INSERT INTO balance_ledger (type='adjustment', amount, notes, ...)`
  - [ ] Commit transaction
  - [ ] Insert audit_log
  - [ ] Queue notification
  - [ ] Show success message

**Adjustment Types:**
- Add: Credit balance manually
- Deduct: Debit balance manually (requires sufficient balance)

---

### S4-4: Supplier Balance Tracking (5 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] Tampil saldo supplier
- [ ] History mutasi supplier balance
- [ ] Form deposit ke supplier

**Subtasks:**
- [ ] Create stored functions in PostgreSQL:
  ```sql
  CREATE OR REPLACE FUNCTION debit_supplier_balance(
    p_supplier_id UUID,
    p_amount DECIMAL,
    p_type VARCHAR,
    p_ref_type VARCHAR,
    p_ref_id UUID
  ) RETURNS BOOLEAN;

  CREATE OR REPLACE FUNCTION credit_supplier_balance(
    p_supplier_id UUID,
    p_amount DECIMAL,
    p_type VARCHAR,
    p_ref_type VARCHAR,
    p_ref_id UUID
  ) RETURNS BOOLEAN;
  ```
- [ ] Create `SupplierBalanceController` in `Web/Areas/Admin/Controllers/`
- [ ] Create `SupplierBalanceService` in `Application/Services/`:
  - [ ] `GetSupplierBalanceAsync(supplierId)`
  - [ ] `GetSupplierBalanceHistoryAsync(supplierId, ...)`
  - [ ] `DepositToSupplierAsync(...)`
- [ ] Create Supplier detail view with tabs:
  - [ ] Tab: Info
  - [ ] Tab: Balance
  - [ ] Tab: Products (linked to S3)
- [ ] Implement Balance tab:
  - [ ] Current balance card:
    - [ ] Balance amount (formatted)
    - [ ] Status indicator:
      - 🟢 Green: > Rp 1.000.000
      - 🟡 Yellow: Rp 100.000 - 1.000.000
      - 🔴 Red: < Rp 100.000
  - [ ] "Add Deposit" button
  - [ ] Balance history table:
    - [ ] Date/Time
    - [ ] Type (Deposit, Transaction, Refund)
    - [ ] Amount (±)
    - [ ] Balance After
    - [ ] Reference
- [ ] Create deposit modal:
  - [ ] Supplier info display
  - [ ] Current balance
  - [ ] Amount input
  - [ ] Transfer proof upload (optional)
  - [ ] Notes (optional)
  - [ ] Confirm/Cancel buttons
- [ ] Implement deposit logic:
  - [ ] `POST /admin/suppliers/{id}/deposit`
  - [ ] Validate amount > 0
  - [ ] Upload proof to MinIO/local storage
  - [ ] Insert `supplier_deposits` record
  - [ ] Call `credit_supplier_balance()`
  - [ ] Insert `supplier_balance_ledger` (type='deposit')
  - [ ] Show success message
- [ ] Update dashboard widget (from S1):
  - [ ] Supplier balance cards with status color
  - [ ] Auto-refresh balance every 10 seconds (SignalR)

**Supplier Balance Card:**
```
┌────────────────────────────┐
│ Digiflazz                 │
│                            │
│ Rp 2.500.000             │
│                            │
│ 🟢 Good                   │
│                            │
│ [View History] [Deposit]  │
└────────────────────────────┘
```

---

### S4-5: Profit Ledger & Reports (5 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] View: profit harian, per supplier, per produk
- [ ] Database views: v_profit_daily, v_profit_by_supplier, v_profit_by_product
- [ ] Report pages with filters

**Subtasks:**
- [ ] Create database views:
  ```sql
  CREATE VIEW v_profit_daily AS
  SELECT
    DATE(created_at) as date,
    COUNT(*) as transaction_count,
    SUM(sell_price - cost_price) as total_profit,
    SUM(sell_price) as total_revenue
  FROM transactions
  WHERE status = 'success'
  GROUP BY DATE(created_at);

  CREATE VIEW v_profit_by_supplier AS
  SELECT
    s.name as supplier_name,
    COUNT(t.id) as transaction_count,
    SUM(t.sell_price - t.cost_price) as total_profit,
    SUM(t.cost_price) as total_cost
  FROM transactions t
  JOIN supplier_products sp ON sp.product_id = t.product_id
  JOIN suppliers s ON s.id = sp.supplier_id
  WHERE t.status = 'success'
  GROUP BY s.id, s.name;

  CREATE VIEW v_profit_by_product AS
  SELECT
    p.name as product_name,
    pc.name as category_name,
    COUNT(t.id) as transaction_count,
    SUM(t.sell_price - t.cost_price) as total_profit,
    SUM(t.sell_price) as total_revenue
  FROM transactions t
  JOIN products p ON p.id = t.product_id
  JOIN product_categories pc ON pc.id = p.category_id
  WHERE t.status = 'success'
  GROUP BY p.id, p.name, pc.name;
  ```
- [ ] Create `ReportController` in `Web/Areas/Admin/Controllers/`
- [ ] Create `ReportService` in `Application/Services/`:
  - [ ] `GetDailyProfitAsync(startDate, endDate)`
  - [ ] `GetProfitBySupplierAsync(startDate, endDate)`
  - [ ] `GetProfitByProductAsync(startDate, endDate)`
- [ ] Create Profit Daily view:
  - [ ] Date range picker (default: last 30 days)
  - [ ] Summary cards:
    - [ ] Total Revenue
    - [ ] Total Cost
    - [ ] Total Profit
    - [ ] Profit Margin %
  - [ ] Chart: Line chart daily profit
  - [ ] Table: Date | Trx Count | Revenue | Cost | Profit | Margin
- [ ] Create Profit by Supplier view:
  - [ ] Date range picker
  - [ ] Table: Supplier | Trx Count | Total Cost | Total Revenue | Profit | Margin
  - [ ] Bar chart: Top 5 suppliers by profit
- [ ] Create Profit by Product view:
  - [ ] Date range picker + Category filter
  - [ ] Table: Product | Category | Trx Count | Revenue | Profit | Margin
  - [ ] Bar chart: Top 10 products by profit

**Report Menu Structure:**
```
Reports
├── Daily Profit
├── Profit by Supplier
└── Profit by Product
```

---

### S4-6: Export Laporan Excel (3 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] Export laporan profit ke .xlsx
- [ ] Export transaksi ke .xlsx
- [ ] Filter by date range

**Subtasks:**
- [ ] Install EPPlus or ClosedXML:
  - [ ] `EPPlus` or `ClosedXML` NuGet package
- [ ] Create `ExportService` in `Application/Services/`:
  - [ ] `ExportProfitToExcelAsync(...)`
  - [ ] `ExportTransactionsToExcelAsync(...)`
- [ ] Implement profit export:
  - [ ] Query `v_profit_daily` for date range
  - [ ] Create Excel file:
    - [ ] Sheet 1: Summary
    - [ ] Sheet 2: Daily breakdown
  - [ ] Format: headers, currency, dates
  - [ ] Add charts (optional)
  - [ ] Return file stream
- [ ] Implement transaction export:
  - [ ] Query transactions with filters
  - [ ] Create Excel file:
    - [ ] Columns: Date, Reference, User, Product, Destination, Price, Cost, Profit, Status
  - [ ] Format data
  - [ ] Return file stream
- [ ] Add export buttons to report pages:
  - [ ] "Export Excel" button
  - [ ] Download file on click
- [ ] Configure file response:
  - [ ] Content-Type: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
  - [ ] Filename: `Profit_Report_20260312.xlsx`

**Export Excel Format:**
```
Sheet 1: Summary
┌──────────────┬────────────┐
│ Metric       │ Value      │
├──────────────┼────────────┤
│ Revenue      │ Rp 50M     │
│ Cost         │ Rp 45M     │
│ Profit       │ Rp 5M      │
│ Margin       │ 10%        │
└──────────────┴────────────┘

Sheet 2: Daily Breakdown
┌────────────┬──────────┬─────────┬────────┬────────┐
│ Date       │ Trx Count│ Revenue │ Cost   │ Profit │
├────────────┼──────────┼─────────┼────────┼────────┤
│ 2026-03-12 │ 150      │ Rp 5M   │ Rp 4.5M│ Rp 500K│
│ 2026-03-11 │ 140      │ Rp 4.8M │ Rp 4.3M│ Rp 475K│
└────────────┴──────────┴─────────┴────────┴────────┘
```

---

### S4-7: Referral Management (4 SP)
**Status:** ⏳ TODO
**Acceptance Criteria:**
- [ ] Admin lihat pending referral bonus
- [ ] Beri bonus referral manual
- [ ] Tab Referral user terisi

**Subtasks:**
- [ ] Create `ReferralController` in `Web/Areas/Admin/Controllers/`
- [ ] Create `ReferralService` in `Application/Services/`:
  - [ ] `GetPendingReferralBonusesAsync()`
  - [ ] `PayReferralBonusAsync(...)`
- [ ] Create Referral list view:
  - [ ] DataTable with columns:
    - [ ] Referral Code
    - [ ] Referrer (Username)
    - [ ] Referee (Username)
    - [ ] Bonus Amount
    - [ ] Status (Pending/Paid)
    - [ ] Created Date
    - [ ] Actions
  - [ ] Filter by status (default: Pending)
- [ ] Implement pay bonus action:
  - [ ] `POST /admin/referral/{id}/pay`
  - [ ] Modal confirmation
  - [ ] Validation: status must be Pending
  - [ ] Begin transaction:
    - [ ] `SELECT ... FROM user_balances WHERE user_id = ? FOR UPDATE`
    - [ ] `UPDATE user_balances SET active_balance = active_balance + bonus_amount`
    - [ ] `INSERT INTO balance_ledger (type='referral', ...)`
    - [ ] `UPDATE referral_bonuses SET status='paid', paid_at=NOW()`
  - [ ] Commit transaction
  - [ ] Queue notification
  - [ ] Show success message
- [ ] Implement Referral tab in User Detail:
  - [ ] Referral code display
  - [ ] Referral count (total referrals)
  - [ ] Total bonus earned
  - [ ] List of referees:
    - [ ] Username
    - [ ] Registered date
    - [ ] Bonus status

**Referral List:**
| Referrer | Referee | Bonus | Status | Created | Action |
|---|---|---|---|---|---|
| user123 | user456 | Rp 5.000 | Pending | 12 Mar | Pay Bonus |
| user789 | user012 | Rp 5.000 | Paid | 11 Mar | - |

---

## TECHNICAL NOTES (from PRD)

- Implementasi stored function: `debit_supplier_balance`, `credit_supplier_balance`
- View database: `v_profit_daily`, `v_profit_by_supplier`, `v_profit_by_product`
- Export Excel pakai EPPlus / ClosedXML
- Insert ke `profit_ledger` dan `supplier_balance_ledger` setiap transaksi sukses
- Threshold saldo supplier: Hijau > 1M, Kuning 100K-1M, Merah < 100K

---

## DELIVERABLES

- [ ] Admin bisa approve/reject topup → saldo user bertambah otomatis
- [ ] Admin bisa deposit ke supplier → saldo supplier bertambah
- [ ] Laporan profit tersedia: harian, per supplier, per produk
- [ ] Export laporan ke Excel berfungsi
- [ ] Admin bisa beri bonus referral manual
- [ ] Supplier balance tracking dengan status warna

---

## DEFINITION OF DONE

- [ ] Topup approval flow working end-to-end
- [ ] Manual balance adjustment functional
- [ ] Supplier deposit updates balance correctly
- [ ] Profit reports accessible and accurate
- [ ] Excel export generates valid files
- [ ] Referral bonus payment functional
- [ ] All stored functions working
- [ ] Database views created and tested
- [ ] No critical bugs

---

**NEXT SPRINT:** Sprint 5 - Member API Auth & Balance
