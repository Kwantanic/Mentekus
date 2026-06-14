using Pgvector;

namespace Mentekus.Api.Features.Expertise.Entities;

internal record UserEmbeddingRow(
    Vector? ExpertiseEmbedding,
    DateTime? LastExpertiseUpdate);
