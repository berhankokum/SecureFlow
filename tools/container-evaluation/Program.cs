using System.Net;
using System.Security.Cryptography;
using System.Text;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Options;
using Npgsql;
using SecureFlow.Modules.Files.Infrastructure;
using Testcontainers.PostgreSql;

// Opt-in evaluation, not in the application solution or default CI. No host volume mounts.
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var ct = timeout.Token;
if (args.Length != 1 || args[0] is not ("postgres" or "garage" or "seaweedfs" or "rustfs"))
    throw new ArgumentException("Choose postgres, garage, seaweedfs or rustfs.");

if (args[0] == "postgres")
{
    await using var postgres = new PostgreSqlBuilder("secureflow-postgres-evaluation:20260917")
        .WithDatabase("evaluation").WithUsername("evaluation")
        .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(24))).Build();
    await postgres.StartAsync(ct);
    var privilege = await postgres.ExecAsync(["gosu", "postgres", "id", "-u"], ct);
    Check(privilege.ExitCode == 0 && privilege.Stdout.Trim() != "0", "gosu drops root");
    await using (var connection = new NpgsqlConnection(postgres.GetConnectionString()))
    {
        await connection.OpenAsync(ct);
        await new NpgsqlCommand("CREATE TABLE probe(id int primary key); INSERT INTO probe VALUES (1)", connection).ExecuteNonQueryAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await new NpgsqlCommand("INSERT INTO probe VALUES (2)", connection, transaction).ExecuteNonQueryAsync(ct);
        await transaction.RollbackAsync(ct);
    }
    await postgres.StopAsync(ct);
    await postgres.StartAsync(ct);
    await using var reopened = new NpgsqlConnection(postgres.GetConnectionString());
    await reopened.OpenAsync(ct);
    Check(Convert.ToInt64(await new NpgsqlCommand("SELECT count(*) FROM probe", reopened).ExecuteScalarAsync(ct)) == 1,
        "committed data survives restart; rollback does not persist");
    Console.WriteLine("PASS PostgreSQL startup, privilege drop, transaction rollback and restart persistence.");
    return;
}

var accessKey = "GK" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
var secretKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
const string bucket = "secureflow-evaluation";
var config = $$"""
metadata_dir = "/tmp/meta"
data_dir = "/tmp/data"
db_engine = "sqlite"
replication_factor = 1
rpc_bind_addr = "127.0.0.1:3901"
rpc_public_addr = "127.0.0.1:3901"
rpc_secret = "{{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}}"
[s3_api]
s3_region = "garage"
api_bind_addr = "0.0.0.0:3900"
""";
var useSeaweed = args[0] == "seaweedfs";
var useRust = args[0] == "rustfs";
var storagePort = useRust ? 9000 : useSeaweed ? 8333 : 3900;
var region = useSeaweed || useRust ? "us-east-1" : "garage";
var candidateBuilder = useRust
    ? new ContainerBuilder("rustfs/rustfs:1.0.0@sha256:8cc9801755448b71a786705ce76692c77e14936cccd87cf2fc31842e58f4d1ff")
        .WithEnvironment("RUSTFS_ACCESS_KEY", accessKey)
        .WithEnvironment("RUSTFS_SECRET_KEY", secretKey)
        .WithEnvironment("RUSTFS_CONSOLE_ENABLE", "false")
        .WithCommand("/data")
    : useSeaweed
    ? new ContainerBuilder("chrislusf/seaweedfs:4.47@sha256:ce9e796f1fe6f06968f4c04bdaf8f678dad9c8acdfef3d244133d71bfa6bf882")
        .WithEnvironment("AWS_ACCESS_KEY_ID", accessKey)
        .WithEnvironment("AWS_SECRET_ACCESS_KEY", secretKey)
        .WithEnvironment("S3_BUCKET", bucket)
        .WithCommand("mini", "-dir=/data")
    : new ContainerBuilder("dxflrs/garage:v2.3.0@sha256:866bd13ed2038ba7e7190e840482bc27234c4afaf77be8cfa439ae088c1e4690")
    .WithResourceMapping(Encoding.UTF8.GetBytes(config), "/etc/garage.toml")
    .WithEnvironment("GARAGE_DEFAULT_ACCESS_KEY", accessKey)
    .WithEnvironment("GARAGE_DEFAULT_SECRET_KEY", secretKey)
    .WithEnvironment("GARAGE_DEFAULT_BUCKET", bucket)
    .WithCommand("/garage", "server", "--single-node", "--default-bucket");
await using var garage = candidateBuilder.WithPortBinding(storagePort, true).Build();
await garage.StartAsync(ct);
var endpoint = $"http://{garage.Hostname}:{garage.GetMappedPublicPort(storagePort)}";
using var client = new AmazonS3Client(accessKey, secretKey, new AmazonS3Config
{
    ServiceURL = endpoint,
    ForcePathStyle = true,
    AuthenticationRegion = region,
});
var storage = new S3ObjectStorage(client, Options.Create(new ObjectStorageOptions
{
    BucketName = bucket,
    CreateBucketIfMissing = useRust,
}));
for (var attempt = 0; ; attempt++)
{
    try { await storage.EnsureBucketAsync(ct); break; }
    catch when (attempt < 20 && !ct.IsCancellationRequested) { await Task.Delay(500, ct); }
}
const string key = "tenant/project/evaluation.txt";
await using var payload = new MemoryStream(Encoding.UTF8.GetBytes("isolated-storage-check"));
await storage.PutAsync(key, payload, "text/plain", ct);
var stored = await storage.GetAsync(key, ct);
Check(stored is not null, "object exists");
using (var reader = new StreamReader(stored!.Content))
    Check(await reader.ReadToEndAsync(ct) == "isolated-storage-check", "object bytes match");
using var http = new HttpClient();
using var anonymous = await http.GetAsync($"{endpoint}/{bucket}/{key}", ct);
Check(anonymous.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized, "anonymous read denied");
using var invalidClient = new AmazonS3Client(accessKey, "invalid-" + secretKey, new AmazonS3Config
{
    ServiceURL = endpoint,
    ForcePathStyle = true,
    AuthenticationRegion = region,
});
try
{
    using var unauthorized = await invalidClient.GetObjectAsync(bucket, key, ct);
    throw new InvalidOperationException("Wrong secret unexpectedly allowed object access.");
}
catch (AmazonS3Exception exception) when (exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
{
    // Authentication must reject the incorrect signature, not merely hide the object.
}
var largeBytes = RandomNumberGenerator.GetBytes(10 * 1024 * 1024);
await using var largePayload = new MemoryStream(largeBytes);
await storage.PutAsync("tenant/project/large.bin", largePayload, "application/octet-stream", ct);
var largeObject = await storage.GetAsync("tenant/project/large.bin", ct);
Check(largeObject is not null && largeObject.Length == largeBytes.Length, "10 MiB object length");
await using (var content = largeObject!.Content)
    Check((await SHA256.HashDataAsync(content, ct)).SequenceEqual(SHA256.HashData(largeBytes)), "10 MiB byte integrity");
await storage.DeleteAsync("tenant/project/large.bin", ct);
Check(await storage.GetAsync("tenant/project/large.bin", ct) is null, "large object deletion");
await storage.DeleteAsync(key, ct);
Check(await storage.GetAsync(key, ct) is null, "deleted object is missing");
Console.WriteLine($"PASS {args[0]} bucket, actual S3 adapter put/get/delete/missing, anonymous/wrong-secret denial and 10 MiB integrity.");

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
