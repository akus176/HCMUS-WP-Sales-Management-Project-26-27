namespace CoffeeShop.Api;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Contracts;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this RouteGroupBuilder api)
    {
        var customers = api.MapGroup("/customers").RequireAuthorization("customers.manage")
            .WithDescription(StaffMobileRelease.OutsideScope);
        customers.MapGet("/", async (CustomerQueryHandler handler, string? search, bool? isActive,
            int? limit, bool? includeInactive, CancellationToken ct) =>
        {
            var result = await handler.SearchCustomersAsync(
                new CustomerSearchCriteria(search, IncludeInactive: isActive == false || (isActive is null && includeInactive == true),
                    Limit: limit ?? 50), ct);
            if (result.IsFailure) return ApiErrorResults.From(result.Error!);
            var filtered = isActive == false ? result.Value.Where(customer => !customer.IsActive) : result.Value;
            return Results.Ok(filtered.Select(ToResponse).ToArray());
        }).Produces<CustomerResponse[]>().ProducesProblem(400);

        customers.MapGet("/{id:guid}", async (Guid id, CustomerQueryHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.GetCustomerDetailAsync(new(id), ct);
            return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : ApiErrorResults.From(result.Error!);
        }).Produces<CustomerDetailResponse>().ProducesProblem(400).ProducesProblem(404);

        customers.MapPost("/", async (CustomerUpsertRequest request, CustomerCommandHandler commands,
            CustomerQueryHandler queries, CancellationToken ct) =>
        {
            var created = await commands.CreateCustomerAsync(
                new CreateCustomerCommand(request.FullName ?? string.Empty, request.Phone ?? string.Empty, request.Email), ct);
            if (created.IsFailure) return ApiErrorResults.From(created.Error!);
            var detail = await queries.GetCustomerDetailAsync(created.Value, ct);
            return detail.IsSuccess ? Results.Json(ToResponse(detail.Value), statusCode: StatusCodes.Status201Created)
                : ApiErrorResults.From(detail.Error!);
        }).RequireAuthorization("customers.manage").Produces<CustomerDetailResponse>(201)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409);

        customers.MapPatch("/{id:guid}", async (Guid id, CustomerUpsertRequest request,
            CustomerCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.UpdateCustomerAsync(new UpdateCustomerCommand(new(id),
                request.FullName ?? string.Empty, request.Phone ?? string.Empty, request.Email), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("customers.manage").Produces(204).ProducesProblem(400)
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);

        customers.MapPost("/{id:guid}/deactivate", async (Guid id, CustomerCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.DeactivateCustomerAsync(new(new(id)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("customers.manage").Produces(204).ProducesProblem(400)
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);

        customers.MapGet("/{id:guid}/addresses", async (Guid id, CustomerQueryHandler handler,
            bool? includeInactive, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.GetCustomerDetailAsync(new(id), ct);
            if (result.IsFailure) return ApiErrorResults.From(result.Error!);
            var addresses = result.Value.Addresses.Where(address => includeInactive == true || address.IsActive)
                .Select(ToResponse).ToArray();
            return Results.Ok(addresses);
        }).Produces<CustomerAddressResponse[]>().ProducesProblem(400).ProducesProblem(404);

        customers.MapPost("/{id:guid}/addresses", async (Guid id, CustomerAddressRequest request,
            CustomerCommandHandler commands, CustomerQueryHandler queries, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var added = await commands.AddAddressAsync(new AddCustomerAddressCommand(new(id),
                request.AddressLine ?? string.Empty, request.Label, request.IsDefault), ct);
            if (added.IsFailure) return ApiErrorResults.From(added.Error!);
            var detail = await queries.GetCustomerDetailAsync(new(id), ct);
            if (detail.IsFailure) return ApiErrorResults.From(detail.Error!);
            var address = detail.Value.Addresses.Single(item => item.Id == added.Value);
            return Results.Json(ToResponse(address), statusCode: StatusCodes.Status201Created);
        }).RequireAuthorization("customers.manage").Produces<CustomerAddressResponse>(201)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

        customers.MapPatch("/{id:guid}/addresses/{addressId:guid}", async (Guid id, Guid addressId,
            CustomerAddressRequest request, CustomerCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty || addressId == Guid.Empty) return InvalidId();
            var result = await handler.UpdateAddressAsync(new UpdateCustomerAddressCommand(new(id), new(addressId),
                request.AddressLine ?? string.Empty, request.Label, request.IsDefault), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("customers.manage").Produces(204).ProducesProblem(400)
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);

        customers.MapPost("/{id:guid}/addresses/{addressId:guid}/default", async (Guid id, Guid addressId,
            CustomerCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty || addressId == Guid.Empty) return InvalidId();
            var result = await handler.SetDefaultAddressAsync(new(new(id), new(addressId)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("customers.manage").Produces(204).ProducesProblem(400)
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);

        customers.MapPost("/{id:guid}/addresses/{addressId:guid}/deactivate", async (Guid id, Guid addressId,
            CustomerCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty || addressId == Guid.Empty) return InvalidId();
            var result = await handler.DeactivateAddressAsync(new(new(id), new(addressId)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("customers.manage").Produces(204).ProducesProblem(400)
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);

        customers.MapGet("/{id:guid}/loyalty-transactions", async (Guid id, CustomerQueryHandler handler,
            int? offset, int? limit, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.GetLoyaltyHistoryAsync(new(id),
                new LoyaltyHistoryCriteria(offset ?? 0, limit ?? 50), ct);
            return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : ApiErrorResults.From(result.Error!);
        }).Produces<LoyaltyHistoryResponse>().ProducesProblem(400).ProducesProblem(404);

        api.MapGet("/loyalty-settings", async (CustomerQueryHandler handler, CancellationToken ct) =>
        {
            var result = await handler.GetLoyaltySettingsAsync(ct);
            return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization().Produces<LoyaltySettingsResponse>().ProducesProblem(404)
            .WithDescription(StaffMobileRelease.OutsideScope);

        api.MapPatch("/loyalty-settings", async (LoyaltySettingsRequest request,
            LoyaltyCommandHandler commands, CustomerQueryHandler queries, CancellationToken ct) =>
        {
            var updated = await commands.UpdateSettingsAsync(new UpdateLoyaltySettingsCommand(
                request.EarnPointsPerCurrencyUnit, request.RedeemValuePerPoint, request.MaxRedeemablePercentage), ct);
            if (updated.IsFailure) return ApiErrorResults.From(updated.Error!);
            var result = await queries.GetLoyaltySettingsAsync(ct);
            return result.IsSuccess ? Results.Ok(ToResponse(result.Value)) : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("users.manage").Produces<LoyaltySettingsResponse>().ProducesProblem(400)
            .ProducesProblem(401).ProducesProblem(403)
            .WithDescription(StaffMobileRelease.OutsideScope);

        api.MapPost("/orders/{id:guid}/loyalty/redeem", async (Guid id, RedeemLoyaltyRequest request,
            LoyaltyCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.RedeemForOrderAsync(new RedeemOrderLoyaltyCommand(new(id), request.Points), ct);
            if (result.IsFailure)
            {
                var error = result.Error!;
                return ApiErrorResults.From(error, error.Type == ErrorType.Validation
                    ? StatusCodes.Status422UnprocessableEntity
                    : null);
            }
            return Results.Ok(new LoyaltyOperationResponse(result.Value.TransactionId?.Value,
                result.Value.Points, result.Value.DiscountAmount, result.Value.AlreadyApplied));
        }).RequireAuthorization("loyalty.redeem").Produces<LoyaltyOperationResponse>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404)
            .ProducesProblem(409).ProducesProblem(422)
            .WithDescription(StaffMobileRelease.OutsideScope);
    }

    private static CustomerResponse ToResponse(CustomerSummary customer) => new(customer.Id.Value,
        customer.FullName, customer.Phone, customer.Email, customer.IsActive, customer.LoyaltyPointsBalance);

    private static CustomerDetailResponse ToResponse(CustomerDetail customer) => new(customer.Id.Value,
        customer.FullName, customer.Phone, customer.Email, customer.IsActive, customer.LoyaltyPointsBalance,
        customer.CreatedAt, customer.UpdatedAt, customer.Addresses.Select(ToResponse).ToArray(),
        customer.RecentOrders.Select(order => new CustomerOrderSummaryResponse(order.Id.Value,
            order.OrderNumber, order.Status, order.Total, order.CreatedAt)).ToArray());

    private static CustomerAddressResponse ToResponse(CustomerAddressSummary address) => new(address.Id.Value,
        address.AddressLine, address.Label, address.IsDefault, address.IsActive);

    private static LoyaltyHistoryResponse ToResponse(LoyaltyHistoryPage history) => new(
        history.Items.Select(transaction => new LoyaltyTransactionResponse(transaction.Id.Value, transaction.Type,
            transaction.Points, transaction.BalanceAfter, transaction.OrderId?.Value,
            transaction.ReversesTransactionId?.Value, transaction.CreatedAt, transaction.CreatedBy?.Value)).ToArray(),
        history.Offset, history.Limit, history.TotalCount);

    private static LoyaltySettingsResponse ToResponse(LoyaltySettingsDetail settings) => new(settings.Id.Value,
        settings.EarnPointsPerCurrencyUnit, settings.RedeemValuePerPoint, settings.MaxRedeemablePercentage,
        settings.UpdatedAt, settings.UpdatedBy?.Value);

    private static IResult InvalidId() => ApiErrorResults.From(Error.Validation(
        "customers.invalid_id", "A nonempty identifier is required."));
}
