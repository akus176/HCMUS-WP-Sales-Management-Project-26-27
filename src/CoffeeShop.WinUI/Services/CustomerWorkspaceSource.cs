using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Sales;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Services;

public interface ICustomerWorkspaceSource
{
    Task<Result<IReadOnlyList<CustomerSummary>>> SearchAsync(CustomerSearchCriteria criteria);
    Task<Result<CustomerDetail>> GetDetailAsync(EntityId id);
    Task<Result<LoyaltyHistoryPage>> GetHistoryAsync(EntityId id, LoyaltyHistoryCriteria criteria);
    Task<Result<EntityId>> CreateAsync(CreateCustomerCommand command);
    Task<Result> UpdateAsync(UpdateCustomerCommand command);
    Task<Result> DeactivateAsync(DeactivateCustomerCommand command);
    Task<Result<EntityId>> AddAddressAsync(AddCustomerAddressCommand command);
    Task<Result> UpdateAddressAsync(UpdateCustomerAddressCommand command);
    Task<Result> SetDefaultAddressAsync(SetDefaultCustomerAddressCommand command);
    Task<Result> DeactivateAddressAsync(DeactivateCustomerAddressCommand command);
    Task<Result<CustomerOrderSelection>> PrepareOrderSelectionAsync(CustomerOrderSelectionRequest request);
}

public sealed class DirectCustomerWorkspaceSource(IServiceScopeFactory scopes) : ICustomerWorkspaceSource
{
    public Task<Result<IReadOnlyList<CustomerSummary>>> SearchAsync(CustomerSearchCriteria criteria) => QueryAsync(async service =>
        Convert(await service.GetRequiredService<CustomerQueryHandler>().SearchCustomersAsync(criteria), rows => (IReadOnlyList<CustomerSummary>)rows));
    public Task<Result<CustomerDetail>> GetDetailAsync(EntityId id) => QueryAsync(service => service.GetRequiredService<CustomerQueryHandler>().GetCustomerDetailAsync(id));
    public Task<Result<LoyaltyHistoryPage>> GetHistoryAsync(EntityId id, LoyaltyHistoryCriteria criteria) => QueryAsync(service => service.GetRequiredService<CustomerQueryHandler>().GetLoyaltyHistoryAsync(id, criteria));
    public Task<Result<EntityId>> CreateAsync(CreateCustomerCommand command) => CommandAsync(service => service.GetRequiredService<CustomerCommandHandler>().CreateCustomerAsync(command));
    public Task<Result> UpdateAsync(UpdateCustomerCommand command) => CommandAsync(service => service.GetRequiredService<CustomerCommandHandler>().UpdateCustomerAsync(command));
    public Task<Result> DeactivateAsync(DeactivateCustomerCommand command) => CommandAsync(service => service.GetRequiredService<CustomerCommandHandler>().DeactivateCustomerAsync(command));
    public Task<Result<EntityId>> AddAddressAsync(AddCustomerAddressCommand command) => CommandAsync(service => service.GetRequiredService<CustomerCommandHandler>().AddAddressAsync(command));
    public Task<Result> UpdateAddressAsync(UpdateCustomerAddressCommand command) => CommandAsync(service => service.GetRequiredService<CustomerCommandHandler>().UpdateAddressAsync(command));
    public Task<Result> SetDefaultAddressAsync(SetDefaultCustomerAddressCommand command) => CommandAsync(service => service.GetRequiredService<CustomerCommandHandler>().SetDefaultAddressAsync(command));
    public Task<Result> DeactivateAddressAsync(DeactivateCustomerAddressCommand command) => CommandAsync(service => service.GetRequiredService<CustomerCommandHandler>().DeactivateAddressAsync(command));
    public Task<Result<CustomerOrderSelection>> PrepareOrderSelectionAsync(CustomerOrderSelectionRequest request) => QueryAsync(service => service.GetRequiredService<CustomerOrderSelectionHandler>().PrepareAsync(request));

