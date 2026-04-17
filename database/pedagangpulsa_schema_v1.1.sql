-- ============================================================
-- PEDAGANGPULSA.COM - PostgreSQL Database Schema
-- Version: 1.1 | Date: 2026-03-07
-- Changelog:
--   - product_level_prices: harga jual mengikat per produk per level
--   - users: tambah referral_code + referred_by
--   - NEW: referral_logs (tracking undangan + bonus manual)
-- ============================================================

-- ============================================================
-- EXTENSIONS
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "pg_partman";

-- ============================================================
-- ENUMS
-- ============================================================
CREATE TYPE user_status          AS ENUM ('active', 'inactive', 'suspended');
CREATE TYPE transaction_status   AS ENUM ('pending', 'processing', 'success', 'failed', 'refunded', 'cancelled');
CREATE TYPE attempt_status       AS ENUM ('pending', 'processing', 'success', 'failed', 'timeout');
CREATE TYPE topup_status         AS ENUM ('pending', 'approved', 'rejected');
CREATE TYPE notification_channel AS ENUM ('email', 'sms', 'whatsapp');
CREATE TYPE markup_type          AS ENUM ('percentage', 'fixed');
CREATE TYPE admin_role           AS ENUM ('superadmin', 'admin', 'finance', 'staff');
CREATE TYPE referral_bonus_status AS ENUM ('pending', 'paid', 'cancelled');
CREATE TYPE balance_tx_type      AS ENUM (
  'topup',
  'purchase_hold',
  'purchase_debit',
  'purchase_release',
  'transfer_out',
  'transfer_in',
  'refund',
  'adjustment'
);

-- ============================================================
-- MODULE 1: USER & AUTH
-- ============================================================

