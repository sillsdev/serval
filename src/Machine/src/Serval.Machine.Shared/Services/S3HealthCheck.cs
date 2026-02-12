namespace Serval.Machine.Shared.Services;

public class S3HealthCheck(IMemoryCache cache, IOptions<SharedFileOptions> options) : IHealthCheck
{
    private const string FailureCountKey = "S3HealthCheck.ConsecutiveFailures";
    private readonly AsyncLock _lock = new AsyncLock();

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = new Uri(options.Value.Uri).Host,
                Prefix = new Uri(options.Value.Uri).AbsolutePath.TrimStart('/'),
                MaxKeys = 1,
                Delimiter = string.Empty,
            };

            await new AmazonS3Client(
                options.Value.S3AccessKeyId,
                options.Value.S3SecretAccessKey,
                new AmazonS3Config
                {
                    MaxErrorRetry = 0, //Do not let health check hang
                    RegionEndpoint = RegionEndpoint.GetBySystemName(options.Value.S3Region),
                }
            ).ListObjectsV2Async(request, cancellationToken);
            using (await _lock.LockAsync(cancellationToken))
                cache.Set(FailureCountKey, 0);
            return HealthCheckResult.Healthy("The S3 bucket is available");
        }
        catch (Exception e)
        {
            using (await _lock.LockAsync(cancellationToken))
            {
                int numConsecutiveFailures = cache.Get<int>(FailureCountKey);
                cache.Set(FailureCountKey, ++numConsecutiveFailures);
                if (
                    e is HttpRequestException httpRequestException
                    && httpRequestException.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
                )
                {
                    return numConsecutiveFailures > 3
                        ? HealthCheckResult.Unhealthy(
                            "S3 bucket is not available because of an authentication error. Please verify that credentials are valid."
                        )
                        : HealthCheckResult.Degraded(
                            "S3 bucket is not available because of an authentication error. Please verify that credentials are valid."
                        );
                }
                return numConsecutiveFailures > 3
                    ? HealthCheckResult.Unhealthy(
                        "S3 bucket is not available. The following exception occurred: " + e.Message
                    )
                    : HealthCheckResult.Degraded(
                        "S3 bucket is not available. The following exception occurred: " + e.Message
                    );
            }
        }
    }
}
