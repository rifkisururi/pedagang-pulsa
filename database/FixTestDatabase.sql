-- ============================================
-- PedagangPulsa Database Fix Script
-- ============================================
-- This script helps fix database issues encountered during unit testing
-- Run this manually on your PostgreSQL server to resolve constraint conflicts
-- ============================================

-- ============================================
-- SECTION 1: IDENTIFY AND FIX DUPLICATE DATA
-- ============================================

-- 1.1 Check for duplicate UserLevels (by primary key)
-- This happens when tests try to insert UserLevels with existing IDs
SELECT
    "Id" as user_level_id,
    "Name" as level_name,
    "DiscountPercent" as discount_percent,
    "MinTransaction" as min_transaction,
    "CreatedAt" as created_at
FROM "UserLevels"
ORDER BY "Id"
LIMIT 20;

-- 1.2 Check for duplicate ReferralCodes in Users table
SELECT
    "Id",
    "Username",
    "ReferralCode",
    "CreatedAt"
FROM "Users"
WHERE "ReferralCode" IS NOT NULL
ORDER BY "CreatedAt" DESC
LIMIT 50;

-- 1.3 Check for duplicate Product Codes
SELECT
    "Id",
    "Code",
    "Name",
    "Category",
    "IsActive"
FROM "Products"
ORDER BY "Code"
LIMIT 50;

-- 1.4 Find duplicate ReferralCodes
SELECT
    "ReferralCode",
    COUNT(*) as count
FROM "Users"
WHERE "ReferralCode" IS NOT NULL
GROUP BY "ReferralCode"
HAVING COUNT(*) > 1;

-- 1.5 Find duplicate Product Codes
SELECT
    "Code",
    COUNT(*) as count
FROM "Products"
GROUP BY "Code"
HAVING COUNT(*) > 1;

-- ============================================
-- SECTION 2: CLEANUP TEST DATA
-- ============================================

-- 2.1 Reset UserLevels sequence (fixes PK_UserLevels conflicts)
-- This resets the ID sequence to avoid conflicts with existing data
SELECT setval('"UserLevels_Id_seq"', (SELECT COALESCE(MAX("Id"), 0) + 1 FROM "UserLevels"), false);

-- 2.2 Reset Products sequence
SELECT setval('"Products_Id_seq"', (SELECT COALESCE(MAX("Id"), 0) + 1 FROM "Products"), false);

-- 2.3 Reset Users sequence
SELECT setval('"Users_Id_seq"', (SELECT COALESCE(MAX("Id"), 0) + 1 FROM "Users"), false);

-- 2.4 Reset SupplierProducts sequence
SELECT setval('"SupplierProducts_Id_seq"', (SELECT COALESCE(MAX("Id"), 0) + 1 FROM "SupplierProducts"), false);

-- ============================================
-- SECTION 3: FIX SPECIFIC CONSTRAINT ISSUES
-- ============================================

-- 3.1 Remove duplicate UserLevels (keep the latest one)
WITH ranked_user_levels AS (
    SELECT
        "Id",
        "Name",
        ROW_NUMBER() OVER (PARTITION BY "Name" ORDER BY "Id" DESC) as rn
    FROM "UserLevels"
)
DELETE FROM "UserLevels"
WHERE "Id" IN (
    SELECT "Id" FROM ranked_user_levels WHERE rn > 1
);

-- 3.2 Remove duplicate ReferralCodes (keep the latest one)
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

-- 3.3 Remove duplicate Product Codes (keep the latest one)
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
-- SECTION 4: VERIFY DATA INTEGRITY
-- ============================================

-- 4.1 Check UserLevels integrity
SELECT
    'UserLevels' as table_name,
    COUNT(*) as total_records,
    COUNT(DISTINCT "Id") as unique_ids,
    COUNT(DISTINCT "Name") as unique_names
FROM "UserLevels";

-- 4.2 Check Users integrity
SELECT
    'Users' as table_name,
    COUNT(*) as total_records,
    COUNT(DISTINCT "Id") as unique_ids,
    COUNT(DISTINCT "Username") as unique_usernames,
    COUNT(DISTINCT "ReferralCode") as unique_referral_codes
FROM "Users";

-- 4.3 Check Products integrity
SELECT
    'Products' as table_name,
    COUNT(*) as total_records,
    COUNT(DISTINCT "Id") as unique_ids,
    COUNT(DISTINCT "Code") as unique_codes
FROM "Products";

-- ============================================
-- SECTION 5: REINDEX TABLES (OPTIONAL)
-- ============================================

-- Reindex tables to improve performance
-- Uncomment if needed

-- REINDEX TABLE "UserLevels";
-- REINDEX TABLE "Users";
-- REINDEX TABLE "Products";
-- REINDEX TABLE "SupplierProducts";

-- ============================================
-- SECTION 6: TEST DATA CLEANUP (OPTIONAL)
-- ============================================

-- WARNING: This will DELETE all test data. Only run if you want to start fresh!

-- 6.1 Delete all test data (uncomment to use)
-- DELETE FROM "Transactions" WHERE "CreatedAt" > NOW() - INTERVAL '1 day';
-- DELETE FROM "BalanceLedgers" WHERE "CreatedAt" > NOW() - INTERVAL '1 day';
-- DELETE FROM "UserBalances" WHERE "CreatedAt" > NOW() - INTERVAL '1 day';
-- DELETE FROM "TopupRequests" WHERE "CreatedAt" > NOW() - INTERVAL '1 day';
-- DELETE FROM "ReferralLogs" WHERE "CreatedAt" > NOW() - INTERVAL '1 day';
-- DELETE FROM "ProductPrices" WHERE "ProductId" IN (SELECT "Id" FROM "Products" WHERE "CreatedAt" > NOW() - INTERVAL '1 day');
-- DELETE FROM "SupplierProducts" WHERE "CreatedAt" > NOW() - INTERVAL '1 day';
-- DELETE FROM "Products" WHERE "CreatedAt" > NOW() - INTERVAL '1 day';
-- DELETE FROM "UserLevels" WHERE "CreatedAt" > NOW() - INTERVAL '1 day';
-- DELETE FROM "Users" WHERE "CreatedAt" > NOW() - INTERVAL '1 day';
-- DELETE FROM "Suppliers" WHERE "CreatedAt" > NOW() - INTERVAL '1 day';

-- ============================================
-- NOTES:
-- ============================================
-- 1. Always backup your database before running cleanup scripts
-- 2. Test data conflicts occur when unit tests use hardcoded IDs that conflict with existing data
-- 3. The proper fix is to update the tests to use dynamic IDs or properly clean up after each test
-- 4. This script provides temporary relief but doesn't fix the root cause
-- 5. Consider using transactions in your tests and rolling back after each test
-- ============================================

-- Script completed
SELECT 'Database fix script completed' as status;
