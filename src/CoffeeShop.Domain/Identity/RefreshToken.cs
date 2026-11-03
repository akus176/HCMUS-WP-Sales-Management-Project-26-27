namespace CoffeeShop.Domain.Identity;

using CoffeeShop.Domain.Common;

public class RefreshToken
{
    // Parameterless constructor for EF Core
    protected RefreshToken()
    {
    }

    public RefreshToken(
        EntityId id,
        EntityId userId,
        string tokenHash,
        Guid familyId,
        UtcTimestamp createdAt,
        UtcTimestamp expiresAt,
        UtcTimestamp? revokedAt = null,
        EntityId? replacedById = null,
        string? createdByIp = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (familyId == Guid.Empty)
        {
            throw new ArgumentException("Family identifier cannot be empty.", nameof(familyId));
        }

        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(expiresAt);

        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
        ReplacedById = replacedById;
        CreatedByIp = createdByIp?.Trim();
    }

    public EntityId Id { get; protected set; } = null!;

    public EntityId UserId { get; protected set; } = null!;

    public User? User { get; protected set; }

    public string TokenHash { get; protected set; } = string.Empty;

    public Guid FamilyId { get; protected set; }

    public UtcTimestamp CreatedAt { get; protected set; } = null!;

    public UtcTimestamp ExpiresAt { get; protected set; } = null!;

    public UtcTimestamp? RevokedAt { get; protected set; }

    public EntityId? ReplacedById { get; protected set; }

    public RefreshToken? ReplacedBy { get; protected set; }

    public string? CreatedByIp { get; protected set; }

    public bool IsActive => RevokedAt is null && ExpiresAt.Value > DateTimeOffset.UtcNow;

    public void Revoke(UtcTimestamp now, EntityId? replacement = null)
    {
        RevokedAt ??= now;
        ReplacedById ??= replacement;
    }
}
