namespace CoffeeShop.Domain.Identity;

using CoffeeShop.Domain.Common;

public class SecurityAuditEvent
{
    // Parameterless constructor for EF Core
    protected SecurityAuditEvent()
    {
    }

    public SecurityAuditEvent(
        EntityId id,
        string eventType,
        UtcTimestamp occurredAt,
        string outcome,
        EntityId? userId = null,
        string? ipAddress = null,
        string? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(occurredAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        Id = id;
        EventType = eventType.Trim();
        OccurredAt = occurredAt;
        Outcome = outcome.Trim();
        UserId = userId;
        IpAddress = ipAddress?.Trim();
        Metadata = metadata;
    }

    public EntityId Id { get; protected set; } = null!;

    public string EventType { get; protected set; } = string.Empty;

    public EntityId? UserId { get; protected set; }

    public User? User { get; protected set; }

    public UtcTimestamp OccurredAt { get; protected set; } = null!;

    public string Outcome { get; protected set; } = string.Empty;

    public string? IpAddress { get; protected set; }

    public string? Metadata { get; protected set; }
}
