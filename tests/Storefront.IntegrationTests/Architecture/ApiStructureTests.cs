using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Storefront.IntegrationTests.Architecture;

/// <summary>
/// Rules 1, 2, 3, 6 and 7 of decision 0017 that a machine can check (debt D14). Rules 1 to 3 read the source files of
/// <c>Storefront.Api</c>; rules 6 and 7 read the compiled controllers. What stays for the review is written next to
/// each rule: that a POST which creates answers 201, and that an action calls exactly one service.
/// </summary>
public sealed partial class ApiStructureTests
{
    private const string ApiFolder = "src/backend/Storefront.Api";
    private const string ControllersNamespace = "Storefront.Api.Controllers";
    private const int ProgramMaxLines = 30;

    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    // Rule 6: a DELETE answers 204. The image delete of the admin panel answers 200 with the product, because the panel
    // needs the new version of the row (decision 0016). It is the one known exception (debt D60).
    private static readonly HashSet<string> DeleteReturningTheNewState = ["AdminProductImagesController.Delete"];

    [Fact]
    public void Rule1_ProgramCs_OnlyRunsACommandAndTheSetup()
    {
        var program = RepositoryFiles.Read($"{ApiFolder}/Program.cs");
        var lines = program.ReplaceLineEndings("\n").TrimEnd().Split('\n').Length;

        Assert.True(lines <= ProgramMaxLines, $"Program.cs has {lines} lines; the limit is {ProgramMaxLines}.");
        Assert.DoesNotContain("builder.Services", program, StringComparison.Ordinal);
        Assert.DoesNotContain("builder.Configuration", program, StringComparison.Ordinal);
        Assert.DoesNotContain("app.Map", program, StringComparison.Ordinal);
        Assert.DoesNotContain("app.Use(", program, StringComparison.Ordinal);
        // Guards the test itself: the file must still do its job through the setup.
        Assert.Contains("AddApiServices", program, StringComparison.Ordinal);
        Assert.Contains("UseApiPipeline", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Rule2_TheRootOfTheApi_HoldsOnlyProgramSettingsAndTheProjectFile()
    {
        var root = Path.Combine(RepositoryFiles.Root, ApiFolder);
        var files = Directory.EnumerateFiles(root).Select(Path.GetFileName).OfType<string>().ToList();

        // Guards the test itself: the folder exists and has its files.
        Assert.Contains("Program.cs", files);

        var offenders = files.Where(name =>
            name != "Program.cs"
            && !name.EndsWith(".csproj", StringComparison.Ordinal)
            && !(name.StartsWith("appsettings", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal)));

        Assert.Empty(offenders);
    }

    [Fact]
    public void Rule3_RoutesAreOnlyInControllers_NoMinimalApiRoutes()
    {
        var sources = ApiSources().ToList();
        // Guards the test itself: the Api has many source files.
        Assert.True(sources.Count > 20, $"Only {sources.Count} source files were found; the filter looks wrong.");

        var minimalApi = sources
            .Where(path => MinimalApiRoute().IsMatch(File.ReadAllText(path)))
            .Select(RepositoryFiles.Relative)
            .ToList();
        var mapsOutsideTheSetup = sources
            .Where(path => !RepositoryFiles.Relative(path).StartsWith($"{ApiFolder}/Setup/", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains(".MapControllers(", StringComparison.Ordinal)
                || File.ReadAllText(path).Contains(".MapOpenApi(", StringComparison.Ordinal))
            .Select(RepositoryFiles.Relative)
            .ToList();

        Assert.Empty(minimalApi);
        Assert.Empty(mapsOutsideTheSetup);
    }

    [Fact]
    public void Rule6_APutDeclaresTheNewState_AndADeleteAnswersWithNoBody()
    {
        var actions = Actions().ToList();
        // Guards the test itself.
        Assert.True(actions.Count(a => a.Verb == "PUT") > 5 && actions.Count(a => a.Verb == "DELETE") > 1,
            "Too few PUT or DELETE actions were found; the filter looks wrong.");

        var puts = actions
            .Where(a => a.Verb == "PUT")
            .Where(a => !a.SuccessCodes.Contains(StatusCodes.Status200OK) || a.SuccessCodes.Contains(StatusCodes.Status204NoContent))
            .Select(a => $"{a.Name} (PUT) declares {string.Join(", ", a.SuccessCodes)}; a PUT answers 200 with the new state")
            .ToList();
        var deletes = actions
            .Where(a => a.Verb == "DELETE" && !DeleteReturningTheNewState.Contains(a.Name))
            .Where(a => !a.SuccessCodes.SetEquals([StatusCodes.Status204NoContent]))
            .Select(a => $"{a.Name} (DELETE) declares {string.Join(", ", a.SuccessCodes)}; a DELETE answers 204")
            .ToList();

        Assert.Empty(puts);
        Assert.Empty(deletes);
    }

    [Fact]
    public void Rule6_TheKnownDeleteException_IsStillThere()
    {
        // When the exception is paid (the delete answers 204), this test fails and the entry above must go with it.
        var known = Actions().Where(a => DeleteReturningTheNewState.Contains(a.Name)).ToList();

        Assert.Equal(DeleteReturningTheNewState.Count, known.Count);
        Assert.All(known, a => Assert.Contains(StatusCodes.Status200OK, a.SuccessCodes));
    }

    [Fact]
    public void Rule7_AnAction_HasNoTryCatch()
    {
        var controllers = ApiSources()
            .Where(path => RepositoryFiles.Relative(path).StartsWith($"{ApiFolder}/Controllers/", StringComparison.Ordinal))
            .ToList();
        Assert.True(controllers.Count > 5, "Too few controller files were found; the filter looks wrong.");

        var offenders = controllers
            .Where(path => TryOrCatch().IsMatch(File.ReadAllText(path)))
            .Select(RepositoryFiles.Relative)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Rule7_AControllerDoesNotTakeARepositoryOrADbContext()
    {
        var controllers = Controllers().ToList();
        Assert.True(controllers.Count > 5, "Too few controllers were found; the filter looks wrong.");

        var offenders = controllers
            .SelectMany(type => type.GetConstructors()
                .SelectMany(ctor => ctor.GetParameters())
                .Where(parameter => IsDataAccess(parameter.ParameterType))
                .Select(parameter => $"{type.Name} takes {parameter.ParameterType.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool IsDataAccess(Type type) =>
        type.Name.Contains("Repository", StringComparison.Ordinal)
        || type.Name.EndsWith("DbContext", StringComparison.Ordinal)
        || typeof(Microsoft.EntityFrameworkCore.DbContext).IsAssignableFrom(type);

    private static IEnumerable<string> ApiSources() =>
        RepositoryFiles.Find(ApiFolder, "*.cs");

    private static IEnumerable<Type> Controllers() =>
        ApiAssembly.GetTypes().Where(type => type.Namespace == ControllersNamespace && typeof(ControllerBase).IsAssignableFrom(type));

    private static IEnumerable<ActionInfo> Actions() =>
        Controllers()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(method => (Method: method, Http: method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).FirstOrDefault()))
            .Where(pair => pair.Http is not null)
            .Select(pair => new ActionInfo(
                $"{pair.Method.DeclaringType!.Name}.{pair.Method.Name}",
                pair.Http!.HttpMethods.Single(),
                pair.Method.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
                    .Select(attribute => attribute.StatusCode)
                    .Where(code => code is >= 200 and < 300)
                    .ToHashSet()));

    private sealed record ActionInfo(string Name, string Verb, HashSet<int> SuccessCodes);

    [GeneratedRegex(@"\.Map(Get|Post|Put|Delete|Patch)\s*\(")]
    private static partial Regex MinimalApiRoute();

    [GeneratedRegex(@"\b(try|catch)\b\s*[\{(]")]
    private static partial Regex TryOrCatch();
}
