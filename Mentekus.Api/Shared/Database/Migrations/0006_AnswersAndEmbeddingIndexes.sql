-- Question embeddings must match the 1024-dimension expertise vectors.
-- A single mismatched row makes every <=> comparison fail.
DELETE FROM Questions
WHERE Embedding IS NOT NULL
  AND vector_dims(Embedding) <> 1024;

ALTER TABLE Questions
    ALTER COLUMN Embedding TYPE vector(1024)
    USING Embedding::vector(1024);

CREATE TABLE IF NOT EXISTS Answers
(
    Id               UUID PRIMARY KEY,
    QuestionId       UUID        NOT NULL REFERENCES Questions (Id) ON DELETE CASCADE,
    Text             TEXT        NOT NULL,
    Embedding        vector(1024) NOT NULL,
    CreatedAt        TIMESTAMPTZ NOT NULL,
    AnsweredByUserId UUID        NOT NULL REFERENCES Users (Id)
);

CREATE INDEX IF NOT EXISTS idx_answers_question_created
    ON Answers (QuestionId, CreatedAt);

CREATE INDEX IF NOT EXISTS idx_questions_embedding_cosine
    ON Questions USING hnsw (Embedding vector_cosine_ops)
    WHERE Embedding IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_users_expertise_embedding_cosine
    ON Users USING hnsw (ExpertiseEmbedding vector_cosine_ops)
    WHERE AllowRouting = true
      AND ExpertiseEmbedding IS NOT NULL;
