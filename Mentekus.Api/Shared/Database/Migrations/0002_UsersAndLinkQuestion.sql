CREATE TABLE IF NOT EXISTS Users
(
    Id                 UUID PRIMARY KEY,
    Name               TEXT        NOT NULL,
    Email              TEXT UNIQUE NOT NULL,
    CreatedAt          TIMESTAMPTZ DEFAULT now(),
    ProfileVisible     BOOLEAN     DEFAULT true,
    AllowRouting       BOOLEAN     DEFAULT true,
    ExpertiseEmbedding VECTOR(1024)
);

ALTER TABLE Questions
    ADD COLUMN IF NOT EXISTS AskedByUserId UUID REFERENCES Users (Id);