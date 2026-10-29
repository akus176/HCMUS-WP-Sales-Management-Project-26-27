using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Tests;

public sealed class ReportsWorkspaceTests
{
    [Fact]
    public async Task FailedRegion_DoesNotPreventOtherRegionsOrRetry()
    {
        var reads = new ReportReads();
        using var services = new ServiceCollection()
            .AddSingleton<IRevenueReportRepository>(reads)
            .AddSingleton<IInventoryRepository, FakeInventoryRepository>()
            .AddScoped<RevenueReportQueryHandler>().AddScoped<BestSellerReportQueryHandler>()
            .AddScoped<OrderChannelReportQueryHandler>().BuildServiceProvider();
        var model = new ReportsWorkspace(services.GetRequiredService<IServiceScopeFactory>());
        var date = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

        await model.LoadAsync(date, date, TimeZoneInfo.Utc);

        Assert.NotNull(model.Revenue.Error);
        Assert.Null(model.Revenue.Value);
        Assert.DoesNotContain("private", model.Revenue.Error.Description);
        Assert.Empty(model.BestSellers.Value!.Items);
        Assert.Equal(3, model.Channels.Value!.ByChannel.Count);
        Assert.Empty(model.LowStock.Value!);
        Assert.False(model.IsBusy);

        await model.LoadAsync(date.AddDays(1), date.AddDays(1), TimeZoneInfo.Utc);

        Assert.Null(model.Revenue.Error);
        Assert.Equal(0m, model.Revenue.Value!.Revenue);
        Assert.Equal(DateOnly.FromDateTime(date.AddDays(1).Date), model.FirstDate);
        Assert.Equal(WorkflowFeedbackKind.Success, model.Feedback.Kind);
        await model.LoadAsync(date.AddDays(2), date, TimeZoneInfo.Utc);
        Assert.Equal(WorkflowFeedbackKind.Validation, model.Feedback.Kind);
        Assert.Equal(DateOnly.FromDateTime(date.AddDays(1).Date), model.FirstDate);
    }

    [Fact]
    public async Task NewPeriod_ClearsPreviousValuesBeforeQueryCompletes()
    {
        var region = new ReportRegion<string>();
        await region.LoadAsync(() => Task.FromResult(Result<string>.Success("Previous period")));
        var gate = new TaskCompletionSource<Result<string>>();
        var loading = region.LoadAsync(() => gate.Task);
        Assert.Null(region.Value);
        Assert.False(region.IsLoaded);
        gate.SetResult(Result<string>.Failure(Error.Validation("invalid", "Choose another period.")));
        await loading;
        Assert.True(region.IsLoaded);
        Assert.NotNull(region.Error);
        Assert.Null(region.Value);
    }

    [Fact]
    public async Task NewPeriod_ClearsEveryRegionBeforePublishingItsDate()
    {
        var reads = new ReportReads();
        using var services = new ServiceCollection()
            .AddSingleton<IRevenueReportRepository>(reads)
            .AddSingleton<IInventoryRepository, FakeInventoryRepository>()
            .AddScoped<RevenueReportQueryHandler>().AddScoped<BestSellerReportQueryHandler>()
            .AddScoped<OrderChannelReportQueryHandler>().BuildServiceProvider();
        var model = new ReportsWorkspace(services.GetRequiredService<IServiceScopeFactory>());
        var date = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        await model.LoadAsync(date, date, TimeZoneInfo.Utc);
        await model.LoadAsync(date, date, TimeZoneInfo.Utc);
        Assert.NotNull(model.Revenue.Value);
        Assert.NotNull(model.Channels.Value);
        reads.Gate = new();

        var loading = model.LoadAsync(date.AddDays(1), date.AddDays(1), TimeZoneInfo.Utc);

        Assert.True(model.IsBusy);
        Assert.Equal(DateOnly.FromDateTime(date.AddDays(1).Date), model.FirstDate);
        Assert.Null(model.Revenue.Value);
        Assert.Null(model.BestSellers.Value);
        Assert.Null(model.Channels.Value);
        Assert.Null(model.LowStock.Value);
        reads.Gate.SetResult([]);
        await loading;
        Assert.False(model.IsBusy);
        Assert.Equal(WorkflowFeedbackKind.Success, model.Feedback.Kind);
    }

    private sealed class ReportReads : IRevenueReportRepository
    {
        private int calls;
        public TaskCompletionSource<IReadOnlyList<RevenueOrderCandidate>>? Gate { get; set; }
        public Task<IReadOnlyList<RevenueOrderCandidate>> LoadCandidatesAsync(RevenuePeriod period, CancellationToken cancellationToken = default)
        {
            if (++calls == 1) throw new InvalidOperationException("private connection details");
            return Gate?.Task ?? Task.FromResult<IReadOnlyList<RevenueOrderCandidate>>([]);
        }
    }
}
