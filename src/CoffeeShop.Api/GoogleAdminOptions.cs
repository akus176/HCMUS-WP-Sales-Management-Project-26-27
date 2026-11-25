namespace CoffeeShop.Api;

using System.Net.Mail;

public sealed class GoogleAdminOptions
{
    public bool Enabled { get; init; }
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public GoogleAdminLink[] Links { get; init; } = [];

    public static GoogleAdminOptions Load(IConfiguration configuration)
    {
        var options = configuration.GetSection("GoogleAdmin").Get<GoogleAdminOptions>() ?? new();
        if (options.Enabled && (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret) ||
            options.Links.Length == 0 || options.Links.Any(link => link.UserId == Guid.Empty ||
                string.IsNullOrWhiteSpace(link.Subject) || link.Subject.Length > 255 ||
                !MailAddress.TryCreate(link.Email, out var address) || address.Address != link.Email) ||
            options.Links.Select(link => link.UserId).Distinct().Count() != options.Links.Length ||
            options.Links.Select(link => link.Subject).Distinct(StringComparer.Ordinal).Count() != options.Links.Length))
            throw new InvalidOperationException("Configure the Google desktop client and unique administrator links in private API configuration.");
        return options;
    }

    public string? SessionBinding(Guid userId, string email)
    {
        if (!Enabled) return null;
        var link = Links.SingleOrDefault(item => item.UserId == userId &&
            string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
        return link is null ? null : StaffIdentityService.HashToken($"{ClientId}\n{link.Subject}\n{email.ToLowerInvariant()}");
    }
}

public sealed class GoogleAdminLink
{
    public Guid UserId { get; init; }
    public string Subject { get; init; } = "";
    public string Email { get; init; } = "";
}
