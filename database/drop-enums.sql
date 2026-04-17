-- Drop existing enum types to allow migration to recreate them
DROP TYPE IF EXISTS admin_role CASCADE;
DROP TYPE IF EXISTS attempt_status CASCADE;
DROP TYPE IF EXISTS balance_tx_type CASCADE;
DROP TYPE IF EXISTS markup_type CASCADE;
DROP TYPE IF EXISTS notification_channel CASCADE;
DROP TYPE IF EXISTS referral_bonus_status CASCADE;
DROP TYPE IF EXISTS topup_status CASCADE;
DROP TYPE IF EXISTS transaction_status CASCADE;
DROP TYPE IF EXISTS user_status CASCADE;
