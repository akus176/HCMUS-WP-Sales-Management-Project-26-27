using System.ComponentModel;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.ViewModels;

public sealed class ReportsWorkspace : INotifyPropertyChanged
{
    private readonly IReportsSource source;

    public ReportsWorkspace(IServiceScopeFactory scopes) : this(new DirectReportsSource(scopes)) { }
    public ReportsWorkspace(IReportsSource source) => this.source = source;
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsBusy { get; private set; }
    public DateOnly FirstDate { get; private set; }
    public DateOnly LastDate { get; private set; }
    public ReportRegion<RevenueReport> Revenue { get; } = new();
    public ReportRegion<BestSellerReport> BestSellers { get; } = new();
    public ReportRegion<OrderChannelReport> Channels { get; } = new();
    public ReportRegion<IReadOnlyList<LowStockIngredient>> LowStock { get; } = new();
    public WorkflowFeedback Feedback { get; private set; } = WorkflowFeedback.Ready("Choose a reporting period.");

    public async Task LoadAsync(DateTimeOffset firstDate, DateTimeOffset lastDate, TimeZoneInfo timeZone)
    {
        if (IsBusy) return;
        if (!ReportPresentation.TryCreatePeriod(firstDate, lastDate, out var first, out var last, out var message))
        {
            Feedback = WorkflowFeedback.Validation("reporting period", message!);
            Notify();
            return;
        }

        FirstDate = first;
        LastDate = last;
        IsBusy = true;
        // Clear all previous-period values before publishing the new period.
        Revenue.Reset();
        BestSellers.Reset();
        Channels.Reset();
        LowStock.Reset();
        Feedback = WorkflowFeedback.Loading("Loading the selected reporting period…");
        Notify();
        try
        {
            await Revenue.LoadAsync(() => source.RevenueAsync(first, last, timeZone));
            Notify();
            await BestSellers.LoadAsync(() => source.BestSellersAsync(first, last, timeZone));
            Notify();
            await Channels.LoadAsync(() => source.ChannelsAsync(first, last, timeZone));
            Notify();
            await LowStock.LoadAsync(source.LowStockAsync);
            Feedback = Revenue.Error is not null || BestSellers.Error is not null || Channels.Error is not null || LowStock.Error is not null
                ? WorkflowFeedback.Ready("Some regions could not load. Available results are shown; select Refresh to retry.")
                : WorkflowFeedback.Success("Reports refreshed for the selected period. Stock shows current balances.");
        }
        finally
        {
            IsBusy = false;
            Notify();
        }
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}

public sealed class ReportRegion<T> where T : notnull
{
    public T? Value { get; private set; }
    public Error? Error { get; private set; }
    public bool IsLoaded { get; private set; }

    public void Reset()
    {
        Value = default;
        Error = null;
        IsLoaded = false;
    }

    public async Task LoadAsync(Func<Task<Result<T>>> query)
    {
        Reset();
        try
        {
            var result = await query();
            if (result.IsSuccess) Value = result.Value;
            else Error = result.Error;
        }
        catch (ApiException exception)
        {
            Error = exception.Kind switch
            {
                ApiErrorKind.Unauthorized => Error.Authentication("reports.unauthorized", "Your session has expired. Sign in again."),
                ApiErrorKind.Forbidden => Error.Authorization("reports.forbidden", "You do not have permission to view this report."),
                ApiErrorKind.Validation => Error.Validation("reports.invalid_request", "Check the reporting period and try again."),
                _ => Error.Unexpected("reports.unavailable", "This report could not load. Check the connection and select Refresh to retry."),
            };
        }
        catch (Exception)
        {
            Error = Error.Unexpected("reports.unavailable", "This report could not load. Check the connection and select Refresh to retry.");
        }
        IsLoaded = true;
    }
}
