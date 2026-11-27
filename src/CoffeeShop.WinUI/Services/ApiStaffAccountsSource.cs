using CoffeeShop.Contracts;

namespace CoffeeShop.WinUI.Services;

public sealed class ApiStaffAccountsSource(AuthenticatedApiClient client)
{
    public Task<StaffAccountResponse[]> SearchAsync(string? search, CancellationToken cancellationToken = default) =>
        client.GetAsync<StaffAccountResponse[]>($"users?search={Uri.EscapeDataString(search?.Trim() ?? string.Empty)}&limit=100",
            cancellationToken);

    public Task<StaffAccountResponse> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken = default) =>
        client.PostAsync<CreateStaffRequest, StaffAccountResponse>("users", request, cancellationToken);

    public Task<StaffAccountResponse> UpdateAsync(Guid id, UpdateStaffRequest request, CancellationToken cancellationToken = default) =>
        client.PutAsync<UpdateStaffRequest, StaffAccountResponse>($"users/{id:D}", request, cancellationToken);

    public Task<StaffAccountResponse> DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        client.PostAsync<DeactivateStaffRequest, StaffAccountResponse>(
            $"users/{id:D}/deactivate", new DeactivateStaffRequest(true), cancellationToken);

    public Task<StaffAccountResponse> ReactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        client.PostAsync<ReactivateStaffRequest, StaffAccountResponse>(
            $"users/{id:D}/reactivate", new ReactivateStaffRequest(true), cancellationToken);
}
