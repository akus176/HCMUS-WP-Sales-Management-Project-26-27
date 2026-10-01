namespace CoffeeShop.Application.Common.Audit;

using CoffeeShop.Domain.Common;

public interface IAuditContext
{
    AuditMetadata CreateMetadata();

    AuditMetadata TouchUpdated(AuditMetadata current);
}
