namespace Serval.Aqua.Shared.Services;

public class AquaAccessTokenHandler(IAquaAuthService aquaAuthService) : DelegatingHandler
{
    private readonly IAquaAuthService _aquaAuthService = aquaAuthService;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _policy = Policy
        .HandleResult<HttpResponseMessage>(r => r.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        .RetryAsync((_, _) => aquaAuthService.RefreshAccessTokenAsync());

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        return _policy.ExecuteAsync(async () =>
        {
            string accessToken = await _aquaAuthService.GetAccessTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return await base.SendAsync(request, cancellationToken);
        });
    }
}
