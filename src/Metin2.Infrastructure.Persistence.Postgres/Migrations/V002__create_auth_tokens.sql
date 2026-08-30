CREATE TABLE IF NOT EXISTS auth_tokens (
    token BIGINT PRIMARY KEY,
    account_id BIGINT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
    username_normalized VARCHAR(30) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT ck_auth_tokens_token_uint32 CHECK (token BETWEEN 1 AND 4294967295)
);

CREATE INDEX IF NOT EXISTS ix_auth_tokens_account_id
    ON auth_tokens (account_id);

CREATE INDEX IF NOT EXISTS ix_auth_tokens_expires_at
    ON auth_tokens (expires_at);
