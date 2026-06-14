-- Enable citext extension
CREATE EXTENSION IF NOT EXISTS citext;

ALTER TABLE Users
    DROP CONSTRAINT IF EXISTS users_email_key;

ALTER TABLE Users
    ALTER COLUMN Email TYPE citext;

ALTER TABLE Users
    ADD CONSTRAINT uq_users_email UNIQUE (Email);
