using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureFlow.IntegrationTests;

public class HealthCheckTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Liveness_endpoint_returns_ok_without_checking_dependencies()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
