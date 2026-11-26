using CoffeeShop.Contracts;

namespace CoffeeShop.WinUI.Services;

public sealed class ApiStaffAccountsSource(AuthenticatedApiClient client)
{
    public Task<StaffAccountResponse[]> SearchAsync(string? search, CancellationToken cancellationToken = default) =>
        client.GetAsync<StaffAccountResponse[]>($"users?search={Uri.EscapeDataString(search?.Trim() ?? string.Empty)}&limit=100",
            cancellationToken);

    public Task<StaffAccountResponse> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken = default) =>
        client.PostAsync<CreateStaffRequest, StaffAccountResponse>("users", request, cancellationToken);
}
