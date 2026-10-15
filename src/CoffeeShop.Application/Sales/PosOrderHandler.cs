namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class PosOrderHandler(IPosRepository repository, IOrderConfirmationRepository confirmationRepository,
    CustomerOrderSelectionHandler customers, OrderConfirmationCommandHandler confirmation, DiscountCommandHandler discounts,
    IUnitOfWork unitOfWork, ITransactionManager transactions, IClock clock, ICurrentUser currentUser)
{
    public Task<PosOptions> GetOptionsAsync(CancellationToken cancellationToken = default) => repository.GetOptionsAsync(cancellationToken);

    public async Task<Result<DiscountOperationResult>> RequestDiscountAsync(RequestDiscountCommand command, CancellationToken cancellationToken = default)
    {
        var actorError = await ValidateActorAsync(cancellationToken);
        return actorError is not null ? Result<DiscountOperationResult>.Failure(actorError)
            : await discounts.RequestAsync(command, cancellationToken);
    }

    public async Task<Result<PosOrderDetail>> GetOrderAsync(EntityId id, CancellationToken cancellationToken = default)
    {
        var order = await repository.GetOrderAsync(id, cancellationToken);
        return order is null
            ? Result<PosOrderDetail>.Failure(Error.NotFound("pos.order_not_found", "Ticket not found. Check the order identifier."))
            : Result<PosOrderDetail>.Success(order);
    }

    public async Task<Result<PosOrderDetail>> CreateDraftAsync(CreatePosDraftCommand command, CancellationToken cancellationToken = default)
    {
        var actorError = await ValidateActorAsync(cancellationToken);
        if (actorError is not null) return Result<PosOrderDetail>.Failure(actorError);
        return await transactions.ExecuteInTransactionAsync(async () =>
        {
            // Stable client-generated identity makes retrying an uncertain save safe.
            var existing = await repository.GetOrderAsync(command.OrderId, cancellationToken);
            if (existing is not null) return Result<PosOrderDetail>.Success(existing);
            if (command.Channel != "dine_in" && command.TableId is not null)
                return Invalid("Only dine-in orders can select a table.");

            var catalog = await confirmationRepository.LoadCatalogForConfirmationAsync(
                command.Lines.Select(line => line.MenuItemId).Distinct().ToArray(),
                command.Lines.Select(line => line.SizeVariantId).Distinct().ToArray(),
                command.Lines.SelectMany(line => line.AddonIds).Distinct().ToArray(), cancellationToken);
            var selections = new List<OrderLineSelection>();
            foreach (var line in command.Lines)
            {
                if (!catalog.MenuItems.TryGetValue(line.MenuItemId, out var item)
                    || !catalog.Categories.TryGetValue(item.CategoryId, out var category) || !category.IsActive
                    || !catalog.SizeVariants.TryGetValue(line.SizeVariantId, out var variant)
                    || line.AddonIds.Any(id => !catalog.Addons.ContainsKey(id)))
                    return Invalid("A drink, category, size, or add-on is unavailable. Reload the menu and choose again.");
                selections.Add(new(item, variant, line.Quantity, line.AddonIds.Select(id => catalog.Addons[id]).ToArray(),
                    catalog.CompatibilityLinks));
            }

            var draft = await customers.CreateDraftAsync(new(command.OrderId,
                "POS-" + command.OrderId.Value.ToString("N")[..26], command.Channel, currentUser.GetRequiredId(),
                clock.UtcNow, selections, TableId: command.TableId), command.Customer, cancellationToken);
            if (draft.IsFailure) return Result<PosOrderDetail>.Failure(draft.Error!);
            repository.AddDraft(draft.Value.Order, draft.Value.Delivery);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return await GetOrderAsync(command.OrderId, cancellationToken);
        }, cancellationToken);
    }

    public async Task<Result<PosOrderDetail>> ConfirmAsync(EntityId orderId, decimal reviewedTotal, CancellationToken cancellationToken = default)
    {
        var actorError = await ValidateActorAsync(cancellationToken);
        if (actorError is not null) return Result<PosOrderDetail>.Failure(actorError);
        return await transactions.ExecuteInTransactionAsync(async () =>
        {
            var order = await confirmationRepository.LoadOrderForConfirmationAsync(orderId, cancellationToken);
            if (order is null) return await GetOrderAsync(orderId, cancellationToken);
            if (order.Status == OrderStatus.Draft)
            {
                if (order.Total.Amount != reviewedTotal)
                    return Result<PosOrderDetail>.Failure(Error.Conflict("pos.total_changed", "The total changed. Reload and review approved discounts before confirming."));
                var detail = await repository.GetOrderAsync(orderId, cancellationToken);
                var customer = await customers.PrepareAsync(new(order.CustomerId, order.Channel == "delivery",
                    detail!.CustomerAddressId, detail.RecipientName, detail.ContactNumber, detail.AddressLine), cancellationToken);
                if (customer.IsFailure) return Result<PosOrderDetail>.Failure(customer.Error!);
            }

            var result = await confirmation.ConfirmAsync(new(orderId, currentUser.GetRequiredId()), cancellationToken);
            return result.IsFailure ? Result<PosOrderDetail>.Failure(result.Error!) : await GetOrderAsync(orderId, cancellationToken);
        }, cancellationToken);
    }

    private async Task<Error?> ValidateActorAsync(CancellationToken cancellationToken) =>
        currentUser.Id is not { } id || !await repository.IsActiveStaffAsync(id, cancellationToken)
            ? Error.Validation("pos.operator_required", "Choose an active operator before saving or confirming.") : null;

    private static Result<PosOrderDetail> Invalid(string message) =>
        Result<PosOrderDetail>.Failure(Error.Validation("pos.invalid_selection", message));
}
