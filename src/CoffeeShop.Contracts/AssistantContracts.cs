namespace CoffeeShop.Contracts;

using System.Text.Json.Serialization;

/// <summary>Only these four read-only report tools can be selected by the assistant.</summary>
public static class AssistantTools
{
    public const string Revenue = "revenue";
    public const string BestSellers = "best_sellers";
    public const string Channels = "order_channels";
    public const string LowStock = "low_stock";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssistantToolInput(DateOnly? From = null, DateOnly? To = null,
    string? TimeZone = null, string? SortBy = null, int? Limit = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssistantAskRequest(string? Question, DateOnly? From, DateOnly? To, string? TimeZone);

public sealed record AssistantToolResult(string Tool, string Outcome, object? Metrics);

public sealed record AssistantAskResponse(string Answer, DateOnly? From, DateOnly? To,
    IReadOnlyList<AssistantToolResult> ToolsInvoked);
