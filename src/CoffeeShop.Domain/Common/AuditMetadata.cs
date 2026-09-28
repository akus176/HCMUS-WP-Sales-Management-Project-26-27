namespace CoffeeShop.Domain.Common;

public sealed record AuditMetadata
{
    public AuditMetadata(
        UtcTimestamp createdAt,
        EntityId createdBy,
        UtcTimestamp? updatedAt = null,
        EntityId? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(createdBy);

        if (updatedAt is not null && updatedBy is null)
        {
            throw new ArgumentException("An updated actor is required when an updated timestamp is provided.", nameof(updatedBy));
        }

        if (updatedBy is not null && updatedAt is null)
        {
            throw new ArgumentException("An updated timestamp is required when an updated actor is provided.", nameof(updatedAt));
        }

        if (updatedAt is not null && updatedAt.Value < createdAt.Value)
        {
            throw new ArgumentException("The updated timestamp cannot be before the created timestamp.", nameof(updatedAt));
        }

        CreatedAt = createdAt;
        CreatedBy = createdBy;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    public UtcTimestamp CreatedAt { get; }

    public EntityId CreatedBy { get; }

    public UtcTimestamp? UpdatedAt { get; }

    public EntityId? UpdatedBy { get; }
}
