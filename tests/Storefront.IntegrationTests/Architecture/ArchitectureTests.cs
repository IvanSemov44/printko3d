using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Storefront.IntegrationTests.Architecture;

/// <summary>
/// Rules of CLAUDE.md and decision 0017 that can be checked by machine. They read the compiled assemblies,
/// so no extra package is needed. Reflection sees only the assemblies the code really uses, which is what we want.
/// </summary>
public sealed class ArchitectureTests
{
    private const string ControllersNamespace = "Storefront.Api.Controllers";

    private static readonly Assembly DomainAssembly = typeof(Domain.Entity).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_DoesNotReferenceAnyOtherProjectOfTheSolution()
    {
        var references = DomainAssembly.GetReferencedAssemblies().Select(name => name.Name!).ToList();

        Assert.DoesNotContain(references, name => name.StartsWith("Storefront.", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Api_DoesNotReferenceEntityFramework()
    {
        var references = ApiAssembly.GetReferencedAssemblies().Select(name => name.Name!).ToList();

        // Guards the test itself: if the Api stopped using Application, this check would prove nothing.
        Assert.Contains("Storefront.Application", references);
        Assert.DoesNotContain(references, name =>
                name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || name.Contains("Identity.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Controllers_DoNotDeclareRecords()
    {
        var records = ControllerTypesAndNestedTypes().Where(IsRecord).Select(type => type.FullName!).ToList();

        Assert.Empty(records);
    }

    [Fact]
    public void EveryAction_ReturnsActionResultOrActionResultOfT()
    {
        var actions = Controllers()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .ToList();

        // Guards the test itself: the Api has many actions; zero found means the filter is wrong.
        Assert.True(actions.Count > 10, $"Only {actions.Count} actions were found; the filter looks wrong.");

        var offenders = actions
            .Where(method => !ReturnsActionResult(method.ReturnType))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name} returns {method.ReturnType}")
            .ToList();

        Assert.Empty(offenders);
    }

    private static IEnumerable<Type> Controllers() =>
        ApiAssembly.GetTypes().Where(type => type.Namespace == ControllersNamespace && typeof(ControllerBase).IsAssignableFrom(type));

    private static IEnumerable<Type> ControllerTypesAndNestedTypes() =>
        ApiAssembly.GetTypes().Where(type => type.Namespace == ControllersNamespace || (type.DeclaringType?.Namespace == ControllersNamespace));

    // The compiler adds a "<Clone>$" method to every record; ordinary classes never have one.
    private static bool IsRecord(Type type) => type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null;

    private static bool ReturnsActionResult(Type returnType)
    {
        var result = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>) ? returnType.GetGenericArguments()[0] : returnType;

        return result == typeof(ActionResult) || (result.IsGenericType && result.GetGenericTypeDefinition() == typeof(ActionResult<>));
    }
}
