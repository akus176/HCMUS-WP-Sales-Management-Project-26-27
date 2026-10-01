using System.Reflection;
using CoffeeShop.Application.Common.Results;

namespace CoffeeShop.Application.Tests.Common.Results;

public sealed class ResultContractBoundaryTests
{
    private static readonly string[] ForbiddenNamespacePrefixes =
    [
        "FluentValidation",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
    ];

    [Fact]
    public void PublicContracts_DoNotExposeFrameworkSpecificTypes()
    {
        Type[] contractTypes =
        [
            typeof(ErrorType),
            typeof(ErrorDetail),
            typeof(Error),
            typeof(Result),
            typeof(Result<string>),
        ];

        var exposedTypes = contractTypes
            .SelectMany(GetPublicSignatureTypes)
            .Where(type => type.Namespace is not null)
            .ToArray();

        Assert.DoesNotContain(
            exposedTypes,
            type => ForbiddenNamespacePrefixes.Any(prefix =>
                type.Namespace!.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private static IEnumerable<Type> GetPublicSignatureTypes(Type contractType)
    {
        yield return contractType;

        foreach (var constructor in contractType.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var property in contractType.GetProperties())
        {
            yield return property.PropertyType;
        }

        foreach (var method in contractType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            yield return method.ReturnType;

            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }
    }
}
