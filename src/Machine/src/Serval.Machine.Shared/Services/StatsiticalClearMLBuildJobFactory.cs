namespace Serval.Machine.Shared.Services;

public class StatisticalClearMLBuildJobFactory(
    ISharedFileService sharedFileService,
    IRepository<WordAlignmentEngine> engines
) : IClearMLBuildJobFactory
{
    private readonly ISharedFileService _sharedFileService = sharedFileService;
    private readonly IRepository<WordAlignmentEngine> _engines = engines;

    public EngineType EngineType => EngineType.Statistical;

    public async Task<string> CreateJobScriptAsync(
        string engineId,
        string buildId,
        string modelType,
        BuildStage stage,
        object? data = null,
        string? buildOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        if (stage == BuildStage.Train)
        {
            WordAlignmentEngine? engine = await _engines.GetAsync(e => e.EngineId == engineId, cancellationToken);
            if (engine is null)
                throw new InvalidOperationException("The engine does not exist.");

            Uri sharedFileUri = _sharedFileService.GetBaseUri();
            string baseUri = sharedFileUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
            string folder = sharedFileUri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
            return "from machine.jobs.build_word_alignment_model import run\n"
                + "args = {\n"
                + $"    'model_type': '{modelType}',\n"
                + $"    'engine_id': '{engineId}',\n"
                + $"    'build_id': '{buildId}',\n"
                + $"    'shared_file_uri': '{baseUri}',\n"
                + $"    'shared_file_folder': '{folder}',\n"
                + (buildOptions is not null ? $"    'build_options': '''{buildOptions}''',\n" : "")
                + $"    'clearml': True\n"
                + "}\n"
                + "run(args)\n";
        }
        else
        {
            throw new ArgumentException("Unknown build stage.", nameof(stage));
        }
    }
}
