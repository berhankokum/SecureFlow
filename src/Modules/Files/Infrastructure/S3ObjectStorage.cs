using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Options;
using SecureFlow.Modules.Files.Application.Abstractions;

namespace SecureFlow.Modules.Files.Infrastructure;

public sealed class S3ObjectStorage(
    IAmazonS3 client,
    IOptions<ObjectStorageOptions> options) : IObjectStorage
{
    private readonly ObjectStorageOptions _options = options.Value;

    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (await AmazonS3Util.DoesS3BucketExistV2Async(client, _options.BucketName))
        {
            return;
        }

        if (!_options.CreateBucketIfMissing)
        {
            throw new InvalidOperationException("Configured object storage bucket does not exist.");
        }

        await client.PutBucketAsync(
            new PutBucketRequest { BucketName = _options.BucketName },
            cancellationToken);
    }

    public async Task PutAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = storageKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, cancellationToken);
    }

    public async Task<StoredObject?> GetAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetObjectAsync(
                _options.BucketName,
                storageKey,
                cancellationToken);
            return new StoredObject(
                response.ResponseStream,
                response.Headers.ContentType ?? "application/octet-stream",
                response.ContentLength);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) =>
        client.DeleteObjectAsync(_options.BucketName, storageKey, cancellationToken);
}
