namespace CoffeeShop.Application.Common.Security;

using CoffeeShop.Domain.Common;

public interface ICurrentUser
{
    EntityId? Id { get; }

    string? Username { get; }

    bool IsAuthenticated { get; }

    IReadOnlyCollection<string> Roles { get; }

    IReadOnlyCollection<string> Permissions { get; }

    bool HasPermission(string permission);

    bool IsInRole(string role);

    EntityId GetRequiredId();
}
