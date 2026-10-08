namespace CoffeeShop.Application.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;

public sealed class CustomerQueryHandler
{
    private const int MaximumSearchLength = 120;
    private const int DefaultSearchLimit = 50;
    private const int MaximumPageSize = 200;
    private readonly ICustomerReadRepository repository;

    public CustomerQueryHandler(ICustomerReadRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Result<IReadOnlyList<CustomerSummary>>> SearchCustomersAsync(
        CustomerSearchCriteria? criteria = null,
        CancellationToken cancellationToken = default)
    {
        criteria ??= new CustomerSearchCriteria();
        var query = criteria.Query?.Trim();
        var limit = criteria.Limit ?? DefaultSearchLimit;
        if (query?.Length > MaximumSearchLength)
        {
            return Result<IReadOnlyList<CustomerSummary>>.Failure(Validation(
                "customers.search_too_long", "Search text cannot exceed 120 characters.", "query"));
        }

        if (limit is <= 0 or > MaximumPageSize)
        {
            return Result<IReadOnlyList<CustomerSummary>>.Failure(Validation(
                "customers.invalid_limit", "Search limit must be between 1 and 200.", "limit"));
        }

        var normalized = criteria with
        {
            Query = string.IsNullOrWhiteSpace(query) ? null : query,
            Limit = limit
        };
        var customers = await repository.SearchCustomersAsync(normalized, cancellationToken);
        return Result<IReadOnlyList<CustomerSummary>>.Success(customers);
    }

    public async Task<Result<CustomerDetail>> GetCustomerDetailAsync(
        EntityId customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId is null)
        {
            return Result<CustomerDetail>.Failure(Validation(
                "customers.invalid_input", "A customer identifier is required.", "customerId"));
        }

        var detail = await repository.GetCustomerDetailAsync(customerId, cancellationToken);
        return detail is null
            ? Result<CustomerDetail>.Failure(CustomerNotFound(customerId))
            : Result<CustomerDetail>.Success(detail);
    }

    public async Task<Result<LoyaltyHistoryPage>> GetLoyaltyHistoryAsync(
        EntityId customerId,
        LoyaltyHistoryCriteria? criteria = null,
        CancellationToken cancellationToken = default)
    {
        if (customerId is null)
        {
            return Result<LoyaltyHistoryPage>.Failure(Validation(
                "customers.invalid_input", "A customer identifier is required.", "customerId"));
        }

        criteria ??= new LoyaltyHistoryCriteria();
        if (criteria.Offset < 0)
        {
            return Result<LoyaltyHistoryPage>.Failure(Validation(
                "loyalty.invalid_offset", "History offset cannot be negative.", "offset"));
        }

        if (criteria.Limit is < 1 or > MaximumPageSize)
        {
            return Result<LoyaltyHistoryPage>.Failure(Validation(
                "loyalty.invalid_limit", "History limit must be between 1 and 200.", "limit"));
        }

        var history = await repository.GetLoyaltyHistoryAsync(customerId, criteria, cancellationToken);
        return history is null
            ? Result<LoyaltyHistoryPage>.Failure(CustomerNotFound(customerId))
            : Result<LoyaltyHistoryPage>.Success(history);
    }

    public async Task<Result<LoyaltySettingsDetail>> GetLoyaltySettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await repository.GetLoyaltySettingsAsync(cancellationToken);
        return settings is null
            ? Result<LoyaltySettingsDetail>.Failure(Error.NotFound(
                "loyalty.settings_not_found", "Loyalty settings have not been configured."))
            : Result<LoyaltySettingsDetail>.Success(settings);
    }

    private static Error CustomerNotFound(EntityId id) =>
        Error.NotFound("customers.customer_not_found", $"Customer '{id}' was not found.");

    private static Error Validation(string code, string description, string target) =>
        Error.Validation(code, description, [new ErrorDetail(target, description)]);
}