CREATE TABLE user_levels (
  id            SERIAL        PRIMARY KEY,
  name          VARCHAR(50)   NOT NULL UNIQUE,
  description   TEXT,
  markup_type   markup_type   NOT NULL DEFAULT 'percentage',
  markup_value  NUMERIC(10,4) NOT NULL DEFAULT 0,
  can_transfer  BOOLEAN       NOT NULL DEFAULT TRUE,
  is_active     BOOLEAN       NOT NULL DEFAULT TRUE,
  created_at    TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
  updated_at    TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

-- Konfigurasi limit per level (key-value extensible)
-- Contoh key: max_daily_topup, max_single_trx, max_daily_trx_amount, max_transfer_per_day
CREATE TABLE user_level_configs (
  id           SERIAL       PRIMARY KEY,
  level_id     INT          NOT NULL REFERENCES user_levels(id) ON DELETE CASCADE,
  config_key   VARCHAR(100) NOT NULL,
  config_value TEXT         NOT NULL,
  description  TEXT,
  UNIQUE (level_id, config_key)
);

CREATE TABLE users (
  id                    UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
  username              VARCHAR(50)  NOT NULL UNIQUE,
  full_name             VARCHAR(100),
  email                 VARCHAR(100) UNIQUE,
  phone                 VARCHAR(20)  UNIQUE,
  pin_hash              VARCHAR(255) NOT NULL,
  pin_failed_attempts   SMALLINT     NOT NULL DEFAULT 0,
  pin_locked_at         TIMESTAMPTZ,
  level_id              INT          NOT NULL REFERENCES user_levels(id),
  can_transfer_override BOOLEAN      DEFAULT NULL,  -- NULL=ikut level, TRUE/FALSE=override

  -- === REFERRAL ===
  referral_code         VARCHAR(20)  UNIQUE NOT NULL,  -- kode unik user ini untuk dibagikan
  referred_by           UUID         REFERENCES users(id) ON DELETE SET NULL,
  -- referred_by diisi saat register jika user memasukkan kode referral orang lain

  status                user_status  NOT NULL DEFAULT 'active',
  email_verified_at     TIMESTAMPTZ,
  phone_verified_at     TIMESTAMPTZ,
  created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
  updated_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_users_email        ON users(email);
CREATE INDEX idx_users_phone        ON users(phone);
CREATE INDEX idx_users_level        ON users(level_id);
CREATE INDEX idx_users_referral_code ON users(referral_code);
CREATE INDEX idx_users_referred_by  ON users(referred_by) WHERE referred_by IS NOT NULL;

-- ============================================================
-- MODULE 2: REFERRAL
-- ============================================================

/*
  ALUR REFERRAL:
  1. User A punya referral_code = 'AGUS2026'
  2. User B register dan memasukkan kode 'AGUS2026'
     -> users.referred_by = User A .id
     -> INSERT referral_logs (referrer=A, referee=B, status=pending)
  3. Admin cek laporan referral, beri bonus manual via topup/adjustment
     -> UPDATE referral_logs SET status='paid', bonus_amount=X, paid_at=NOW(), paid_by=admin_id
     -> Mutasi saldo User A dicatat di balance_ledger (type=adjustment, ref_type='referral')
*/
CREATE TABLE referral_logs (
  id             UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  referrer_id    UUID          NOT NULL REFERENCES users(id),  -- pengundang
  referee_id     UUID          NOT NULL REFERENCES users(id),  -- yang diundang
  bonus_amount   NUMERIC(15,2) DEFAULT NULL,    -- diisi admin saat beri bonus
  bonus_status   referral_bonus_status NOT NULL DEFAULT 'pending',
  notes          TEXT,                          -- catatan admin
  paid_by        UUID,                          -- admin_users.id
  paid_at        TIMESTAMPTZ,
  created_at     TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
  updated_at     TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
  UNIQUE (referee_id)  -- 1 referee hanya bisa punya 1 referrer
);
CREATE INDEX idx_referral_referrer ON referral_logs(referrer_id, bonus_status);
CREATE INDEX idx_referral_pending  ON referral_logs(bonus_status, created_at) WHERE bonus_status = 'pending';

-- Token reset PIN
CREATE TABLE pin_reset_tokens (
  id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id     UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  token_hash  VARCHAR(255) NOT NULL,
  channel     VARCHAR(10)  NOT NULL CHECK (channel IN ('sms', 'email')),
  expires_at  TIMESTAMPTZ  NOT NULL,
  used_at     TIMESTAMPTZ,
  created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_pin_reset_user ON pin_reset_tokens(user_id, expires_at);

-- ============================================================
-- MODULE 3: BALANCE
-- ============================================================

/*
  DUA JENIS SALDO:
  - active_balance : saldo yang siap digunakan
  - held_balance   : saldo yang sedang ditahan selama transaksi berlangsung

  Mutasi saldo SELALU menggunakan stored function di bawah (SELECT FOR UPDATE).
  balance_ledger bersifat APPEND-ONLY — dilarang UPDATE / DELETE.
*/
CREATE TABLE user_balances (
  user_id        UUID          PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
  active_balance NUMERIC(15,2) NOT NULL DEFAULT 0 CHECK (active_balance >= 0),
  held_balance   NUMERIC(15,2) NOT NULL DEFAULT 0 CHECK (held_balance >= 0),
  updated_at     TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE TABLE balance_ledger (
  id             BIGSERIAL        NOT NULL,
  user_id        UUID             NOT NULL REFERENCES users(id),
  type           balance_tx_type  NOT NULL,
  amount         NUMERIC(15,2)    NOT NULL CHECK (amount > 0),
  active_before  NUMERIC(15,2)    NOT NULL,
  active_after   NUMERIC(15,2)    NOT NULL,
  held_before    NUMERIC(15,2)    NOT NULL,
  held_after     NUMERIC(15,2)    NOT NULL,
  ref_type       VARCHAR(50),     -- 'transaction' | 'topup' | 'transfer' | 'referral' | 'adjustment'
  ref_id         UUID,
  notes          TEXT,
  created_by     UUID,            -- admin_users.id jika mutasi manual
  created_at     TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
  PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

CREATE TABLE balance_ledger_y2026m01 PARTITION OF balance_ledger FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');
CREATE TABLE balance_ledger_y2026m02 PARTITION OF balance_ledger FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');
CREATE TABLE balance_ledger_y2026m03 PARTITION OF balance_ledger FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');
CREATE TABLE balance_ledger_y2026m04 PARTITION OF balance_ledger FOR VALUES FROM ('2026-04-01') TO ('2026-05-01');
CREATE TABLE balance_ledger_y2026m05 PARTITION OF balance_ledger FOR VALUES FROM ('2026-05-01') TO ('2026-06-01');
CREATE TABLE balance_ledger_y2026m06 PARTITION OF balance_ledger FOR VALUES FROM ('2026-06-01') TO ('2026-07-01');
CREATE TABLE balance_ledger_y2026m07 PARTITION OF balance_ledger FOR VALUES FROM ('2026-07-01') TO ('2026-08-01');
CREATE TABLE balance_ledger_y2026m08 PARTITION OF balance_ledger FOR VALUES FROM ('2026-08-01') TO ('2026-09-01');
CREATE TABLE balance_ledger_y2026m09 PARTITION OF balance_ledger FOR VALUES FROM ('2026-09-01') TO ('2026-10-01');
CREATE TABLE balance_ledger_y2026m10 PARTITION OF balance_ledger FOR VALUES FROM ('2026-10-01') TO ('2026-11-01');
CREATE TABLE balance_ledger_y2026m11 PARTITION OF balance_ledger FOR VALUES FROM ('2026-11-01') TO ('2026-12-01');
CREATE TABLE balance_ledger_y2026m12 PARTITION OF balance_ledger FOR VALUES FROM ('2026-12-01') TO ('2027-01-01');

CREATE INDEX idx_ledger_user_date ON balance_ledger(user_id, created_at DESC);
CREATE INDEX idx_ledger_ref       ON balance_ledger(ref_type, ref_id) WHERE ref_id IS NOT NULL;

CREATE TABLE bank_accounts (
  id             SERIAL       PRIMARY KEY,
  bank_name      VARCHAR(50)  NOT NULL,
  account_number VARCHAR(30)  NOT NULL,
  account_name   VARCHAR(100) NOT NULL,
  is_active      BOOLEAN      NOT NULL DEFAULT TRUE,
  created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE topup_requests (
  id               UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id          UUID          NOT NULL REFERENCES users(id),
  bank_account_id  INT           REFERENCES bank_accounts(id),
  amount           NUMERIC(15,2) NOT NULL CHECK (amount > 0),
  transfer_proof_url TEXT,
  status           topup_status  NOT NULL DEFAULT 'pending',
  reject_reason    TEXT,
  notes            TEXT,
  approved_by      UUID,
  approved_at      TIMESTAMPTZ,
  created_at       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
  updated_at       TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_topup_user    ON topup_requests(user_id, created_at DESC);
CREATE INDEX idx_topup_pending ON topup_requests(status, created_at) WHERE status = 'pending';

CREATE TABLE peer_transfers (
  id            UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  from_user_id  UUID          NOT NULL REFERENCES users(id),
  to_user_id    UUID          NOT NULL REFERENCES users(id),
  amount        NUMERIC(15,2) NOT NULL CHECK (amount > 0),
  notes         TEXT,
  created_at    TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
  CHECK (from_user_id <> to_user_id)
);
CREATE INDEX idx_transfer_from ON peer_transfers(from_user_id, created_at DESC);
CREATE INDEX idx_transfer_to   ON peer_transfers(to_user_id,   created_at DESC);

-- ============================================================
-- MODULE 4: PRODUCT
-- ============================================================

CREATE TABLE product_categories (
  id         SERIAL       PRIMARY KEY,
  name       VARCHAR(100) NOT NULL,
  code       VARCHAR(20)  NOT NULL UNIQUE,  -- PULSA | DATA | PLN | GAME | PPOB | EMONEY
  icon_url   TEXT,
  sort_order SMALLINT     NOT NULL DEFAULT 0,
  is_active  BOOLEAN      NOT NULL DEFAULT TRUE
);

CREATE TABLE products (
  id           UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  category_id  INT           NOT NULL REFERENCES product_categories(id),
  name         VARCHAR(150)  NOT NULL,
  code         VARCHAR(50)   NOT NULL UNIQUE,   -- kode internal, e.g. INDOSAT_PULSA_5000
  denomination NUMERIC(15,2),
  operator     VARCHAR(50),
  description  TEXT,
  is_active    BOOLEAN       NOT NULL DEFAULT TRUE,
  created_at   TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
  updated_at   TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_products_category ON products(category_id, is_active);
CREATE INDEX idx_products_operator ON products(operator);

/*
  HARGA JUAL MENGIKAT PER PRODUK PER LEVEL:
  Setiap produk WAJIB punya harga yang ditetapkan admin untuk setiap level.
  Tidak ada perhitungan otomatis dari markup — harga di sini adalah harga final.

  Contoh untuk produk "Pulsa Indosat 5000":
  ┌─────────────────────────┬────────────┬────────────┐
  │ product_code            │ level_name │ sell_price │
  ├─────────────────────────┼────────────┼────────────┤
  │ INDOSAT_PULSA_5000      │ member1    │ 5600       │
  │ INDOSAT_PULSA_5000      │ member2    │ 5550       │
  │ INDOSAT_PULSA_5000      │ member3    │ 5525       │
  └─────────────────────────┴────────────┴────────────┘
*/
CREATE TABLE product_level_prices (
  id          SERIAL        PRIMARY KEY,
  product_id  UUID          NOT NULL REFERENCES products(id) ON DELETE CASCADE,
  level_id    INT           NOT NULL REFERENCES user_levels(id),
  sell_price  NUMERIC(15,2) NOT NULL CHECK (sell_price > 0),
  is_active   BOOLEAN       NOT NULL DEFAULT TRUE,
  updated_at  TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
  UNIQUE (product_id, level_id)  -- 1 harga per produk per level, tidak boleh duplikat
);
-- Index kritis: dipakai setiap kali order masuk untuk lookup harga
CREATE INDEX idx_prices_lookup ON product_level_prices(product_id, level_id) WHERE is_active = TRUE;

-- ============================================================
-- MODULE 5: SUPPLIER
-- ============================================================

CREATE TABLE suppliers (
  id               SERIAL       PRIMARY KEY,
  name             VARCHAR(100) NOT NULL,
  code             VARCHAR(20)  NOT NULL UNIQUE,
  api_base_url     TEXT,
  api_key_enc      TEXT,        -- enkripsi di app-level
  callback_secret  TEXT,
  timeout_seconds  SMALLINT     NOT NULL DEFAULT 30,
  is_active        BOOLEAN      NOT NULL DEFAULT TRUE,
  created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
  updated_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE supplier_products (
  id                    SERIAL        PRIMARY KEY,
  product_id            UUID          NOT NULL REFERENCES products(id) ON DELETE CASCADE,
  supplier_id           INT           NOT NULL REFERENCES suppliers(id),
  supplier_product_code VARCHAR(100)  NOT NULL,
  supplier_product_name VARCHAR(150),
  cost_price            NUMERIC(15,2) NOT NULL CHECK (cost_price > 0),
  seq                   SMALLINT      NOT NULL,
  is_active             BOOLEAN       NOT NULL DEFAULT TRUE,
  updated_at            TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
  UNIQUE (product_id, supplier_id),
  UNIQUE (product_id, seq)
);
CREATE INDEX idx_supplier_routing ON supplier_products(product_id, seq ASC) WHERE is_active = TRUE;

-- ============================================================
-- MODULE 6: TRANSACTION
-- ============================================================

CREATE TABLE transactions (
  id              UUID               PRIMARY KEY DEFAULT gen_random_uuid(),
  reference_id    VARCHAR(100)       NOT NULL UNIQUE,
  user_id         UUID               NOT NULL REFERENCES users(id),
  product_id      UUID               NOT NULL REFERENCES products(id),
  destination     VARCHAR(100)       NOT NULL,
  sell_price      NUMERIC(15,2)      NOT NULL,   -- snapshot harga saat order (dari product_level_prices)
  cost_price      NUMERIC(15,2),
  profit          NUMERIC(15,2) GENERATED ALWAYS AS (sell_price - COALESCE(cost_price, 0)) STORED,
  status          transaction_status NOT NULL DEFAULT 'pending',
  current_seq     SMALLINT           NOT NULL DEFAULT 1,
  pin_verified_at TIMESTAMPTZ        NOT NULL,
  sn              VARCHAR(255),
  completed_at    TIMESTAMPTZ,
  created_at      TIMESTAMPTZ        NOT NULL DEFAULT NOW(),
  updated_at      TIMESTAMPTZ        NOT NULL DEFAULT NOW(),
  PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

CREATE TABLE transactions_y2026m01 PARTITION OF transactions FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');
CREATE TABLE transactions_y2026m02 PARTITION OF transactions FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');
CREATE TABLE transactions_y2026m03 PARTITION OF transactions FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');
CREATE TABLE transactions_y2026m04 PARTITION OF transactions FOR VALUES FROM ('2026-04-01') TO ('2026-05-01');
CREATE TABLE transactions_y2026m05 PARTITION OF transactions FOR VALUES FROM ('2026-05-01') TO ('2026-06-01');
CREATE TABLE transactions_y2026m06 PARTITION OF transactions FOR VALUES FROM ('2026-06-01') TO ('2026-07-01');
CREATE TABLE transactions_y2026m07 PARTITION OF transactions FOR VALUES FROM ('2026-07-01') TO ('2026-08-01');
CREATE TABLE transactions_y2026m08 PARTITION OF transactions FOR VALUES FROM ('2026-08-01') TO ('2026-09-01');
CREATE TABLE transactions_y2026m09 PARTITION OF transactions FOR VALUES FROM ('2026-09-01') TO ('2026-10-01');
CREATE TABLE transactions_y2026m10 PARTITION OF transactions FOR VALUES FROM ('2026-10-01') TO ('2026-11-01');
CREATE TABLE transactions_y2026m11 PARTITION OF transactions FOR VALUES FROM ('2026-11-01') TO ('2026-12-01');
CREATE TABLE transactions_y2026m12 PARTITION OF transactions FOR VALUES FROM ('2026-12-01') TO ('2027-01-01');

CREATE INDEX idx_trx_user_date  ON transactions(user_id, created_at DESC);
CREATE INDEX idx_trx_ref        ON transactions(reference_id);
CREATE INDEX idx_trx_processing ON transactions(status, created_at)
  WHERE status IN ('pending', 'processing');

CREATE TABLE transaction_attempts (
  id                  BIGSERIAL      PRIMARY KEY,
  transaction_id      UUID           NOT NULL,
  supplier_id         INT            NOT NULL REFERENCES suppliers(id),
  supplier_product_id INT            NOT NULL REFERENCES supplier_products(id),
  seq                 SMALLINT       NOT NULL,
  status              attempt_status NOT NULL DEFAULT 'pending',
  supplier_ref_id     VARCHAR(100),
  supplier_trx_id     VARCHAR(100),
  request_payload     JSONB,
  response_payload    JSONB,
  error_code          VARCHAR(50),
  error_message       TEXT,
  attempted_at        TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
  completed_at        TIMESTAMPTZ,
  UNIQUE (transaction_id, seq)
);
CREATE INDEX idx_attempt_trx_id  ON transaction_attempts(transaction_id);
CREATE INDEX idx_attempt_sup_ref ON transaction_attempts(supplier_ref_id) WHERE supplier_ref_id IS NOT NULL;
CREATE INDEX idx_attempt_active  ON transaction_attempts(status, attempted_at)
  WHERE status IN ('pending', 'processing');

CREATE TABLE supplier_callbacks (
  id            BIGSERIAL   PRIMARY KEY,
  supplier_id   INT         NOT NULL REFERENCES suppliers(id),
  raw_headers   JSONB,
  raw_payload   JSONB       NOT NULL,
  ip_address    INET,
  hmac_valid    BOOLEAN,
  attempt_id    BIGINT      REFERENCES transaction_attempts(id),
  is_processed  BOOLEAN     NOT NULL DEFAULT FALSE,
  processed_at  TIMESTAMPTZ,
  error_message TEXT,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_cb_unprocessed ON supplier_callbacks(created_at) WHERE is_processed = FALSE;

-- ============================================================
-- MODULE 7: IDEMPOTENCY
-- ============================================================

CREATE TABLE idempotency_keys (
  key             VARCHAR(200) NOT NULL,
  user_id         UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  transaction_id  UUID,
  response_cache  JSONB,
  created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
  expires_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW() + INTERVAL '24 hours',
  PRIMARY KEY (user_id, key)
);
CREATE INDEX idx_idempotency_expire ON idempotency_keys(expires_at);

-- ============================================================
-- MODULE 8: NOTIFICATION
-- ============================================================

-- Code contoh: TRX_SUCCESS | TRX_FAILED | TOPUP_APPROVED | TOPUP_REJECTED
--              PIN_RESET | TRANSFER_IN | TRANSFER_OUT | REFERRAL_BONUS_PAID
CREATE TABLE notification_templates (
  id          SERIAL               PRIMARY KEY,
  code        VARCHAR(50)          NOT NULL,
  channel     notification_channel NOT NULL,
  subject     VARCHAR(200),
  body        TEXT                 NOT NULL,
  is_active   BOOLEAN              NOT NULL DEFAULT TRUE,
  UNIQUE (code, channel)
);

CREATE TABLE notification_logs (
  id            BIGSERIAL            NOT NULL,
  user_id       UUID                 NOT NULL REFERENCES users(id),
  channel       notification_channel NOT NULL,
  template_code VARCHAR(50),
  recipient     VARCHAR(100)         NOT NULL,
  subject       VARCHAR(200),
  body          TEXT,
  status        VARCHAR(20)          NOT NULL DEFAULT 'pending',
  ref_type      VARCHAR(50),
  ref_id        UUID,
  sent_at       TIMESTAMPTZ,
  retry_count   SMALLINT             NOT NULL DEFAULT 0,
  error_message TEXT,
  created_at    TIMESTAMPTZ          NOT NULL DEFAULT NOW(),
  PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

CREATE TABLE notification_logs_y2026m01 PARTITION OF notification_logs FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');
CREATE TABLE notification_logs_y2026m02 PARTITION OF notification_logs FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');
CREATE TABLE notification_logs_y2026m03 PARTITION OF notification_logs FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');
CREATE TABLE notification_logs_y2026m04 PARTITION OF notification_logs FOR VALUES FROM ('2026-04-01') TO ('2026-05-01');
CREATE TABLE notification_logs_y2026m05 PARTITION OF notification_logs FOR VALUES FROM ('2026-05-01') TO ('2026-06-01');
CREATE TABLE notification_logs_y2026m06 PARTITION OF notification_logs FOR VALUES FROM ('2026-06-01') TO ('2026-07-01');
CREATE TABLE notification_logs_y2026m07 PARTITION OF notification_logs FOR VALUES FROM ('2026-07-01') TO ('2026-08-01');
CREATE TABLE notification_logs_y2026m08 PARTITION OF notification_logs FOR VALUES FROM ('2026-08-01') TO ('2026-09-01');
CREATE TABLE notification_logs_y2026m09 PARTITION OF notification_logs FOR VALUES FROM ('2026-09-01') TO ('2026-10-01');
CREATE TABLE notification_logs_y2026m10 PARTITION OF notification_logs FOR VALUES FROM ('2026-10-01') TO ('2026-11-01');
CREATE TABLE notification_logs_y2026m11 PARTITION OF notification_logs FOR VALUES FROM ('2026-11-01') TO ('2026-12-01');
CREATE TABLE notification_logs_y2026m12 PARTITION OF notification_logs FOR VALUES FROM ('2026-12-01') TO ('2027-01-01');

CREATE INDEX idx_notif_user    ON notification_logs(user_id, created_at DESC);
CREATE INDEX idx_notif_pending ON notification_logs(status, created_at) WHERE status = 'pending';

-- ============================================================
-- MODULE 9: ADMIN & AUDIT
-- ============================================================

CREATE TABLE admin_users (
  id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
  username      VARCHAR(50)  NOT NULL UNIQUE,
  email         VARCHAR(100) NOT NULL UNIQUE,
  password_hash VARCHAR(255) NOT NULL,
  role          admin_role   NOT NULL DEFAULT 'staff',
  is_active     BOOLEAN      NOT NULL DEFAULT TRUE,
  last_login_at TIMESTAMPTZ,
  created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
  updated_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE audit_logs (
  id          BIGSERIAL    NOT NULL,
  actor_type  VARCHAR(10)  NOT NULL CHECK (actor_type IN ('user', 'admin', 'system')),
  actor_id    UUID         NOT NULL,
  action      VARCHAR(100) NOT NULL,
  entity      VARCHAR(50),
  entity_id   TEXT,
  old_value   JSONB,
  new_value   JSONB,
  ip_address  INET,
  user_agent  TEXT,
  created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
  PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

CREATE TABLE audit_logs_y2026m01 PARTITION OF audit_logs FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');
CREATE TABLE audit_logs_y2026m02 PARTITION OF audit_logs FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');
CREATE TABLE audit_logs_y2026m03 PARTITION OF audit_logs FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');
CREATE TABLE audit_logs_y2026m04 PARTITION OF audit_logs FOR VALUES FROM ('2026-04-01') TO ('2026-05-01');
CREATE TABLE audit_logs_y2026m05 PARTITION OF audit_logs FOR VALUES FROM ('2026-05-01') TO ('2026-06-01');
CREATE TABLE audit_logs_y2026m06 PARTITION OF audit_logs FOR VALUES FROM ('2026-06-01') TO ('2026-07-01');
CREATE TABLE audit_logs_y2026m07 PARTITION OF audit_logs FOR VALUES FROM ('2026-07-01') TO ('2026-08-01');
CREATE TABLE audit_logs_y2026m08 PARTITION OF audit_logs FOR VALUES FROM ('2026-08-01') TO ('2026-09-01');
CREATE TABLE audit_logs_y2026m09 PARTITION OF audit_logs FOR VALUES FROM ('2026-09-01') TO ('2026-10-01');
CREATE TABLE audit_logs_y2026m10 PARTITION OF audit_logs FOR VALUES FROM ('2026-10-01') TO ('2026-11-01');
CREATE TABLE audit_logs_y2026m11 PARTITION OF audit_logs FOR VALUES FROM ('2026-11-01') TO ('2026-12-01');
CREATE TABLE audit_logs_y2026m12 PARTITION OF audit_logs FOR VALUES FROM ('2026-12-01') TO ('2027-01-01');

CREATE INDEX idx_audit_actor  ON audit_logs(actor_id, created_at DESC);
CREATE INDEX idx_audit_entity ON audit_logs(entity, entity_id);

-- ============================================================
-- SEED DATA
-- ============================================================

INSERT INTO user_levels (name, description, markup_type, markup_value, can_transfer) VALUES
  ('member1',   'Member Level 1', 'fixed', 0, TRUE),
  ('member2',   'Member Level 2', 'fixed', 0, TRUE),
  ('member3',   'Member Level 3', 'fixed', 0, TRUE);
-- Catatan: markup_value di user_levels tidak dipakai untuk kalkulasi harga
-- karena harga di-manage langsung di product_level_prices (harga mengikat)

INSERT INTO user_level_configs (level_id, config_key, config_value, description) VALUES
  (1, 'max_daily_topup',      '5000000',  'Maks topup/hari (IDR)'),
  (1, 'max_single_trx',       '1000000',  'Maks 1 transaksi (IDR)'),
  (1, 'max_daily_trx_amount', '3000000',  'Maks total transaksi/hari (IDR)'),
  (1, 'max_transfer_per_day', '2000000',  'Maks transfer/hari (IDR)'),
  (2, 'max_daily_topup',      '10000000', 'Maks topup/hari (IDR)'),
  (2, 'max_single_trx',       '2000000',  'Maks 1 transaksi (IDR)'),
  (2, 'max_daily_trx_amount', '10000000', 'Maks total transaksi/hari (IDR)'),
  (2, 'max_transfer_per_day', '5000000',  'Maks transfer/hari (IDR)'),
  (3, 'max_daily_topup',      '50000000', 'Maks topup/hari (IDR)'),
  (3, 'max_single_trx',       '10000000', 'Maks 1 transaksi (IDR)'),
  (3, 'max_daily_trx_amount', '50000000', 'Maks total transaksi/hari (IDR)'),
  (3, 'max_transfer_per_day', '25000000', 'Maks transfer/hari (IDR)');

INSERT INTO product_categories (name, code, sort_order) VALUES
  ('Pulsa',         'PULSA',  1),
  ('Paket Data',    'DATA',   2),
  ('Token Listrik', 'PLN',    3),
  ('Game Voucher',  'GAME',   4),
  ('PPOB',          'PPOB',   5),
  ('E-Money',       'EMONEY', 6);

INSERT INTO bank_accounts (bank_name, account_number, account_name) VALUES
  ('BCA',     '1234567890', 'PT Pedagang Pulsa Indonesia'),
  ('Mandiri', '0987654321', 'PT Pedagang Pulsa Indonesia');

-- ============================================================
-- STORED FUNCTIONS: MUTASI SALDO (thread-safe, SELECT FOR UPDATE)
-- ============================================================

CREATE OR REPLACE FUNCTION hold_balance(
  p_user_id UUID, p_amount NUMERIC, p_ref_type VARCHAR, p_ref_id UUID, p_notes TEXT DEFAULT NULL
) RETURNS VOID LANGUAGE plpgsql AS $$
DECLARE v_active NUMERIC; v_held NUMERIC;
BEGIN
  SELECT active_balance, held_balance INTO v_active, v_held
    FROM user_balances WHERE user_id = p_user_id FOR UPDATE;
  IF v_active < p_amount THEN
    RAISE EXCEPTION 'Saldo tidak cukup: aktif=%, dibutuhkan=%', v_active, p_amount;
  END IF;
  UPDATE user_balances
     SET active_balance = active_balance - p_amount,
         held_balance   = held_balance   + p_amount, updated_at = NOW()
   WHERE user_id = p_user_id;
  INSERT INTO balance_ledger
    (user_id, type, amount, active_before, active_after, held_before, held_after, ref_type, ref_id, notes)
  VALUES (p_user_id, 'purchase_hold', p_amount,
          v_active, v_active - p_amount, v_held, v_held + p_amount,
          p_ref_type, p_ref_id, p_notes);
END; $$;

CREATE OR REPLACE FUNCTION debit_held_balance(
  p_user_id UUID, p_amount NUMERIC, p_ref_type VARCHAR, p_ref_id UUID, p_notes TEXT DEFAULT NULL
) RETURNS VOID LANGUAGE plpgsql AS $$
DECLARE v_active NUMERIC; v_held NUMERIC;
BEGIN
  SELECT active_balance, held_balance INTO v_active, v_held
    FROM user_balances WHERE user_id = p_user_id FOR UPDATE;
  IF v_held < p_amount THEN
    RAISE EXCEPTION 'Held balance tidak cukup: held=%, dibutuhkan=%', v_held, p_amount;
  END IF;
  UPDATE user_balances
     SET held_balance = held_balance - p_amount, updated_at = NOW()
   WHERE user_id = p_user_id;
  INSERT INTO balance_ledger
    (user_id, type, amount, active_before, active_after, held_before, held_after, ref_type, ref_id, notes)
  VALUES (p_user_id, 'purchase_debit', p_amount,
          v_active, v_active, v_held, v_held - p_amount,
          p_ref_type, p_ref_id, p_notes);
END; $$;

CREATE OR REPLACE FUNCTION release_held_balance(
  p_user_id UUID, p_amount NUMERIC, p_ref_type VARCHAR, p_ref_id UUID, p_notes TEXT DEFAULT NULL
) RETURNS VOID LANGUAGE plpgsql AS $$
DECLARE v_active NUMERIC; v_held NUMERIC;
BEGIN
  SELECT active_balance, held_balance INTO v_active, v_held
    FROM user_balances WHERE user_id = p_user_id FOR UPDATE;
  UPDATE user_balances
     SET held_balance   = held_balance   - p_amount,
         active_balance = active_balance + p_amount, updated_at = NOW()
   WHERE user_id = p_user_id;
  INSERT INTO balance_ledger
    (user_id, type, amount, active_before, active_after, held_before, held_after, ref_type, ref_id, notes)
  VALUES (p_user_id, 'purchase_release', p_amount,
          v_active, v_active + p_amount, v_held, v_held - p_amount,
          p_ref_type, p_ref_id, p_notes);
END; $$;

-- ============================================================
-- QUERY REFERENSI
-- ============================================================

-- [1] Lookup harga jual untuk user tertentu saat order masuk
-- SELECT plp.sell_price
-- FROM product_level_prices plp
-- JOIN users u ON u.level_id = plp.level_id
-- WHERE plp.product_id = $1 AND u.id = $2 AND plp.is_active = TRUE;

-- [2] Laporan referral: siapa saja yang diundang user X + status bonus
-- SELECT u_ref.username AS referrer, u_new.username AS referee,
--        u_new.created_at AS register_at, rl.bonus_status, rl.bonus_amount, rl.paid_at
-- FROM referral_logs rl
-- JOIN users u_ref ON u_ref.id = rl.referrer_id
-- JOIN users u_new ON u_new.id = rl.referee_id
-- WHERE rl.referrer_id = $1
-- ORDER BY u_new.created_at DESC;

-- [3] Daftar bonus referral pending (untuk admin)
-- SELECT u_ref.username AS pengundang, u_ref.phone,
--        COUNT(rl.id) AS total_undangan,
--        COUNT(*) FILTER (WHERE rl.bonus_status = 'pending') AS pending_bonus
-- FROM referral_logs rl
-- JOIN users u_ref ON u_ref.id = rl.referrer_id
-- WHERE rl.bonus_status = 'pending'
-- GROUP BY u_ref.id, u_ref.username, u_ref.phone
-- ORDER BY pending_bonus DESC;

-- [4] Cek apakah user boleh transfer
-- SELECT COALESCE(u.can_transfer_override, ul.can_transfer) AS can_transfer
-- FROM users u JOIN user_levels ul ON u.level_id = ul.id WHERE u.id = $1;

-- [5] Dashboard realtime: transaksi per menit hari ini
-- SELECT date_trunc('minute', created_at) AS menit,
--        COUNT(*) AS total,
--        COUNT(*) FILTER (WHERE status='success') AS sukses,
--        SUM(sell_price) FILTER (WHERE status='success') AS omzet
-- FROM transactions WHERE created_at >= CURRENT_DATE
-- GROUP BY 1 ORDER BY 1 DESC;

-- [6] Success rate per supplier hari ini
-- SELECT s.name, COUNT(*) AS attempts,
--        ROUND(COUNT(*) FILTER (WHERE ta.status='success') * 100.0 / COUNT(*), 2) AS success_pct
-- FROM transaction_attempts ta JOIN suppliers s ON s.id = ta.supplier_id
-- WHERE ta.attempted_at >= CURRENT_DATE
-- GROUP BY s.name ORDER BY success_pct DESC;
