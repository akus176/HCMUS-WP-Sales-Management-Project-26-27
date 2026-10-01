namespace CoffeeShop.Application.Common.Results;

public enum ErrorType
{
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Authentication = 4,
    Authorization = 5,
    BusinessRule = 6,
    Unexpected = 7,
}
