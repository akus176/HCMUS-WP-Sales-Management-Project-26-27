using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Sales;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;

namespace CoffeeShop.WinUI.ViewModels;

// Midterm actor selection, not a login. The final API must replace this context.
public sealed class PosOperatorSession : ICurrentUser
{
    private readonly Func<StaffSession?>? currentStaff;
    private bool operationalWorkflow;

    public PosOperatorSession() { }
    public PosOperatorSession(Func<StaffSession?> currentStaff) => this.currentStaff = currentStaff;

    public PosStaffOption? Operator { get; set; }
    public StaffSession? Staff => currentStaff?.Invoke();
    public EntityId? Id => Staff is { } user ? new EntityId(user.Id) : Operator?.Id;
    public string? Username => Staff?.FullName ?? Operator?.FullName;
    public bool IsAuthenticated => Staff is not null || Operator is not null;
    public IReadOnlyCollection<string> Roles => Staff?.Roles ?? [];
    public IReadOnlyCollection<string> Permissions => Staff?.Permissions ?? (IsAuthenticated
        ? operationalWorkflow
            ? ["discounts.request", "discounts.approve", "orders.cancel", "orders.fulfill"]
            : ["discounts.request"]
        : []);
    public bool HasPermission(string permission) => Permissions.Contains(permission);
    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public EntityId GetRequiredId() => Id ?? throw new UnauthorizedAccessException("Select a POS operator.");

    // Retained for local/demo workflows; an API session always uses its server-issued grants.
    public void EnableOperationalWorkflow() => operationalWorkflow = true;
}
