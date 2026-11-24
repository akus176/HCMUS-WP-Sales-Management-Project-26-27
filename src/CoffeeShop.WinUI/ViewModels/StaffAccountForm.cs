using System.Net.Mail;
using CoffeeShop.Contracts;

namespace CoffeeShop.WinUI.ViewModels;

public static class StaffAccountForm
{
    public static IReadOnlyList<string> Roles { get; } =
        ["Admin", "Manager", "Cashier", "Barista", "InventoryStaff", "DeliveryStaff"];

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
}
