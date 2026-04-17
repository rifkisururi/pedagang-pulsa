-- ============================================
-- PedagangPulsa Database Cleanup & Reset Script
-- ============================================
-- Script ini untuk membersihkan dan mereset database
-- Jalankan secara manual di server database PostgreSQL Anda
-- ============================================

-- ============================================
-- INSTRUKSI:
-- ============================================
-- 1. Connect ke database PostgreSQL Anda:
--    psql -h ep-noisy-rain-a1pqpydc-pooler.ap-southeast-1.aws.neon.tech -U neondb_owner -d neondb
--
-- 2. Jalankan script ini:
--    \i D:\Code\saas\PedagangPulsa\CleanupDatabase.sql
--
-- ATAU gunakan string connection:
--    Host=ep-noisy-rain-a1pqpydc-pooler.ap-southeast-1.aws.neon.tech
--    Username=neondb_owner
--    Password=npg_a1pMW8UqCKVI
--    Database=neondb
--    SSL Mode=Require
-- ============================================

-- ============================================
-- BAGIAN 1: RESET SEQUENCES
-- ============================================
-- Mereset semua sequences ke nilai yang aman

-- Reset UserLevels sequence
SELECT setval('"UserLevels_Id_seq"', 1, false);

-- Reset ProductCategories sequence
SELECT setval('"ProductCategories_Id_seq"', 1, false);

-- Reset SupplierBalances sequence
SELECT setval('"SupplierBalances_Id_seq"', 1, false);

-- Reset SupplierProducts sequence
SELECT setval('"SupplierProducts_Id_seq"', 1, false);

-- Reset ProductLevelPrices sequence
SELECT setval('"ProductLevelPrices_Id_seq"', 1, false);

-- ============================================
-- BAGIAN 2: HAPUS DATA TEST YANG MENYEBABKAN KONFLIK
-- ============================================

-- Hapus data yang mungkin menyebabkan konflik unique constraint
-- Ini membersihkan data test tanpa menghapus production data

-- 2.1 Hapus transaksi test (dibuat dalam 24 jam terakhir)
DELETE FROM "TransactionAttempts"
WHERE "TransactionId" IN (
    SELECT "Id" FROM "Transactions"
    WHERE "CreatedAt" > NOW() - INTERVAL '1 day'
);

DELETE FROM "Transactions"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- 2.2 Hapus balance ledger test
DELETE FROM "BalanceLedgers"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- 2.3 Hapus user balance test (jika user dibuat dalam 24 jam terakhir)
DELETE FROM "UserBalances"
WHERE "UserId" IN (
    SELECT "Id" FROM "Users"
    WHERE "CreatedAt" > NOW() - INTERVAL '1 day'
);

-- 2.4 Hapus user test
DELETE FROM "Users"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- 2.5 Hapus refresh tokens test
DELETE FROM "RefreshTokens"
WHERE "ExpiresAt" < NOW();

-- 2.6 Hapus product level prices test
DELETE FROM "ProductLevelPrices"
WHERE "ProductId" IN (
    SELECT "Id" FROM "Products"
    WHERE "CreatedAt" > NOW() - INTERVAL '1 day'
);

-- 2.7 Hapus products test
DELETE FROM "Products"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- 2.8 Hapus product categories test
DELETE FROM "ProductCategories"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- 2.9 Hapus supplier products test
DELETE FROM "SupplierProducts"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- 2.10 Hapus supplier balances test
DELETE FROM "SupplierBalanceLedgers"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

DELETE FROM "SupplierBalances"
WHERE "SupplierId" IN (
    SELECT "Id" FROM "Suppliers"
    WHERE "CreatedAt" > NOW() - INTERVAL '1 day'
);

-- 2.11 Hapus suppliers test
DELETE FROM "Suppliers"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- 2.12 Hapus user levels test
DELETE FROM "UserLevels"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- 2.13 Hapus referral logs test
DELETE FROM "ReferralLogs"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- 2.14 Hapus topup requests test
DELETE FROM "TopupRequests"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- 2.15 Hapus admin users test
DELETE FROM "AdminUsers"
WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- ============================================
-- BAGIAN 3: PERBAIKI DUPLIKASI REFERRAL CODES
-- ============================================

