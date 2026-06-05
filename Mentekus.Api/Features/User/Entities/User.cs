using Pgvector;

namespace Mentekus.Api.Features.User.Entities;

public class User
{
    public Guid Id { get; set; }
    public string? ExternalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool ProfileVisible { get; set; } = true;
    public bool AllowRouting { get; set; } = true;
    public Vector? ExpertiseEmbedding { get; set; }
}