using SecureFlow.SharedKernel;

namespace SecureFlow.ArchitectureTests;

/// <summary>
/// Modüler monolith sınırlarını harici bir architecture-test framework'ü eklemeden
/// reflection üzerinden doğrular.
/// </summary>
public class ModuleSkeletonTests
{
    private static readonly string[] ExpectedModules =
    [
        "SecureFlow.Modules.Identity",
        "SecureFlow.Modules.Organizations",
        "SecureFlow.Modules.Teams",
        "SecureFlow.Modules.Projects",
        "SecureFlow.Modules.WorkItems",
        "SecureFlow.Modules.Collaboration",
        "SecureFlow.Modules.Notifications",
        "SecureFlow.Modules.Files",
        "SecureFlow.Modules.Audit",
        "SecureFlow.Modules.Outbox",
    ];

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Every_expected_module_assembly_should_load(string assemblyName)
    {
        var assembly = System.Reflection.Assembly.Load(assemblyName);

        Assert.NotNull(assembly);
    }

    [Fact]
    public void SharedKernel_should_be_loadable_from_a_module_assembly()
    {
        var assembly = typeof(Entity).Assembly;

        Assert.Equal("SecureFlow.SharedKernel", assembly.GetName().Name);
    }

    [Fact]
    public void Modules_should_not_reference_other_module_assemblies()
    {
        foreach (var moduleName in ExpectedModules)
        {
            var assembly = System.Reflection.Assembly.Load(moduleName);
            var forbiddenReferences = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name is not null
                    && name.StartsWith("SecureFlow.Modules.", StringComparison.Ordinal)
                    && !string.Equals(name, moduleName, StringComparison.Ordinal))
                .ToArray();

            Assert.True(
                forbiddenReferences.Length == 0,
                $"{moduleName} references other modules: {string.Join(", ", forbiddenReferences)}");
        }
    }

    [Fact]
    public void SharedKernel_should_not_reference_application_or_infrastructure_assemblies()
    {
        var references = typeof(Entity).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name =>
                name.StartsWith("SecureFlow.", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || name.StartsWith("Npgsql", StringComparison.Ordinal)
                || name.StartsWith("StackExchange.Redis", StringComparison.Ordinal))
            .ToArray();

        Assert.True(references.Length == 0, $"SharedKernel has forbidden references: {string.Join(", ", references)}");
    }

    [Fact]
    public void Domain_public_surface_should_not_expose_http_persistence_cache_or_signalr_types()
    {
        string[] forbiddenNamespacePrefixes =
        [
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.SignalR",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "StackExchange.Redis",
        ];

        var violations = ExpectedModules
            .Select(System.Reflection.Assembly.Load)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace?.Contains(".Domain", StringComparison.Ordinal) == true)
            .SelectMany(type => PublicSurfaceTypes(type).Select(dependency => (Type: type, Dependency: dependency)))
            .Where(pair => forbiddenNamespacePrefixes.Any(prefix =>
                pair.Dependency.Namespace?.StartsWith(prefix, StringComparison.Ordinal) == true))
            .Select(pair => $"{pair.Type.FullName} -> {pair.Dependency.FullName}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0, $"Domain boundary violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static IEnumerable<Type> PublicSurfaceTypes(Type type)
    {
        if (type.BaseType is not null)
        {
            yield return type.BaseType;
        }

        foreach (var implementedInterface in type.GetInterfaces())
        {
            yield return implementedInterface;
        }

        foreach (var constructor in type.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return Unwrap(parameter.ParameterType);
            }
        }

        foreach (var property in type.GetProperties())
        {
            yield return Unwrap(property.PropertyType);
        }

        foreach (var method in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
        {
            yield return Unwrap(method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                yield return Unwrap(parameter.ParameterType);
            }
        }
    }

    private static Type Unwrap(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType()!;
        }

        return Nullable.GetUnderlyingType(type) ?? type;
    }

    public static TheoryData<string> ModuleNames() => [.. ExpectedModules];

    [Fact]
    public void Domain_surface_generic_arguments_cannot_hide_infrastructure_dependencies()
    {
        var violations = ExpectedModules.Select(System.Reflection.Assembly.Load).SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.Contains(".Domain", StringComparison.Ordinal) == true)
            .SelectMany(t => PublicSurfaceTypes(t).SelectMany(ExpandType).Select(d => (Owner: t, Dependency: d)))
            .Where(pair => pair.Dependency.Namespace is { } ns &&
                (ns.Contains(".Infrastructure", StringComparison.Ordinal)
                || ns.Contains(".Presentation", StringComparison.Ordinal)
                || ns.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                || ns.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)))
            // The specification deliberately uses ASP.NET IdentityUser<Guid>.
            // Allow only that exact base type on the Identity module's user entity.
            .Where(pair => !(pair.Owner.FullName == "SecureFlow.Modules.Identity.Domain.ApplicationUser"
                && pair.Dependency.Namespace == "Microsoft.AspNetCore.Identity"
                && pair.Dependency.Name == "IdentityUser`1"))
            .Select(pair => $"{pair.Owner.FullName} -> {pair.Dependency.FullName}").ToArray();
        Assert.Empty(violations);
    }

    [Fact]
    public void Dependency_inspector_expands_nested_generic_and_array_arguments()
    {
        var expanded = ExpandType(typeof(Task<Dictionary<string, List<int[]>>>)).ToArray();
        Assert.Contains(typeof(int), expanded);
        Assert.Contains(typeof(string), expanded);
        Assert.Contains(typeof(List<int[]>), expanded);
    }

    private static IEnumerable<Type> ExpandType(Type type)
    {
        yield return type;
        if (type.HasElementType)
            foreach (var nested in ExpandType(type.GetElementType()!)) yield return nested;
        foreach (var argument in type.GetGenericArguments())
            foreach (var nested in ExpandType(argument)) yield return nested;
    }
}
