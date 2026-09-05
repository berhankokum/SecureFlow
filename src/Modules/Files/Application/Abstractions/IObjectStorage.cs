namespace SecureFlow.Modules.Files.Application.Abstractions;

public sealed record StoredObject(Stream Content, string ContentType, long Length);

public interface IObjectStorage
{
    Task EnsureBucketAsync(CancellationToken cancellationToken);

    Task PutAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);

    Task<StoredObject?> GetAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