    private async Task<Result<T>> QueryAsync<T>(Func<IServiceProvider, Task<Result<T>>> operation) where T : notnull
    { using var scope = scopes.CreateScope(); return await operation(scope.ServiceProvider); }
    private async Task<Result<T>> CommandAsync<T>(Func<IServiceProvider, Task<Result<T>>> operation) where T : notnull
    { using var scope = scopes.CreateScope(); return await operation(scope.ServiceProvider); }
    private async Task<Result> CommandAsync(Func<IServiceProvider, Task<Result>> operation)
    { using var scope = scopes.CreateScope(); return await operation(scope.ServiceProvider); }
    private static Result<TOut> Convert<TIn, TOut>(Result<TIn> result, Func<TIn, TOut> convert) where TIn : notnull where TOut : notnull =>
        result.IsSuccess ? Result<TOut>.Success(convert(result.Value)) : Result<TOut>.Failure(result.Error!);
}

public sealed class ApiCustomerWorkspaceSource(AuthenticatedApiClient client) : ICustomerWorkspaceSource
{
    public Task<Result<IReadOnlyList<CustomerSummary>>> SearchAsync(CustomerSearchCriteria criteria)
    {
        var path = $"customers?search={Uri.EscapeDataString(criteria.Query ?? string.Empty)}&limit={Math.Clamp(criteria.Limit ?? 100, 1, 100)}";
        if (!criteria.IncludeInactive) path += "&isActive=true";
        return RunAsync(async () => (IReadOnlyList<CustomerSummary>)(await client.GetAsync<CustomerResponse[]>(path))
            .Select(row => new CustomerSummary(new(row.Id), row.FullName, row.Phone, row.Email, row.IsActive, row.LoyaltyPointsBalance)).ToArray());
    }

    public Task<Result<CustomerDetail>> GetDetailAsync(EntityId id) => RunAsync(async () => ToDetail(await client.GetAsync<CustomerDetailResponse>($"customers/{id.Value}")));
    public Task<Result<LoyaltyHistoryPage>> GetHistoryAsync(EntityId id, LoyaltyHistoryCriteria criteria) => RunAsync(async () =>
    {
        var page = await client.GetAsync<LoyaltyHistoryResponse>($"customers/{id.Value}/loyalty-transactions?offset={Math.Max(0, criteria.Offset)}&limit={Math.Clamp(criteria.Limit, 1, 100)}");
        return new LoyaltyHistoryPage(page.Items.Select(item => new LoyaltyTransactionSummary(new(item.Id), item.Type, item.Points,
            item.BalanceAfter, ToId(item.OrderId), ToId(item.ReversesTransactionId), item.CreatedAt, ToId(item.CreatedBy))).ToArray(),
            page.Offset, page.Limit, page.TotalCount);
    });
    public async Task<Result<EntityId>> CreateAsync(CreateCustomerCommand command) => await RunAsync(async () =>
        new EntityId((await client.PostAsync<CustomerUpsertRequest, CustomerDetailResponse>("customers", new(command.FullName, command.Phone, command.Email))).Id));
    public Task<Result> UpdateAsync(UpdateCustomerCommand command) => RunCommandAsync(() => client.PatchAsync<CustomerUpsertRequest, object>($"customers/{command.CustomerId.Value}", new(command.FullName, command.Phone, command.Email)));
    public Task<Result> DeactivateAsync(DeactivateCustomerCommand command) => RunCommandAsync(() => client.PostAsync<object, object>($"customers/{command.CustomerId.Value}/deactivate", new { }));
    public async Task<Result<EntityId>> AddAddressAsync(AddCustomerAddressCommand command) => await RunAsync(async () =>
        new EntityId((await client.PostAsync<CustomerAddressRequest, CustomerAddressResponse>($"customers/{command.CustomerId.Value}/addresses", new(command.AddressLine, command.Label, command.IsDefault))).Id));
    public Task<Result> UpdateAddressAsync(UpdateCustomerAddressCommand command) => RunCommandAsync(() => client.PatchAsync<CustomerAddressRequest, object>(
        $"customers/{command.CustomerId.Value}/addresses/{command.AddressId.Value}", new(command.AddressLine, command.Label, command.IsDefault)));
    public Task<Result> SetDefaultAddressAsync(SetDefaultCustomerAddressCommand command) => RunCommandAsync(() => client.PostAsync<object, object>(
        $"customers/{command.CustomerId.Value}/addresses/{command.AddressId.Value}/default", new { }));
    public Task<Result> DeactivateAddressAsync(DeactivateCustomerAddressCommand command) => RunCommandAsync(() => client.PostAsync<object, object>(
        $"customers/{command.CustomerId.Value}/addresses/{command.AddressId.Value}/deactivate", new { }));

