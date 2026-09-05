using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SecureFlow.Modules.Files.Application;
using SecureFlow.Modules.Files.Application.Abstractions;
using SecureFlow.Modules.Files.Infrastructure;
using SecureFlow.SharedKernel;

namespace SecureFlow.Modules.Files;

public static class FilesModule
{
    public static IServiceCollection AddFilesModule<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TDbContext : class, IFilesDbContext
    {
        services.AddScoped<IFilesDbContext>(provider => provider.GetRequiredService<TDbContext>());
        services.AddScoped<FileScopeHandler>();
        services.AddScoped<AttachmentHandlers>();
        services.TryAddSingleton<IClock, SystemClock>();

        services.AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetSection(ObjectStorageOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                "ObjectStorage:ServiceUrl must be an absolute HTTP(S) URL.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AccessKey),
                "ObjectStorage:AccessKey is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SecretKey),
                "ObjectStorage:SecretKey is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BucketName),
                "ObjectStorage:BucketName is required.")
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
            var serviceUri = new Uri(options.ServiceUrl);
            return new AmazonS3Client(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = options.ServiceUrl,
                    ForcePathStyle = true,
                    UseHttp = serviceUri.Scheme == Uri.UriSchemeHttp,
                    AuthenticationRegion = options.Region,
                });
        });
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        services.AddHostedService<ObjectStorageBucketInitializer>();
        services.AddHealthChecks()
            .AddCheck<ObjectStorageHealthCheck>("object-storage", tags: ["ready"]);
        return services;
    }
}
