using CoffeeShop.Contracts;

namespace CoffeeShop.WinUI.Services;

public interface IStaffIdentityApi
{
    Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, string accessToken, CancellationToken cancellationToken = default);
}
