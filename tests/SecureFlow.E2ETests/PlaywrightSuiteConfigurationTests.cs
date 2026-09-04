namespace SecureFlow.E2ETests;

/// <summary>
/// Browser E2E testleri frontend/e2e altında Playwright ile çalışır. Bu repository
/// contract testi suite'in yanlışlıkla kaldırılmasını engeller.
/// </summary>
public class PlaywrightSuiteConfigurationTests
{
    [Fact]
    public void Playwright_auth_security_suite_should_exist()
    {
        var repositoryRoot = FindRepositoryRoot();

        Assert.True(File.Exists(Path.Combine(repositoryRoot, "frontend", "playwright.config.ts")));
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "frontend", "e2e", "auth-security.spec.ts")));
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "frontend", "e2e", "organization-flow.spec.ts")));
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "frontend", "e2e", "membership-invitation.spec.ts")));
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "frontend", "e2e", "team-flow.spec.ts")));
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "frontend", "e2e", "project-flow.spec.ts")));
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "frontend", "e2e", "work-item-flow.spec.ts")));
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "frontend", "e2e", "label-flow.spec.ts")));
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "frontend", "e2e", "assignment-flow.spec.ts")));
        Assert.True(File.Exists(Path.Combine(repositoryRoot, "frontend", "e2e", "workflow-kanban.spec.ts")));
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startingPath in new[]
                 {
                     Path.GetDirectoryName(sourceFilePath)!,
                     Directory.GetCurrentDirectory(),
                     AppContext.BaseDirectory
                 })
        {
            var directory = new DirectoryInfo(startingPath);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "SecureFlow.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("SecureFlow repository root could not be located.");
    }
}
