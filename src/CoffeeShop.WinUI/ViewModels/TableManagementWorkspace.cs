using System.ComponentModel;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.Services;

namespace CoffeeShop.WinUI.ViewModels;

public sealed class TableManagementWorkspace(ITableManagementSource source) : INotifyPropertyChanged
{
    private bool commandSucceeded;
    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<TableManagementSummary> Tables { get; private set; } = [];
    public bool IsBusy { get; private set; }
    public Error? Error { get; private set; }
    public string Message { get; private set; } = "Manage dining tables and their seat counts.";
    public bool IsEmpty => !IsBusy && Error is null && Tables.Count == 0;
    public WorkflowFeedback Feedback => commandSucceeded && !IsBusy && Error is null
        ? WorkflowFeedback.Success(Message)
        : WorkflowFeedback.Resolve(IsBusy, IsEmpty, Error, Message);

    public Task LoadAsync() => RunAsync(ReloadCoreAsync);

    public Task<bool> SaveAsync(EntityId? id, string number, int? seats) => RunAsync(async () =>
    {
        var result = id is { } tableId
            ? await source.UpdateAsync(new(tableId, number, seats))
            : await source.CreateAsync(new CreateDiningTableCommand(number, seats));
        if (result.IsFailure) return result.Error!;
        return await LoadRowsAsync();
    }, id is null ? "Table created." : "Table updated.", markSuccess: true);

    public Task<bool> DeleteAsync(EntityId id) => RunAsync(async () =>
    {
        var result = await source.DeleteAsync(new(id));
        if (result.IsFailure) return result.Error!;
        return await LoadRowsAsync();
    }, "Table deleted.", markSuccess: true);

    private async Task<bool> RunAsync(Func<Task<Error?>> action, string? successMessage = null,
        bool markSuccess = false)
    {
        if (IsBusy) return false;
        IsBusy = true;
        Error = null;
        commandSucceeded = false;
        Message = "Working...";
        Notify();
        try
        {
            var error = await action();
            if (error is not null)
            {
                Error = error;
                Message = error.Description;
                return false;
            }

            commandSucceeded = markSuccess;
            if (successMessage is not null) Message = successMessage;
            return true;
        }
        catch (Exception)
        {
            Error = Error.Unexpected("tables.database_error",
                "The table operation could not complete. Reload the page and verify the saved state before retrying.");
            Message = Error.Description;
            return false;
        }
        finally
        {
            IsBusy = false;
            Notify();
        }
    }

    private async Task<Error?> ReloadCoreAsync() => await LoadRowsAsync();

    private async Task<Error?> LoadRowsAsync()
    {
        var result = await source.ListAsync();
        if (result.IsFailure) return result.Error!;
        Tables = result.Value;
        Message = result.Value.Count == 0
            ? "No dining tables yet. Create the first table to enable dine-in service."
            : $"{result.Value.Count} tables · {result.Value.Count(table => table.IsOccupied)} occupied";
        return null;
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
