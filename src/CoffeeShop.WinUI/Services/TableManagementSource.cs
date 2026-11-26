using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Services;

public interface ITableManagementSource
{
    Task<Result<IReadOnlyList<TableManagementSummary>>> ListAsync();
    Task<Result<EntityId>> CreateAsync(CreateTableCommand command);
    Task<Result> UpdateAsync(UpdateTableCommand command);
    Task<Result> DeleteAsync(DeleteTableCommand command);
}

public sealed class DirectTableManagementSource(IServiceScopeFactory scopes) : ITableManagementSource
{
    public Task<Result<IReadOnlyList<TableManagementSummary>>> ListAsync() => QueryAsync(async service =>
        Result<IReadOnlyList<TableManagementSummary>>.Success(
            await service.GetRequiredService<TableManagementQueryHandler>().ListAsync()));

    public Task<Result<EntityId>> CreateAsync(CreateTableCommand command) => CommandAsync(service =>
        service.GetRequiredService<TableCommandHandler>().CreateAsync(command));

    public Task<Result> UpdateAsync(UpdateTableCommand command) => CommandAsync(service =>
        service.GetRequiredService<TableCommandHandler>().UpdateAsync(command));

    public Task<Result> DeleteAsync(DeleteTableCommand command) => CommandAsync(service =>
        service.GetRequiredService<TableCommandHandler>().DeleteAsync(command));

    private async Task<Result<T>> QueryAsync<T>(Func<IServiceProvider, Task<Result<T>>> operation) where T : notnull
    {
        using var scope = scopes.CreateScope();
        return await operation(scope.ServiceProvider);
    }

    private async Task<Result<T>> CommandAsync<T>(Func<IServiceProvider, Task<Result<T>>> operation) where T : notnull
    {
        using var scope = scopes.CreateScope();
        return await operation(scope.ServiceProvider);
    }

    private async Task<Result> CommandAsync(Func<IServiceProvider, Task<Result>> operation)
    {
        using var scope = scopes.CreateScope();
        return await operation(scope.ServiceProvider);
    }
}

public sealed class ApiTableManagementSource(AuthenticatedApiClient client) : ITableManagementSource
{
    public Task<Result<IReadOnlyList<TableManagementSummary>>> ListAsync() => RunAsync(async () =>
        (IReadOnlyList<TableManagementSummary>)(await client.GetAsync<TableManagementResponse[]>("table-management"))
            .Select(row => new TableManagementSummary(new(row.Id), row.TableNumber, row.Seats, row.IsOccupied,
                row.ActiveOrderNumber)).ToArray());

    public Task<Result<EntityId>> CreateAsync(CreateTableCommand command) => RunAsync(async () =>
        new EntityId((await client.PostAsync<CreateTableRequest, TableManagementResponse>("table-management",
            new(command.TableNumber, command.Seats))).Id));

    public Task<Result> UpdateAsync(UpdateTableCommand command) => RunCommandAsync(() =>
        client.PutAsync<UpdateTableRequest, TableManagementResponse>($"table-management/{command.Id.Value}",
            new(command.TableNumber, command.Seats)));

    public Task<Result> DeleteAsync(DeleteTableCommand command) => RunCommandAsync(() =>
        client.DeleteAsync<object>($"table-management/{command.Id.Value}"));

    private static async Task<Result<T>> RunAsync<T>(Func<Task<T>> action) where T : notnull
    {
        try { return Result<T>.Success(await action()); }
        catch (ApiException exception) { return Result<T>.Failure(ToError(exception)); }
        catch (HttpRequestException exception) { return Result<T>.Failure(Error.Unexpected("api.network_error", exception.Message)); }
    }

    private static async Task<Result> RunCommandAsync(Func<Task> action)
    {
        try { await action(); return Result.Success(); }
        catch (ApiException exception) { return Result.Failure(ToError(exception)); }
        catch (HttpRequestException exception) { return Result.Failure(Error.Unexpected("api.network_error", exception.Message)); }
    }

    private static Error ToError(ApiException exception) => exception.Kind switch
    {
        ApiErrorKind.Validation => Error.Validation("api.validation", exception.Detail),
        ApiErrorKind.NotFound => Error.NotFound("api.not_found", exception.Detail),
        ApiErrorKind.Conflict => Error.Conflict("api.conflict", exception.Detail),
        ApiErrorKind.Unauthorized => Error.Authentication("api.unauthorized", exception.Detail),
        ApiErrorKind.Forbidden => Error.Authorization("api.forbidden", exception.Detail),
        ApiErrorKind.BusinessRule => Error.BusinessRule("api.business_rule", exception.Detail),
        _ => Error.Unexpected("api.request_failed", exception.Detail)
    };
}
