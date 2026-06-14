-- PR4: Topics + UserTopicExpertise graph; add optional profile columns to Users
-- Additive and idempotent

ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS ExpertiseSummary TEXT;

ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS LastExpertiseUpdate TIMESTAMPTZ;

CREATE TABLE IF NOT EXISTS Topics (
    Id UUID PRIMARY KEY,
    Name citext UNIQUE NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS UserTopicExpertise (
    UserId UUID NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    TopicId UUID NOT NULL REFERENCES Topics(Id) ON DELETE CASCADE,
    Strength REAL NOT NULL DEFAULT 0,
    LastUpdated TIMESTAMPTZ DEFAULT now(),
    PRIMARY KEY (UserId, TopicId)
);

-- Helpful indexes (additive)
CREATE INDEX IF NOT EXISTS idx_user_topic_strength ON UserTopicExpertise (UserId, Strength DESC);
CREATE INDEX IF NOT EXISTS idx_topics_name ON Topics (Name);