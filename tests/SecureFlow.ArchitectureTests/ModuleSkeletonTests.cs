using SecureFlow.SharedKernel;

namespace SecureFlow.ArchitectureTests;

/// <summary>
/// FAZ 1: modüller henüz boş olduğu için yalnızca solution iskeletinin
/// beklenen assembly'leri ürettiğini doğrular. Domain/Application/Infrastructure
/// katman sınırı kuralları, modüller gerçek kod içerdikçe (FAZ 2+) buraya eklenir.
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

    public static TheoryData<string> ModuleNames() => [.. ExpectedModules];
}
