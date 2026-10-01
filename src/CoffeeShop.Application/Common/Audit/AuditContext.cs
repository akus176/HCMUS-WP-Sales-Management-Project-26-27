namespace CoffeeShop.Application.Common.Audit;

using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;

public class AuditContext : IAuditContext
{
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public AuditContext(IClock clock, ICurrentUser currentUser)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public virtual AuditMetadata CreateMetadata()
    {
        var now = _clock.UtcNow;
        var actor = _currentUser.GetRequiredId();
        return new AuditMetadata(now, actor);
    }

    public virtual AuditMetadata TouchUpdated(AuditMetadata current)
    {
        ArgumentNullException.ThrowIfNull(current);

        var now = _clock.UtcNow;
        if (now.Value < current.CreatedAt.Value)
        {
            throw new InvalidOperationException("The updated timestamp cannot be earlier than the created timestamp.");
        }

        var actor = _currentUser.GetRequiredId();
        return new AuditMetadata(current.CreatedAt, current.CreatedBy, now, actor);
    }
}
