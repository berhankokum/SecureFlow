using Microsoft.Extensions.Configuration;

namespace SecureFlow.Api.Configuration;

public static class DevelopmentEnvironmentFile
{
    public static void AddMissingValues(
        ConfigurationManager configuration,
        string contentRootPath,
        string? applicationBasePath = null)
    {
        var environmentFile = FindRepositoryEnvironmentFile(
            contentRootPath,
            applicationBasePath ?? AppContext.BaseDirectory);
        if (environmentFile is null)
        {
            return;
        }

        foreach (var line in File.ReadLines(environmentFile))
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmedLine.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var environmentKey = trimmedLine[..separatorIndex].Trim();
            var configurationKey = environmentKey.Replace("__", ":", StringComparison.Ordinal);

            // Command-line, real environment variables, user secrets and appsettings
            // keep precedence over local development defaults.
            if (!string.IsNullOrWhiteSpace(configuration[configurationKey]))
            {
                continue;
            }

            var value = trimmedLine[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2
                && ((value[0] == '"' && value[^1] == '"')
                    || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            configuration[configurationKey] = value;
        }
    }

    private static string? FindRepositoryEnvironmentFile(
        string contentRootPath,
        string applicationBasePath)
    {
        string[] startingPaths = [contentRootPath, applicationBasePath];

        foreach (var startingPath in startingPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var directory = new DirectoryInfo(Path.GetFullPath(startingPath));
            while (directory is not null)
            {
                var solutionPath = Path.Combine(directory.FullName, "SecureFlow.slnx");
                var environmentPath = Path.Combine(directory.FullName, ".env");

                if (File.Exists(solutionPath) && File.Exists(environmentPath))
                {
                    return environmentPath;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
