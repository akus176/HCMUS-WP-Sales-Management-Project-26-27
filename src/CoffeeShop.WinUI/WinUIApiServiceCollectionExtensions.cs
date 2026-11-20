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
        services.AddSingleton(provider => new ApiAssistantSource(apiBaseAddress, provider.GetRequiredService<StaffSessionManager>()));
        services.AddSingleton<ApiPosSource>();
        services.AddSingleton<ApiOperationsSource>();
        services.AddSingleton<ApiInventorySource>();
        services.AddSingleton<ApiCatalogWorkspaceSource>();
        services.AddSingleton<ICatalogWorkspaceSource>(provider => provider.GetRequiredService<ApiCatalogWorkspaceSource>());
        services.AddSingleton<ApiCustomerWorkspaceSource>();
        services.AddSingleton<ICustomerWorkspaceSource>(provider => provider.GetRequiredService<ApiCustomerWorkspaceSource>());
        services.AddSingleton<CustomerOrderContext>();
        services.AddSingleton<CustomerWorkspace>(provider => new CustomerWorkspace(
            provider.GetRequiredService<ICustomerWorkspaceSource>(), provider.GetRequiredService<CustomerOrderContext>()));
        services.AddSingleton<PosOperatorSession>(provider => new PosOperatorSession(
            () => provider.GetRequiredService<StaffSessionManager>().CurrentUser));
        services.AddSingleton<ICurrentUser>(provider => provider.GetRequiredService<PosOperatorSession>());
        return services;
    }
}
