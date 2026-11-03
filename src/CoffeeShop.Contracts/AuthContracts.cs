namespace CoffeeShop.Contracts;

public sealed record LoginRequest(string? Username, string? Password);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record StaffSession(Guid Id, string FullName, string[] Roles, string[] Permissions);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, StaffSession User);
