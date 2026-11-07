using CoffeeShop.Application.Common.Security;
using CoffeeShop.WinUI.Security;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI;

internal static class WinUIApiServiceCollectionExtensions
{
    public static IServiceCollection AddCoffeeShopApiClient(this IServiceCollection services, Uri apiBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(new HttpClient { BaseAddress = apiBaseAddress, Timeout = TimeSpan.FromSeconds(15) });
        services.AddSingleton<IStaffIdentityApi, StaffIdentityApiClient>();
        services.AddSingleton<IRefreshTokenStore>(_ => new ProtectedRefreshTokenFileStore(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoffeeShop", "refresh-token.bin"),
            new WindowsDpapiSecretProtector()));
        services.AddSingleton<StaffSessionManager>();
        services.AddSingleton<AuthenticatedApiClient>();
        services.AddSingleton<ApiReportsSource>();
        services.AddSingleton<PosOperatorSession>(provider => new PosOperatorSession(
            () => provider.GetRequiredService<StaffSessionManager>().CurrentUser));
        services.AddSingleton<ICurrentUser>(provider => provider.GetRequiredService<PosOperatorSession>());
        return services;
    }
}
