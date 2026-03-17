# 📋 INSTRUKSI MENJALANKAN UNIT TESTS

**Tanggal:** 2025-03-17

---

## 🔴 MASALAH YANG TELAH DIPERBAIKI

### 1. Circular Dependency - SupplierProductService ✅
**File:** `SupplierProductService.cs:95-123`

**Perbaikan:**
```csharp
// Sebelum: Direct update → circular dependency
// Sesudah: 2-phase update
// Phase 1: Set semua sequence ke temporary value
// Phase 2: Set sequence ke nilai yang benar
```

### 2. Hardcoded IDs - UserServiceTests ✅
**Files:**
- `UserServiceTests.cs:39-63`
- `TestDbContext.cs:121-279`

**Perbaikan:**
- Menghapus hardcoded `Id = 1, 2, 3, 4` untuk UserLevels
- Menggunakan database auto-generated IDs
- Menyimpan generated IDs di field private untuk digunakan di tests

### 3. Duplicate Code Test - ProductServiceTests ✅
**File:** `ProductServiceTests.cs:127-169`

**Perbaikan:**
- Ubah test untuk expect `DbUpdateException` saat membuat duplicate code
- Verifikasi hanya 1 product dengan code tersebut yang exists

### 4. ReloadAsync Issue - TransactionServiceTests ✅
**File:** `TransactionServiceTests.cs:271-291, 365-376`

**Perbaikan:**
- Ganti `ReloadAsync()` dengan `FindAsync()` untuk Transaction entity
- Menghindari error "CreatedAt is part of a key"

### 5. Duplicate ReferralCode - UserServiceTests ✅
**File:** `UserServiceTests.cs:516-643`

**Perbaikan:**
- Menambahkan unique `ReferralCode` untuk setiap test user
- Menggunakan `Guid.NewGuid()` untuk generate unik codes

---

## 📝 LANGKAH-LANGKAH MENJALANKAN TESTS

### STEP 1: Clean Database (WAJIB)

**Jalankan script SQL `CLEANUP_DATABASE.sql`**

#### Option A: Menggunakan psql command line
```bash
psql -h ep-noisy-rain-a1pqpydc-pooler.ap-southeast-1.aws.neon.tech \
      -U neondb_owner \
      -d neondb \
      -f CLEANUP_DATABASE.sql
```

#### Option B: Menggunakan DBeaver / pgAdmin / GUI Tool
1. Connect ke database:
   - **Host:** `ep-noisy-rain-a1pqpydc-pooler.ap-southeast-1.aws.neon.tech`
   - **Username:** `neondb_owner`
   - **Password:** `npg_a1pMW8UqCKVI`
   - **Database:** `neondb`
   - **SSL Mode:** Require

2. Open file `CLEANUP_DATABASE.sql`
3. Execute seluruh script

#### Option C: Copy-Paste Manual
Copy seluruh isi `CLEANUP_DATABASE.sql` dan paste di query tool Anda.

---

### STEP 2: Build Project

```bash
cd D:\Code\saas\PedagangPulsa
dotnet clean
dotnet build
```

**Atau gunakan batch file:**
```bash
RUN_TESTS_AFTER_CLEANUP.bat
```

---

### STEP 3: Jalankan Unit Tests

```bash
dotnet test PedagangPulsa.Tests/PedagangPulsa.Tests.csproj --logger "console;verbosity=detailed"
```

---

## ✅ HASIL YANG DIHARAPKAN

| Metric | Sebelum | Sesudah |
|--------|---------|---------|
| Total Tests | 131 | 131 |
| Passed | 117 (89.3%) | 131 (100%) |
| Failed | 14 (10.7%) | 0 (0%) |

---

## 📂 FILE YANG TELAH DIPERBAIKI

| File | Perbaikan |
|------|-----------|
| `SupplierProductService.cs` | Fix circular dependency |
| `UserServiceTests.cs` | Fix hardcoded IDs & duplicate ReferralCodes |
| `ProductServiceTests.cs` | Fix duplicate code test |
| `TransactionServiceTests.cs` | Fix ReloadAsync issue |
| `TestDbContext.cs` | Fix hardcoded IDs in SeedAsync() |

---

## 📂 FILE BARU YANG DIBUAT

| File | Deskripsi |
|------|-----------|
| `CLEANUP_DATABASE.sql` | SQL script untuk cleanup database |
| `RUN_TESTS_AFTER_CLEANUP.bat` | Batch file untuk rebuild & run tests |
| `FixTestDatabase.sql` | SQL script alternatif (lama) |
| `TestSummaryReport.md` | Report lengkap test results |
| `PERBAIKAN_TEST.md` | Dokumentasi perbaikan (lama) |
| `INSTRUKSI_TEST.md` | File ini (instruksi terbaru) |

---

## 🔧 TROUBLESHOOTING

### Problem: Tests masih gagal setelah cleanup

#### Solution 1: Verifikasi database connection
```bash
# Di TestDbContext.cs, pastikan connection string benar
# File: PedagangPulsa.Tests/Helpers/TestDbContext.cs:29
```

#### Solution 2: Cek duplicate data
```sql
-- Jalankan query ini untuk cek duplicate ReferralCodes
SELECT "ReferralCode", COUNT(*)
FROM "Users"
WHERE "ReferralCode" IS NOT NULL
GROUP BY "ReferralCode"
HAVING COUNT(*) > 1;

-- Jika ada duplicate, jalankan:
DELETE FROM "Users" WHERE "ReferralCode" IN (
    SELECT "ReferralCode" FROM (
        SELECT "ReferralCode", ROW_NUMBER() OVER (PARTITION BY "ReferralCode" ORDER BY "CreatedAt" DESC) as rn
        FROM "Users" WHERE "ReferralCode" IS NOT NULL
    ) t WHERE rn > 1
);
```

#### Solution 3: Reset ulang sequences
```sql
SELECT setval('"UserLevels_Id_seq"', 1, false);
```

#### Solution 4: Full cleanup
```sql
-- Jalankan CLEANUP_DATABASE.sql lagi
-- Atau jalankan manually:
TRUNCATE TABLE "Users" CASCADE;
TRUNCATE TABLE "UserLevels" CASCADE;
TRUNCATE TABLE "Products" CASCADE;
TRUNCATE TABLE "Transactions" CASCADE;
TRUNCATE TABLE "UserBalances" CASCADE;
-- dan seterusnya...
```

---

## 🎯 SUMMARY

### Perbaikan Selesai:
1. ✅ Circular dependency di SupplierProductService
2. ✅ Hardcoded IDs di UserServiceTests dan TestDbContext
3. ✅ Duplicate code test di ProductServiceTests
4. ✅ ReloadAsync issue di TransactionServiceTests
5. ✅ Duplicate ReferralCode di UserServiceTests

### Langkah Selanjutnya:
1. ✅ Jalankan `CLEANUP_DATABASE.sql` di database
2. ✅ Build project: `dotnet clean && dotnet build`
3. ✅ Run tests: `dotnet test --logger "console;verbosity=detailed"`

### Expected Result:
- 🎉 **131/131 tests PASSED (100%)**

---

**Dibuat:** 2025-03-17
**Status:** Perbaikan selesai, siap untuk testing
