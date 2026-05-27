namespace Serval.Machine.Shared.Services;

public class ClearMLBuildJobRunner(
    IClearMLService clearMLService,
    IEnumerable<IClearMLBuildJobFactory> buildJobFactories,
    IOptionsMonitor<BuildJobOptions> options
) : IBuildJobRunner
{
    private readonly IClearMLService _clearMLService = clearMLService;
    private readonly Dictionary<EngineType, IClearMLBuildJobFactory> _buildJobFactories =
        buildJobFactories.ToDictionary(f => f.EngineType);

    private readonly Dictionary<EngineType, ClearMLBuildQueue> _options = options.CurrentValue.ClearML.ToDictionary(o =>
        o.EngineType
    );

    public BuildJobRunnerType Type => BuildJobRunnerType.ClearML;

    public async Task CreateEngineAsync(
        string engineId,
        string? name = null,
        CancellationToken cancellationToken = default
    )
    {
        await _clearMLService.CreateProjectAsync(engineId, name, cancellationToken);
    }

    public async Task DeleteEngineAsync(string engineId, CancellationToken cancellationToken = default)
    {
        string? projectId = await _clearMLService.GetProjectIdAsync(engineId, cancellationToken);
        if (projectId is not null)
            await _clearMLService.DeleteProjectAsync(projectId, cancellationToken);
    }

    public async Task<(string JobId, string? JobData)> CreateJobAsync(
        EngineType engineType,
        string engineId,
        string buildId,
        BuildStage stage,
        object? data = null,
        string? buildOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        string? projectId = await _clearMLService.GetProjectIdAsync(engineId, cancellationToken);
        projectId ??= await _clearMLService.CreateProjectAsync(engineId, cancellationToken: cancellationToken);

        ClearMLTask? task = await _clearMLService.GetTaskByNameAsync(buildId, cancellationToken);
        if (task is not null)
            return (task.Id, null);

        IClearMLBuildJobFactory buildJobFactory = _buildJobFactories[engineType];
        string script = await buildJobFactory.CreateJobScriptAsync(
            engineId,
            buildId,
            _options[engineType].ModelType,
            stage,
            buildOptions,
            cancellationToken
        );
        string jobId = await _clearMLService.CreateTaskAsync(
            buildId,
            projectId,
            script,
            _options[engineType].DockerImage,
            cancellationToken
        );
        return (jobId, null);
    }

    public Task<bool> DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return _clearMLService.DeleteTaskAsync(jobId, cancellationToken);
    }

    public Task<bool> EnqueueJobAsync(
        string jobId,
        EngineType engineType,
        CancellationToken cancellationToken = default
    )
    {
        return _clearMLService.EnqueueTaskAsync(jobId, _options[engineType].Queue, cancellationToken);
    }

    public Task<bool> StopJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return _clearMLService.StopTaskAsync(jobId, cancellationToken);
    }
}
