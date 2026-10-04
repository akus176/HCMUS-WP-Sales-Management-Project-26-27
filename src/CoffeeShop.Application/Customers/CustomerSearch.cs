namespace CoffeeShop.Application.Customers;

using CoffeeShop.Domain.Customers;

public sealed record CustomerSearchCriteria(
    string? Query = null,
    bool IncludeInactive = false,
    int? Limit = null);

/// <summary>
/// Provides deterministic search and filtering over customer profiles.
///
/// Deterministic Search Specification:
/// 1. Active State Filtering:
///    - By default (IncludeInactive = false), only active customers (IsActive == true) are returned.
///    - When IncludeInactive = true, both active and deactivated profiles are included.
///
/// 2. Query Matching &amp; Relevance Ranking:
///    - If Query is null, empty, or whitespace, all filtered candidates match (Relevance Tier 0).
///    - When Query is provided, the query is trimmed. Comparison is performed against Phone, FullName, and Email:
///      - Tier 1: Exact Phone Match (normalized digits match exactly)
///      - Tier 2: Phone Prefix Match (phone starts with query)
///      - Tier 3: Full Name Prefix Match (full name starts with query, case-insensitive)
///      - Tier 4: Phone Infix Match (phone contains query)
///      - Tier 5: Full Name Infix Match (full name contains query, case-insensitive)
///      - Tier 6: Email Infix Match (email contains query, case-insensitive)
///    - Customers not matching any of the above criteria are excluded.
///
/// 3. Deterministic Ordering (Tie-Breaking):
///    - Ascending Match Tier (Tier 1 &gt; Tier 2 &gt; ... &gt; Tier 6)
///    - Full Name (OrdinalIgnoreCase ascending)
///    - Phone (Ordinal ascending)
///    - CreatedAt timestamp (chronological ascending)
///    - EntityId value (ascending)
///
/// 4. Result Truncation:
///    - If Limit is greater than 0, at most Limit results are returned.
/// </summary>
public static class CustomerSearch
{
    public static IReadOnlyList<Customer> Search(
        IEnumerable<Customer> candidates,
        CustomerSearchCriteria? criteria = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        criteria ??= new CustomerSearchCriteria();
        var includeInactive = criteria.IncludeInactive;
        var query = criteria.Query?.Trim();

        var filtered = candidates.Where(c => includeInactive || c.IsActive);

        if (string.IsNullOrWhiteSpace(query))
        {
            var orderedWithoutQuery = filtered
                .OrderBy(c => c.FullName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Phone, StringComparer.Ordinal)
                .ThenBy(c => c.CreatedAt.Value)
                .ThenBy(c => c.Id.Value);

            return Truncate(orderedWithoutQuery, criteria.Limit);
        }

        var scored = new List<(Customer Customer, int Tier)>();

        foreach (var customer in filtered)
        {
            var tier = CalculateRelevanceTier(customer, query);
            if (tier.HasValue)
            {
                scored.Add((customer, tier.Value));
            }
        }

        var orderedWithQuery = scored
            .OrderBy(entry => entry.Tier)
            .ThenBy(entry => entry.Customer.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Customer.Phone, StringComparer.Ordinal)
            .ThenBy(entry => entry.Customer.CreatedAt.Value)
            .ThenBy(entry => entry.Customer.Id.Value)
            .Select(entry => entry.Customer);

        return Truncate(orderedWithQuery, criteria.Limit);
    }

    private static int? CalculateRelevanceTier(Customer customer, string query)
    {
        var normalizedQueryPhone = NormalizePhone(query);
        var normalizedCustomerPhone = NormalizePhone(customer.Phone);

        // Tier 1: Exact Phone match
        if (customer.Phone.Equals(query, StringComparison.Ordinal) ||
            (!string.IsNullOrEmpty(normalizedQueryPhone) && normalizedCustomerPhone == normalizedQueryPhone))
        {
            return 1;
        }

        // Tier 2: Phone prefix match
        if (customer.Phone.StartsWith(query, StringComparison.Ordinal) ||
            (!string.IsNullOrEmpty(normalizedQueryPhone) && normalizedCustomerPhone.StartsWith(normalizedQueryPhone, StringComparison.Ordinal)))
        {
            return 2;
        }

        // Tier 3: Full Name prefix match
        if (customer.FullName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        // Tier 4: Phone contains match
        if (customer.Phone.Contains(query, StringComparison.Ordinal) ||
            (!string.IsNullOrEmpty(normalizedQueryPhone) && normalizedCustomerPhone.Contains(normalizedQueryPhone, StringComparison.Ordinal)))
        {
            return 4;
        }

        // Tier 5: Full Name contains match
        if (customer.FullName.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        // Tier 6: Email contains match
        if (customer.Email is not null && customer.Email.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 6;
        }

        return null;
    }

    private static string NormalizePhone(string input) =>
        new(input.Where(char.IsDigit).ToArray());

    private static IReadOnlyList<Customer> Truncate(IEnumerable<Customer> items, int? limit) =>
        limit is > 0 ? items.Take(limit.Value).ToList().AsReadOnly() : items.ToList().AsReadOnly();
}
