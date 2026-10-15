using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;

namespace CoffeeShop.WinUI.ViewModels;

// Midterm actor selection, not a login. The final API must replace this context.
public sealed class PosOperatorSession : ICurrentUser
{
    public PosStaffOption? Operator { get; set; }
    public EntityId? Id => Operator?.Id;
    public string? Username => Operator?.FullName;
    public bool IsAuthenticated => Id is not null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => IsAuthenticated ? ["discounts.request"] : [];
    public bool HasPermission(string permission) => Permissions.Contains(permission);
    public bool IsInRole(string role) => false;
    public EntityId GetRequiredId() => Id ?? throw new UnauthorizedAccessException("Select a POS operator.");
}