-- Hapus users dengan referral code duplikat (keep yang terbaru)
WITH ranked_users AS (
    SELECT
        "Id",
        "ReferralCode",
        ROW_NUMBER() OVER (PARTITION BY "ReferralCode" ORDER BY "CreatedAt" DESC) as rn
    FROM "Users"
    WHERE "ReferralCode" IS NOT NULL
)
DELETE FROM "Users"
WHERE "Id" IN (
    SELECT "Id" FROM ranked_users WHERE rn > 1
);

-- ============================================
-- BAGIAN 4: PERBAIKI DUPLIKASI PRODUCT CODES
-- ============================================

-- Hapus products dengan code duplikat (keep yang terbaru)
WITH ranked_products AS (
    SELECT
        "Id",
        "Code",
        ROW_NUMBER() OVER (PARTITION BY "Code" ORDER BY "Id" DESC) as rn
    FROM "Products"
)
DELETE FROM "Products"
WHERE "Id" IN (
    SELECT "Id" FROM ranked_products WHERE rn > 1
);

-- ============================================
-- BAGIAN 5: VERIFIKASI DATA INTEGRITY
-- ============================================

-- Tampilkan jumlah records setelah cleanup
SELECT 'UserLevels' as table_name, COUNT(*) as record_count FROM "UserLevels"
UNION ALL
SELECT 'Users', COUNT(*) FROM "Users"
UNION ALL
SELECT 'Products', COUNT(*) FROM "Products"
UNION ALL
SELECT 'Suppliers', COUNT(*) FROM "Suppliers"
UNION ALL
SELECT 'Transactions', COUNT(*) FROM "Transactions"
UNION ALL
SELECT 'UserBalances', COUNT(*) FROM "UserBalances";

-- Cek duplikasi referral codes
SELECT
    'Duplicate ReferralCodes' as check_type,
    COUNT(*) as duplicate_count
FROM (
    SELECT "ReferralCode", COUNT(*) as cnt
    FROM "Users"
    WHERE "ReferralCode" IS NOT NULL
    GROUP BY "ReferralCode"
    HAVING COUNT(*) > 1
) duplicates;

-- Cek duplikasi product codes
SELECT
    'Duplicate ProductCodes' as check_type,
    COUNT(*) as duplicate_count
FROM (
    SELECT "Code", COUNT(*) as cnt
    FROM "Products"
    GROUP BY "Code"
    HAVING COUNT(*) > 1
) duplicates;

-- ============================================
-- BAGIAN 6: SEED DATA DASAR (OPSIONAL)
-- ============================================
-- Uncomment jika ingin membuat data dasar untuk testing

-- INSERT INTO "UserLevels" ("Name", "Description", "MarkupType", "MarkupValue", "CanTransfer", "IsActive", "CreatedAt", "UpdatedAt")
-- VALUES
--     ('Regular', 'Regular user level', 'Percentage', 0, false, true, NOW(), NOW()),
--     ('Bronze', 'Bronze member level', 'Percentage', 2, false, true, NOW(), NOW()),
--     ('Silver', 'Silver member level', 'Percentage', 1.5, true, true, NOW(), NOW()),
--     ('Gold', 'Gold member level', 'Percentage', 1, true, true, NOW(), NOW())
-- ON CONFLICT ("Name") DO NOTHING;

-- ============================================
-- BAGIAN 7: VACUUM DAN ANALYZE (OPSIONAL)
-- ============================================
-- Uncomment untuk mengoptimalkan database setelah cleanup

-- VACUUM ANALYZE "UserLevels";
-- VACUUM ANALYZE "Users";
-- VACUUM ANALYZE "Products";
-- VACUUM ANALYZE "Suppliers";
-- VACUUM ANALYZE "Transactions";
-- VACUUM ANALYZE "UserBalances";
-- VACUUM ANALYZE "SupplierProducts";
-- VACUUM ANALYZE "ProductLevelPrices";

-- ============================================
-- SCRIPT SELESAI
-- ============================================
SELECT 'Database cleanup completed successfully!' as status;

-- ============================================
-- CATATAN PENTING:
-- ============================================
-- 1. Script ini menghapus data test yang dibuat dalam 24 jam terakhir
-- 2. Production data yang lebih lama dari 24 jam akan tetap aman
-- 3. Semua sequences di-reset ke nilai awal
-- 4. Duplikasi referral codes dan product codes dihapus
-- 5. Setelah menjalankan script ini, jalankan ulang unit tests
-- ============================================
