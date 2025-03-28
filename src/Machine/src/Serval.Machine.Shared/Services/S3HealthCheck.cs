namespace Serval.Machine.Shared.Services;

public class S3HealthCheck(IOptions<SharedFileOptions> options) : IHealthCheck
{
    private readonly IOptions<SharedFileOptions> _options = options;
    private int _numConsecutiveFailures = 0;
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
                BucketName = new Uri(_options.Value.Uri).Host,
                Prefix = new Uri(_options.Value.Uri).AbsolutePath.TrimStart('/'),
                MaxKeys = 1,
                Delimiter = ""
            };

            await new AmazonS3Client(
                _options.Value.S3AccessKeyId,
                _options.Value.S3SecretAccessKey,
                new AmazonS3Config
                {
                    MaxErrorRetry = 0, //Do not let health check hang
                    RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Value.S3Region)
                }
            ).ListObjectsV2Async(request, cancellationToken);
            using (await _lock.LockAsync(cancellationToken))
                _numConsecutiveFailures = 0;
            return HealthCheckResult.Healthy("The S3 bucket is available");
        }
        catch (Exception e)
        {
            using (await _lock.LockAsync(cancellationToken))
            {
                _numConsecutiveFailures++;
                if (
                    e is HttpRequestException httpRequestException
                    && httpRequestException.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
                )
                {
                    return _numConsecutiveFailures > 3
                        ? HealthCheckResult.Unhealthy(
                            "S3 bucket is not available because of an authentication error. Please verify that credentials are valid."
                        )
                        : HealthCheckResult.Degraded(
                            "S3 bucket is not available because of an authentication error. Please verify that credentials are valid."
                        );
                }
                return _numConsecutiveFailures > 3
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
