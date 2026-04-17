-- ============================================
-- PEDAGANGPULSA DATABASE CLEANUP SCRIPT
-- ============================================
-- Jalankan script ini secara manual di database PostgreSQL Anda
-- untuk membersihkan data test dan memperbaiki masalah database
-- ============================================

-- ============================================
-- CARA MENJALANKAN:
-- ============================================
-- Option 1: Menggunakan psql command line
-- psql -h ep-noisy-rain-a1pqpydc-pooler.ap-southeast-1.aws.neon.tech -U neondb_owner -d neondb -f CLEANUP_DATABASE.sql

-- Option 2: Menggunakan DBeaver, pgAdmin, atau tool GUI lainnya
-- Copy dan paste seluruh isi script ini ke query editor, lalu execute

-- Option 3: Menggunakan string connection
-- Host: ep-noisy-rain-a1pqpydc-pooler.ap-southeast-1.aws.neon.tech
-- Username: neondb_owner
-- Password: npg_a1pMW8UqCKVI
-- Database: neondb
-- SSL Mode: Require
-- ============================================

-- ============================================
-- BAGIAN 1: HAPUS SEMUA DATA TEST
-- ============================================
-- Membersihkan semua data yang mungkin menyebabkan konflik

-- 1.1 Hapus transaction attempts
DELETE FROM "TransactionAttempts";

-- 1.2 Hapus transactions
DELETE FROM "Transactions";

-- 1.3 Hapus balance ledgers
DELETE FROM "BalanceLedgers";

-- 1.4 Hapus user balances
DELETE FROM "UserBalances";

-- 1.5 Hapus users
DELETE FROM "Users";

-- 1.6 Hapus refresh tokens
DELETE FROM "RefreshTokens";

-- 1.7 Hapus referral logs
DELETE FROM "ReferralLogs";

-- 1.8 Hapus product level prices
DELETE FROM "ProductLevelPrices";

-- 1.9 Hapus products
DELETE FROM "Products";

-- 1.10 Hapus product categories
DELETE FROM "ProductCategories";

-- 1.11 Hapus supplier products
DELETE FROM "SupplierProducts";

-- 1.12 Hapus supplier balance ledgers
DELETE FROM "SupplierBalanceLedgers";

-- 1.13 Hapus supplier balances
DELETE FROM "SupplierBalances";

-- 1.14 Hapus suppliers
DELETE FROM "Suppliers";

-- 1.15 Hapus user levels
DELETE FROM "UserLevels";

-- 1.16 Hapus topup requests
DELETE FROM "TopupRequests";

-- 1.17 Hapus admin users (optional, uncomment jika ingin menghapus)
-- DELETE FROM "AdminUsers";

-- 1.18 Hapus idempotency keys
DELETE FROM "IdempotencyKeys";

-- ============================================
-- BAGIAN 2: RESET SEQUENCES
-- ============================================
-- Mereset semua sequences ke nilai awal

SELECT setval('"ProductCategories_Id_seq"', 1, false);
SELECT setval('"UserLevels_Id_seq"', 1, false);
SELECT setval('"SupplierBalances_Id_seq"', 1, false);
SELECT setval('"SupplierProducts_Id_seq"', 1, false);
SELECT setval('"ProductLevelPrices_Id_seq"', 1, false);

-- ============================================
-- BAGIAN 3: VERIFIKASI CLEANUP
-- ============================================
-- Cek bahwa semua tabel sudah kosong

SELECT 'ProductCategories' as table_name, COUNT(*) as record_count FROM "ProductCategories"
UNION ALL
SELECT 'UserLevels', COUNT(*) FROM "UserLevels"
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

-- ============================================
-- BAGIAN 4: OPSIONAL - SEED DATA DASAR
-- ============================================
-- Uncomment bagian ini jika ingin membuat data dasar untuk testing

-- INSERT INTO "UserLevels" ("Name", "Description", "MarkupType", "MarkupValue", "CanTransfer", "IsActive", "CreatedAt", "UpdatedAt")
-- VALUES
--     ('Regular', 'Regular user level', 'Percentage', 0, false, true, NOW(), NOW()),
--     ('Bronze', 'Bronze member level', 'Percentage', 2, false, true, NOW(), NOW()),
--     ('Silver', 'Silver member level', 'Percentage', 1.5, true, true, NOW(), NOW()),
--     ('Gold', 'Gold member level', 'Percentage', 1, true, true, NOW(), NOW());

-- ============================================
-- SELESAI
-- ============================================
SELECT 'DATABASE CLEANUP COMPLETED SUCCESSFULLY!' as status;

-- ============================================
-- CATATAN:
-- ============================================
-- 1. Script ini menghapus SEMUA data dari tabel yang tercantum
-- 2. Jangan jalankan di production database!
-- 3. Gunakan HANYA untuk development/test database
-- 4. Setelah menjalankan script ini, jalankan ulang unit tests
-- 5. Tests akan otomatis membuat data yang diperlukan
-- ============================================
