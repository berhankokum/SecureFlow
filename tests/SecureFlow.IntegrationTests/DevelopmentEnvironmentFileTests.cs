using Microsoft.Extensions.Configuration;
using SecureFlow.Api.Configuration;

namespace SecureFlow.IntegrationTests;

public sealed class DevelopmentEnvironmentFileTests
{
    [Fact]
    public void Repository_dotenv_fills_only_missing_configuration_values()
    {
        var repositoryRoot = CreateTemporaryRepository();
        try
        {
            File.WriteAllText(
                Path.Combine(repositoryRoot, ".env"),
                """
                # local development values
                Existing__Value="from-dotenv"
                ConnectionStrings__Postgres="Host=localhost;Password=value=with=equals"
                MALFORMED_LINE
                """);

            var configuration = new ConfigurationManager
            {
                ["Existing:Value"] = "higher-precedence-value",
            };

            DevelopmentEnvironmentFile.AddMissingValues(
                configuration,
                Path.Combine(repositoryRoot, "src", "SecureFlow.Api"));

            Assert.Equal("higher-precedence-value", configuration["Existing:Value"]);
            Assert.Equal(
                "Host=localhost;Password=value=with=equals",
                configuration.GetConnectionString("Postgres"));
        }
        finally
        {
            Directory.Delete(repositoryRoot, recursive: true);
        }
    }

    [Fact]
    public void Dotenv_without_the_solution_marker_is_not_trusted()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "secureflow-untrusted-dotenv-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, ".env"), "ConnectionStrings__Postgres=untrusted");
            var configuration = new ConfigurationManager();

            DevelopmentEnvironmentFile.AddMissingValues(
                configuration,
                directory,
                applicationBasePath: directory);

            Assert.Null(configuration.GetConnectionString("Postgres"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryRepository()
    {
        var repositoryRoot = Path.Combine(
            Path.GetTempPath(),
            "secureflow-dotenv-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(repositoryRoot, "src", "SecureFlow.Api"));
        File.WriteAllText(Path.Combine(repositoryRoot, "SecureFlow.slnx"), "<Solution />");
        return repositoryRoot;
    }
}
