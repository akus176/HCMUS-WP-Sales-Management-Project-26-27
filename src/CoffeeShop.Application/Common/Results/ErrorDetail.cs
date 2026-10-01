namespace CoffeeShop.Application.Common.Results;

public sealed record ErrorDetail
{
    public ErrorDetail(string target, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Target = target;
        Message = message;
    }

    public string Target { get; }

    public string Message { get; }
}
