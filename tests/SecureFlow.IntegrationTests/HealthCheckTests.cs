using System.Net;
namespace SecureFlow.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class HealthCheckTests(AuthWebApplicationFactory factory)
{
    [Fact]
    public async Task Liveness_endpoint_returns_ok_without_checking_dependencies()
    {
        var client = factory.CreateSecureClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_endpoint_returns_ok_with_isolated_postgresql()
    {
        var client = factory.CreateSecureClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
