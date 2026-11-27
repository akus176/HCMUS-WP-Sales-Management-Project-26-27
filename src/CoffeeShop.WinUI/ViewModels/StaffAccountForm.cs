using System.Net.Mail;
using CoffeeShop.Contracts;

namespace CoffeeShop.WinUI.ViewModels;

public static class StaffAccountForm
{
    public static IReadOnlyList<string> Roles { get; } =
        ["Admin", "Manager", "Cashier", "Barista", "InventoryStaff", "DeliveryStaff"];

    public static IReadOnlyList<StaffRoleOption> RoleOptions { get; } =
    [
        new("Admin", "Administrator"),
        new("Manager", "Manager"),
        new("Cashier", "Cashier"),
        new("Barista", "Barista"),
        new("InventoryStaff", "Inventory staff"),
        new("DeliveryStaff", "Delivery staff"),
    ];

    public static string? Validate(CreateStaffRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 150)
            return "Enter a full name of up to 150 characters.";
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length > 50)
            return "Enter a username of up to 50 characters.";
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 255 ||
            !MailAddress.TryCreate(email, out var address) ||
            !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
            return "Enter a valid email address.";
        if (request.Phone?.Trim().Length > 30)
            return "Enter a phone number of up to 30 characters.";
        if (request.Role is null || !Roles.Contains(request.Role))
            return "Select an operational role.";
        if (request.Password is null || request.Password.Length is < 12 or > 1024)
            return "Enter an initial password of 12 to 1024 characters.";
        return null;
    }

    public static string? Validate(UpdateStaffRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 150)
            return "Enter a full name of up to 150 characters.";
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length > 50)
            return "Enter a username of up to 50 characters.";
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 255 ||
            !MailAddress.TryCreate(email, out var address) ||
            !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
            return "Enter a valid email address.";
        if (request.Phone?.Trim().Length > 30)
            return "Enter a phone number of up to 30 characters.";
        if (request.Roles is null || request.Roles.Length is 0 or > 6 ||
            request.Roles.Any(role => !Roles.Contains(role, StringComparer.Ordinal)) ||
            request.Roles.Distinct(StringComparer.Ordinal).Count() != request.Roles.Length)
            return "Select one or more unique operational roles.";
        return null;
    }
}

public sealed class StaffRoleOption(string code, string label, bool isSelected = false)
{
    public string Code { get; } = code;
    public string Label { get; } = label;
    public bool IsSelected { get; set; } = isSelected;
}
