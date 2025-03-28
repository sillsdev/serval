﻿namespace Serval.Machine.Shared.Services;

public class NmtClearMLBuildJobFactory(
    ISharedFileService sharedFileService,
    ILanguageTagService languageTagService,
    IRepository<TranslationEngine> engines
) : IClearMLBuildJobFactory
{
    private readonly ISharedFileService _sharedFileService = sharedFileService;
    private readonly ILanguageTagService _languageTagService = languageTagService;
    private readonly IRepository<TranslationEngine> _engines = engines;

    public EngineType EngineType => EngineType.Nmt;

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
            TranslationEngine? engine = await _engines.GetAsync(e => e.EngineId == engineId, cancellationToken);
            if (engine is null)
                throw new InvalidOperationException("The engine does not exist.");

            Uri sharedFileUri = _sharedFileService.GetBaseUri();
            string baseUri = sharedFileUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
            string folder = sharedFileUri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
            _languageTagService.ConvertToFlores200Code(engine.SourceLanguage, out string srcLang);
            _languageTagService.ConvertToFlores200Code(engine.TargetLanguage, out string trgLang);
            return "from machine.jobs.build_nmt_engine import run\n"
                + "args = {\n"
                + $"    'model_type': '{modelType}',\n"
                + $"    'engine_id': '{engineId}',\n"
                + $"    'build_id': '{buildId}',\n"
                + $"    'src_lang': '{srcLang}',\n"
                + $"    'trg_lang': '{trgLang}',\n"
                + $"    'shared_file_uri': '{baseUri}',\n"
                + $"    'shared_file_folder': '{folder}',\n"
                + (buildOptions is not null ? $"    'build_options': '''{buildOptions}''',\n" : "")
                // buildRevision + 1 because the build revision is incremented after the build job
                // is finished successfully but the file should be saved with the new revision number
                + (engine.IsModelPersisted ? $"    'save_model': '{engineId}_{engine.BuildRevision + 1}',\n" : $"")
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
