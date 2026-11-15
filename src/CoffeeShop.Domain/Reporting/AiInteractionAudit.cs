namespace CoffeeShop.Domain.Reporting;

using CoffeeShop.Domain.Common;

public sealed class AiInteractionAudit
{
    private AiInteractionAudit() { }

    public AiInteractionAudit(EntityId id, EntityId userId, UtcTimestamp occurredAt,
        string question, string tools, string outcome, long durationMs)
    {
        Id = id;
        UserId = userId;
        OccurredAt = occurredAt;
        Question = question;
        Tools = tools;
        Outcome = outcome;
        DurationMs = durationMs;
    }

    public EntityId Id { get; private set; } = null!;
    public EntityId UserId { get; private set; } = null!;
    public UtcTimestamp OccurredAt { get; private set; } = null!;
    public string Question { get; private set; } = string.Empty;
    public string Tools { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public long DurationMs { get; private set; }
}
