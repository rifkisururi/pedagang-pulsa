-- Setup Test Database for PedagangPulsa
-- Run this file manually to prepare the database

-- Enable detailed error messages
SET client_min_messages TO NOTICE;

-- Drop all existing data
TRUNCATE TABLE "TransactionAttempts" CASCADE;
TRUNCATE TABLE "SupplierCallbacks" CASCADE;
TRUNCATE TABLE "Transactions" CASCADE;
TRUNCATE TABLE "IdempotencyKeys" CASCADE;
TRUNCATE TABLE "SupplierProducts" CASCADE;
TRUNCATE TABLE "ProductLevelPrices" CASCADE;
TRUNCATE TABLE "Products" CASCADE;
TRUNCATE TABLE "ProductCategories" CASCADE;
TRUNCATE TABLE "BalanceLedgers" CASCADE;
TRUNCATE TABLE "UserBalances" CASCADE;
TRUNCATE TABLE "RefreshTokens" CASCADE;
TRUNCATE TABLE "Users" CASCADE;
TRUNCATE TABLE "UserLevelConfigs" CASCADE;
TRUNCATE TABLE "UserLevels" CASCADE;
TRUNCATE TABLE "ReferralLogs" CASCADE;
TRUNCATE TABLE "TopupRequests" CASCADE;
TRUNCATE TABLE "SupplierBalanceLedgers" CASCADE;
TRUNCATE TABLE "SupplierBalances" CASCADE;
TRUNCATE TABLE "Suppliers" CASCADE;
TRUNCATE TABLE "AdminUsers" CASCADE;

-- Restart all sequences to 1
ALTER SEQUENCE "ProductCategories_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "UserLevels_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "SupplierBalances_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "SupplierProducts_Id_seq" RESTART WITH 1;
ALTER SEQUENCE "ProductLevelPrices_Id_seq" RESTART WITH 1;

-- Insert base test data
INSERT INTO "UserLevels" ("Id", "Name", "Description", "MarkupType", "MarkupValue", "CanTransfer", "IsActive")
VALUES
    (1, 'Member1', 'Basic member level', 0, 5.0, false, true),
    (2, 'Member2', 'Advanced member level', 0, 3.0, true, true),
    (3, 'Member3', 'Premium member level', 0, 1.0, true, true);

INSERT INTO "ProductCategories" ("Id", "Name", "Code", "SortOrder", "IsActive")
VALUES
    (1, 'Pulsa', 'PULSA', 1, true),
    (2, 'Data', 'DATA', 2, true),
    (3, 'E-Wallet', 'EWALLET', 3, true);

-- Verify inserts
SELECT 'UserLevels created:' as info, COUNT(*) as count FROM "UserLevels";
SELECT 'ProductCategories created:' as info, COUNT(*) as count FROM "ProductCategories";