    public Task<Result<CustomerOrderSelection>> PrepareOrderSelectionAsync(CustomerOrderSelectionRequest request) => RunAsync(async () =>
    {
        CustomerDetail? customer = request.CustomerId is { } id ? await GetDetailRequiredAsync(id) : null;
        if (customer is { IsActive: false }) throw LocalError(ApiErrorKind.Conflict, "This customer is inactive. Choose an active customer or continue as a walk-in.");
        if (!request.IsDelivery)
        {
            if (request.CustomerAddressId is not null) throw LocalError(ApiErrorKind.Validation, "A saved delivery address can only be selected for a delivery order.");
            return new CustomerOrderSelection(customer?.Id, customer?.FullName, null, null);
        }
        var addressLine = request.AddressLine;
        if (request.CustomerAddressId is { } addressId)
        {
            var address = customer?.Addresses.SingleOrDefault(item => item.Id == addressId && item.IsActive);
            if (address is null) throw LocalError(ApiErrorKind.Conflict, "Choose an active address belonging to the attached customer, or enter an order-only address.");
            addressLine = address.AddressLine;
        }
        try
        {
            var delivery = new Delivery(EntityId.New(), EntityId.New(), request.RecipientName ?? string.Empty,
                request.ContactNumber ?? string.Empty, addressLine ?? string.Empty, request.CustomerAddressId);
            var selection = request.CustomerAddressId is null
                ? DeliverySelection.OrderSpecific(delivery.AddressLine, delivery.RecipientName, delivery.ContactNumber)
                : DeliverySelection.FromCustomerAddress(request.CustomerAddressId, delivery.RecipientName, delivery.ContactNumber);
            return new CustomerOrderSelection(customer?.Id, customer?.FullName, selection, delivery.AddressLine);
        }
        catch (ArgumentException exception) { throw LocalError(ApiErrorKind.Validation, exception.Message); }
    });

    private async Task<CustomerDetail> GetDetailRequiredAsync(EntityId id)
    {
        var result = await GetDetailAsync(id);
        if (result.IsSuccess) return result.Value;
        var error = result.Error!;
        var kind = error.Type switch
        {
            ErrorType.Validation => ApiErrorKind.Validation,
            ErrorType.Conflict => ApiErrorKind.Conflict,
            ErrorType.Authorization => ApiErrorKind.Forbidden,
            ErrorType.Authentication => ApiErrorKind.Unauthorized,
            _ => ApiErrorKind.NotFound
        };
        throw LocalError(kind, error.Description);
    }

    private static CustomerDetail ToDetail(CustomerDetailResponse row) => new(new(row.Id), row.FullName, row.Phone, row.Email,
        row.IsActive, row.LoyaltyPointsBalance, row.CreatedAt, row.UpdatedAt,
        row.Addresses.Select(address => new CustomerAddressSummary(new(address.Id), address.AddressLine, address.Label, address.IsDefault, address.IsActive)).ToArray(),
        row.RecentOrders.Select(order => new CustomerOrderSummary(new(order.Id), order.OrderNumber, order.Status, order.Total, order.CreatedAt)).ToArray());

    private static EntityId? ToId(Guid? id) => id is { } value ? new EntityId(value) : null;
    private static ApiException LocalError(ApiErrorKind kind, string detail) => new(kind,
        kind == ApiErrorKind.Validation ? System.Net.HttpStatusCode.BadRequest : System.Net.HttpStatusCode.Conflict,
        "Customer selection is invalid", detail);

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
    private static Error ToError(ApiException exception)
    {
        var details = exception.Errors.SelectMany(group => group.Value.Select(message => new ErrorDetail(group.Key, message))).ToArray();
        return exception.Kind switch
        {
            ApiErrorKind.Validation => Error.Validation("api.validation", exception.Detail, details),
            ApiErrorKind.NotFound => Error.NotFound("api.not_found", exception.Detail),
            ApiErrorKind.Conflict => Error.Conflict("api.conflict", exception.Detail, details),
            ApiErrorKind.Unauthorized => Error.Authentication("api.unauthorized", exception.Detail),
            ApiErrorKind.Forbidden => Error.Authorization("api.forbidden", exception.Detail),
            ApiErrorKind.BusinessRule => Error.BusinessRule("api.business_rule", exception.Detail, details),
            _ => Error.Unexpected("api.request_failed", exception.Detail)
        };
    }
}
