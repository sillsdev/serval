namespace Serval.Machine.Shared.Services;

public class ClearMLHealthCheck(
    IClearMLAuthenticationService clearMLAuthenticationService,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IOptionsMonitor<BuildJobOptions> buildJobOptions
) : IHealthCheck
{
    private const string FailureCountKey = "ClearMLHealthCheck.ConsecutiveFailures";
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("ClearML-NoRetry");
    private readonly ISet<string> _queuesMonitored = buildJobOptions
        .CurrentValue.ClearML.Select(x => x.Queue)
        .ToHashSet();

    private readonly AsyncLock _lock = new AsyncLock();

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!await PingAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("ClearML is unresponsive");
            (QueueHealth queueHealth, ICollection<string> queuesWithoutWorkers) = await QueuesWithoutWorkers(
                cancellationToken
            );
            switch (queueHealth)
            {
                case QueueHealth.Degraded:
                    return HealthCheckResult.Degraded(
                        $"No ClearML agents are available for configured queues: {string.Join(", ", queuesWithoutWorkers)}"
                    );
                case QueueHealth.Unhealthy:
                    return HealthCheckResult.Unhealthy(
                        $"Autoscaler is down and there are no ClearML agents are available for configured queues: {string.Join(", ", queuesWithoutWorkers)}"
                    );
                case QueueHealth.Healthy:
                default:
                    using (await _lock.LockAsync(cancellationToken))
                        cache.Set(FailureCountKey, 0);
                    return HealthCheckResult.Healthy("ClearML is available");
            }
        }
        catch (Exception e)
        {
            using (await _lock.LockAsync(cancellationToken))
            {
                int numConsecutiveFailures = cache.Get<int>(FailureCountKey);
                cache.Set(FailureCountKey, ++numConsecutiveFailures);
                return numConsecutiveFailures > 3
                    ? HealthCheckResult.Unhealthy(exception: e)
                    : HealthCheckResult.Degraded(exception: e);
            }
        }
    }

    private async Task<JsonObject?> CallAsync(
        string service,
        string action,
        JsonNode body,
        CancellationToken cancellationToken = default
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{service}.{action}")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(
            "Authorization",
            $"Bearer {await clearMLAuthenticationService.GetAuthTokenAsync(cancellationToken)}"
        );
        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string result = await response.Content.ReadAsStringAsync(cancellationToken);
        return (JsonObject?)JsonNode.Parse(result);
    }

    private async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        JsonObject? result = await CallAsync("debug", "ping", new JsonObject(), cancellationToken);
        return result is not null;
    }

    private async Task<(QueueHealth, ICollection<string>)> QueuesWithoutWorkers(
        CancellationToken cancellationToken = default
    )
    {
        var queuesWithoutWorkers = _queuesMonitored.ToHashSet();
        JsonObject? queuesResult = await CallAsync("workers", "get_all", new JsonObject(), cancellationToken);
        JsonNode? workersNode = queuesResult?["data"]?["workers"];
        if (workersNode is null)
            throw new InvalidOperationException("Malformed response from ClearML server.");
        var workers = (JsonArray)workersNode;
        foreach (JsonNode? worker in workers)
        {
            JsonNode? queuesNode = worker?["queues"];
            if (queuesNode is null)
                continue;
            var queues = (JsonArray)queuesNode;
            foreach (JsonNode? currentQueue in queues)
            {
                string? currentQueueName = (string?)currentQueue?["name"];
                if (currentQueueName is not null)
                    queuesWithoutWorkers.Remove(currentQueueName);
            }
        }

        // Ensure the autoscaler queues are present, if there are queues without workers
        if (queuesWithoutWorkers.Count > 0)
        {
            JsonObject? instancesResult = await CallAsync(
                "apps",
                "get_instances",
                new JsonObject { ["app"] = "gcp-autoscaler", ["status"] = "running" },
                cancellationToken
            );

            // Get the autoscaler queues
            HashSet<string> autoscalerQueueNames = [];
            JsonNode? instancesNode = instancesResult?["data"]?["instances"];
            if (instancesNode is null)
                throw new InvalidOperationException("Malformed response from ClearML server.");
            var instances = (JsonArray)instancesNode;
            foreach (JsonNode? instance in instances)
            {
                if (instance?["application"]?["configuration"]?["instance_queue_list"] is JsonArray queues)
                {
                    foreach (JsonNode? queueNode in queues)
                    {
                        string? queueName = queueNode?["queue_name"]?.ToString();
                        if (!string.IsNullOrEmpty(queueName))
                            autoscalerQueueNames.Add(queueName);
                    }
                }
            }

            // We do not check for workers, as autoscaler queues can have no workers but still be healthy
            if (autoscalerQueueNames.Count == 0)
                return (QueueHealth.Unhealthy, queuesWithoutWorkers);

            return (QueueHealth.Degraded, queuesWithoutWorkers);
        }

        return (QueueHealth.Healthy, queuesWithoutWorkers);
    }

    private enum QueueHealth
    {
        Healthy,
        Degraded,
        Unhealthy,
    }
}
