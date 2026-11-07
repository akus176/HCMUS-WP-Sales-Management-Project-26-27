namespace CoffeeShop.Contracts;

public sealed record LoginRequest(string? Username, string? Password);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record StaffSession(Guid Id, string FullName, string[] Roles, string[] Permissions);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, StaffSession User);

public sealed record DeactivateStaffRequest(bool Confirm);
public sealed record StaffAccountResponse(
    Guid Id,
    string Username,
    string FullName,
    string Email,
    string? Phone,
    bool IsActive,
    string[] Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeactivatedAt);
public sealed record SecurityEventResponse(
    Guid Id,
    string EventType,
    DateTimeOffset OccurredAt,
    string Outcome,
    Guid? UserId,
    string? Username,
    string? IpAddress);
