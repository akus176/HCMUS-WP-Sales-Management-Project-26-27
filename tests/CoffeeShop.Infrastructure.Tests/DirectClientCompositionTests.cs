namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.Application.Sales;
using CoffeeShop.Infrastructure.Postgres.Inventory;
using CoffeeShop.WinUI;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

public sealed class DirectClientCompositionTests
{
    [Fact]
    public void ActualClientRegistration_ResolvesAllDemoWorkflowsWithoutConnecting()
    {
        using var services = new ServiceCollection()
            .AddCoffeeShopDirectClient("Host=127.0.0.1;Database=composition_only;Username=unused")
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        Assert.IsType<InventoryRepository>(provider.GetRequiredService<IInventoryRepository>());
        Assert.NotNull(provider.GetRequiredService<InventoryCommandHandler>());
        Assert.NotNull(provider.GetRequiredService<IStockMovementReader>());
        Assert.NotNull(provider.GetRequiredService<InventoryAuditWorkspace>());
        Assert.NotNull(provider.GetRequiredService<RevenueReportQueryHandler>());
        Assert.NotNull(provider.GetRequiredService<BestSellerReportQueryHandler>());
        Assert.NotNull(provider.GetRequiredService<OrderChannelReportQueryHandler>());
        Assert.NotNull(provider.GetRequiredService<OrderFulfillmentCommandHandler>());
        Assert.NotNull(provider.GetRequiredService<OrderCancellationCommandHandler>());
        Assert.NotNull(provider.GetRequiredService<DeliveryCommandHandler>());
        Assert.NotNull(provider.GetRequiredService<CustomerWorkspace>());
        Assert.NotNull(provider.GetRequiredService<PosWorkspace>());
        Assert.NotNull(provider.GetRequiredService<OperationsWorkspace>());
    }
}
