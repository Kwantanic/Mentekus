namespace Mentekus.Api.Features.Expertise;

internal static class EmbeddingSize
{
    public static void Require(float[] embedding)
    {
        if (embedding.Length != ExpertiseSql.EmbeddingDimension)
            throw new ArgumentException(
                $"Embedding must be {ExpertiseSql.EmbeddingDimension}-dimensional (got {embedding.Length}).",
                nameof(embedding));
    }
}
