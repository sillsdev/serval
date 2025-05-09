﻿namespace Serval.Machine.Shared.Services;

public class ClearMLService(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<ClearMLOptions> options,
    IClearMLAuthenticationService clearMLAuthService,
    IHostEnvironment env,
    ILogger<ClearMLService> logger
) : IClearMLService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("ClearML");
    private readonly IOptionsMonitor<ClearMLOptions> _options = options;
    private readonly IHostEnvironment _env = env;
    private static readonly JsonNamingPolicy JsonNamingPolicy = new SnakeCaseJsonNamingPolicy();
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy,
            Converters = { new CustomEnumConverterFactory(JsonNamingPolicy) }
        };

    private readonly IClearMLAuthenticationService _clearMLAuthService = clearMLAuthService;
    private readonly ILogger<ClearMLService> _logger = logger;
    private readonly AsyncLock _lock = new AsyncLock();
    private ImmutableDictionary<string, string>? _queueNamesToIds = null;

    public async Task<string?> GetProjectIdAsync(string name, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["name"] = $"{_options.CurrentValue.RootProject}/{_options.CurrentValue.Project}/{name}",
            ["only_fields"] = new JsonArray("id")
        };
        JsonObject? result = await CallAsync("projects", "get_all", body, cancellationToken);
        var projects = (JsonArray?)result?["data"]?["projects"];
        if (projects is null)
            throw new InvalidOperationException("Malformed response from ClearML server.");
        if (projects.Count == 0)
            return null;
        return (string?)projects[0]?["id"];
    }

    public async Task<string> CreateProjectAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default
    )
    {
        var body = new JsonObject
        {
            ["name"] = $"{_options.CurrentValue.RootProject}/{_options.CurrentValue.Project}/{name}"
        };
        if (description != null)
            body["description"] = description;
        JsonObject? result = await CallAsync("projects", "create", body, cancellationToken);
        var projectId = (string?)result?["data"]?["id"];
        if (projectId is null)
            throw new InvalidOperationException("Malformed response from ClearML server.");
        return projectId;
    }

    public async Task<bool> DeleteProjectAsync(string id, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["project"] = id,
            ["delete_contents"] = true,
            ["force"] = true // needed if there are tasks already in that project.
        };
        JsonObject? result = await CallAsync("projects", "delete", body, cancellationToken);
        var deleted = (int?)result?["data"]?["deleted"];
        if (deleted is null)
            throw new InvalidOperationException("Malformed response from ClearML server.");
        return deleted == 1;
    }

    public async Task<string> CreateTaskAsync(
        string buildId,
        string projectId,
        string script,
        string dockerImage,
        CancellationToken cancellationToken = default
    )
    {
        var snakeCaseEnvironment = JsonNamingPolicy.ConvertName(_env.EnvironmentName);
        var body = new JsonObject
        {
            ["name"] = buildId,
            ["project"] = projectId,
            ["script"] = new JsonObject { ["diff"] = script },
            ["container"] = new JsonObject
            {
                ["image"] = dockerImage,
                ["arguments"] = "--env ENV_FOR_DYNACONF=" + snakeCaseEnvironment,
            },
            ["type"] = "training"
        };
        JsonObject? result = await CallAsync("tasks", "create", body, cancellationToken);
        var taskId = (string?)result?["data"]?["id"];
        if (taskId is null)
            throw new InvalidOperationException("Malformed response from ClearML server.");
        return taskId;
    }

    public async Task<bool> DeleteTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["task"] = id };
        JsonObject? result = await CallAsync("tasks", "delete", body, cancellationToken);
        var deleted = (bool?)result?["data"]?["deleted"];
        if (deleted is null)
            throw new InvalidOperationException("Malformed response from ClearML server.");
        return deleted.Value;
    }

    public async Task<bool> EnqueueTaskAsync(string id, string queue, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["task"] = id, ["queue_name"] = queue };
        JsonObject? result = await CallAsync("tasks", "enqueue", body, cancellationToken);
        var queued = (int?)result?["data"]?["queued"];
        if (queued is null)
            throw new InvalidOperationException("Malformed response from ClearML server.");
        return queued == 1;
    }

    public async Task<bool> DequeueTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["task"] = id };
        JsonObject? result = await CallAsync("tasks", "dequeue", body, cancellationToken);
        var dequeued = (int?)result?["data"]?["dequeued"];
        if (dequeued is null)
            throw new InvalidOperationException("Malformed response from ClearML server.");
        return dequeued == 1;
    }

    public async Task<bool> StopTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["task"] = id, ["force"] = true };
        JsonObject? result = await CallAsync("tasks", "stop", body, cancellationToken);
        var updated = (int?)result?["data"]?["updated"];
        if (updated is null)
            throw new InvalidOperationException("Malformed response from ClearML server.");
        return updated == 1;
    }

    public async Task<IReadOnlyList<ClearMLTask>> GetTasksForQueueAsync(
        string queue,
        CancellationToken cancellationToken = default
    )
    {
        IDictionary<string, string> queueNamesToIds = await PopulateQueueNamesToIdsAsync(
            cancellationToken: cancellationToken
        );
        if (!queueNamesToIds.TryGetValue(queue, out string? queueId))
        {
            queueNamesToIds = await PopulateQueueNamesToIdsAsync(refresh: true, cancellationToken);
            if (!queueNamesToIds.TryGetValue(queue, out queueId))
            {
                throw new InvalidOperationException($"Queue {queue} does not exist");
            }
        }
        var body = new JsonObject { ["queue"] = queueId };
        JsonObject? result = await CallAsync("queues", "get_by_id", body, cancellationToken);
        var tasks = (JsonArray?)result?["data"]?["queue"]?["entries"];
        IEnumerable<string> taskIds = tasks?.Select(t => (string)t?["task"]!) ?? new List<string>();
        return await GetTasksByIdAsync(taskIds, cancellationToken);
    }

    private async Task<IDictionary<string, string>> PopulateQueueNamesToIdsAsync(
        bool refresh = false,
        CancellationToken cancellationToken = default
    )
    {
        using (await _lock.LockAsync(cancellationToken))
        {
            if (!refresh && _queueNamesToIds != null)
                return _queueNamesToIds;
            JsonObject? result = await CallAsync("queues", "get_all", new JsonObject(), cancellationToken);
            var queues = (JsonArray?)result?["data"]?["queues"];
            if (queues is null)
                throw new InvalidOperationException("Malformed response from ClearML server.");

            _queueNamesToIds = queues.ToImmutableDictionary(q => (string)q!["name"]!, q => (string)q!["id"]!);
        }
        return _queueNamesToIds;
    }

    public async Task<ClearMLTask?> GetTaskByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ClearMLTask> tasks = await GetTasksAsync(new JsonObject { ["name"] = name }, cancellationToken);
        if (tasks.Count == 0)
            return null;
        return tasks[0];
    }

    public Task<IReadOnlyList<ClearMLTask>> GetTasksByIdAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default
    )
    {
        string[] idArray = ids.ToArray();
        if (!idArray.Any())
            return Task.FromResult(Array.Empty<ClearMLTask>() as IReadOnlyList<ClearMLTask>);
        return GetTasksAsync(new JsonObject { ["id"] = JsonValue.Create(idArray) }, cancellationToken);
    }

    private async Task<IReadOnlyList<ClearMLTask>> GetTasksAsync(
        JsonObject body,
        CancellationToken cancellationToken = default
    )
    {
        body["only_fields"] = new JsonArray(
            "id",
            "name",
            "status",
            "project",
            "last_iteration",
            "status_reason",
            "status_message",
            "created",
            "active_duration",
            "last_metrics",
            "runtime"
        );
        JsonObject? result = await CallAsync("tasks", "get_all_ex", body, cancellationToken);
        var tasks = (JsonArray?)result?["data"]?["tasks"];
        return tasks?.Select(t => t.Deserialize<ClearMLTask>(JsonSerializerOptions)!).ToArray()
            ?? Array.Empty<ClearMLTask>();
    }

    private async Task<JsonObject?> CallAsync(
        string service,
        string action,
        JsonNode body,
        CancellationToken cancellationToken = default
    )
    {
        string requestPath = $"{service}.{action}";
        var request = new HttpRequestMessage(HttpMethod.Post, requestPath)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add(
            "Authorization",
            $"Bearer {await _clearMLAuthService.GetAuthTokenAsync(cancellationToken)}"
        );
        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string result = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            return (JsonObject?)JsonNode.Parse(result);
        }
        catch
        {
            StringBuilder headerString = new();
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
            {
                headerString.Append(
                    CultureInfo.InvariantCulture,
                    $"{header.Key}: {string.Join(", ", header.Value)}{Environment.NewLine}"
                );
            }
            _logger.LogWarning(
                "Failed to parse ClearML response with code {httpCode} from request path `{request}`: {Response}",
                response.StatusCode,
                requestPath,
                headerString.ToString()
            );
            throw;
        }
    }

    private class SnakeCaseJsonNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            return string.Concat(name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString()))
                .ToLowerInvariant();
        }
    }
}
