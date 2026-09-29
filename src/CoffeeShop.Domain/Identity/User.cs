namespace CoffeeShop.Domain.Identity;

using CoffeeShop.Domain.Common;

public class User
{
    private readonly List<UserRole> _userRoles = [];
    private readonly List<RefreshToken> _refreshTokens = [];

    // Parameterless constructor for EF Core
    protected User()
    {
    }

    public User(
        EntityId id,
        string username,
        string email,
        string passwordHash,
        string fullName,
        string? phone,
        UtcTimestamp createdAt,
        UtcTimestamp updatedAt,
        bool isActive = true,
        UtcTimestamp? deactivatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(updatedAt);

        Id = id;
        Username = username.Trim();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        Phone = phone?.Trim();
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DeactivatedAt = deactivatedAt;
    }

    public EntityId Id { get; protected set; } = null!;

    public string Username { get; protected set; } = string.Empty;

    public string Email { get; protected set; } = string.Empty;

    public string PasswordHash { get; protected set; } = string.Empty;

    public string FullName { get; protected set; } = string.Empty;

    public string? Phone { get; protected set; }

    public bool IsActive { get; protected set; } = true;

    public UtcTimestamp CreatedAt { get; protected set; } = null!;

    public UtcTimestamp UpdatedAt { get; protected set; } = null!;

    public UtcTimestamp? DeactivatedAt { get; protected set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();
}
