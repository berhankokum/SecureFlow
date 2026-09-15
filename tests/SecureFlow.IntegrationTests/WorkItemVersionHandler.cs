namespace SecureFlow.IntegrationTests;

// Legacy feature tests represent a user who has just loaded the resource.
// Concurrency tests explicitly supply stale headers or opt out of this helper.
internal sealed class WorkItemVersionHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (path.Length is 4 or 5 && path[0] == "api" && path[1] == "v1" && path[2] == "work-items"
            && Guid.TryParse(path[3], out _)
            && (request.Method == HttpMethod.Patch && path.Length == 4
                || path.Length == 5 && path[4] is "archive" or "assign" or "self-assign" or "assignment" or "transition")
            && request.Method != HttpMethod.Get && !request.Headers.Contains("If-Match"))
        {
            using var read = new HttpRequestMessage(HttpMethod.Get, new Uri(request.RequestUri, $"/api/v1/work-items/{path[3]}"));
            if (request.Headers.TryGetValues("Cookie", out var cookies)) read.Headers.TryAddWithoutValidation("Cookie", cookies);
            using var response = await base.SendAsync(read, cancellationToken);
            if (response.Headers.ETag is { } tag) request.Headers.IfMatch.Add(tag);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
