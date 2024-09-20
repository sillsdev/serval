using Serval.Assessment.V1;
using Serval.Engine.V1;

namespace Serval.Assessment.Services;

public class AssessmentEngineService(
    IRepository<AssessmentEngine> engines,
    IRepository<AssessmentBuild> builds,
    IRepository<AssessmentResult> results,
    GrpcClientFactory grpcClientFactory,
    IOptionsMonitor<DataFileOptions> dataFileOptions,
    IDataAccessContext dataAccessContext,
    ILoggerFactory loggerFactory,
    IScriptureDataFileService scriptureDataFileService
)
    : EngineServiceBase<AssessmentEngine, AssessmentBuild>(
        engines,
        builds,
        grpcClientFactory,
        dataAccessContext,
        loggerFactory
    ),
        IAssessmentEngineService
{
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;
    private readonly IScriptureDataFileService _scriptureDataFileService = scriptureDataFileService;

    public async Task StartBuildAsync(AssessmentBuild build, CancellationToken cancellationToken = default)
    {
        AssessmentEngine engine = await GetAsync(build.EngineRef, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{build.EngineRef}'.");

        IReadOnlyList<string> includeTextIds = build.TextIds ?? new List<string>();
        IList<Engine.V1.CorpusFile> files = engine.Corpus.Files.Select(Map).ToList();
        IDictionary<string, ScriptureChapters> includeChapters;

        if (build.ScriptureRange is not null)
        {
            if (engine.Corpus.Files.Count > 1 || engine.Corpus.Files[0].Format != Shared.Contracts.FileFormat.Paratext)
            {
                throw new InvalidOperationException($"The engine is not compatible with using a scripture range.");
            }

            try
            {
                ScrVers versification = _scriptureDataFileService
                    .GetParatextProjectSettings(files[0].Location)
                    .Versification;
                includeChapters = ScriptureRangeParser
                    .GetChapters(build.ScriptureRange, versification)
                    .ToDictionary(kvp => kvp.Key, kvp => new ScriptureChapters { Chapters = { kvp.Value } });
            }
            catch (ArgumentException ae)
            {
                throw new InvalidOperationException(
                    $"The scripture range {build.ScriptureRange} is not valid: {ae.Message}"
                );
            }
        }

        AssessmentCorpus corpus = new AssessmentCorpus
        {
            Language = engine.Corpus.Language,
            IncludeAll = build.TextIds is null || build.TextIds.Count == 0,
            IncludeTextIds = includeTextIds,
            IncludeChapters = includeChapters,
            Files = { engine.Corpus.Files.Select(Map) },
        };

        await StartBuildAsync(build, JsonSerializer.Serialize(corpus), build.Options, cancellationToken);
    }

    public async Task<Shared.Models.Corpus> ReplaceCorpusAsync(
        string id,
        Shared.Models.Corpus corpus,
        CancellationToken cancellationToken = default
    )
    {
        AssessmentEngine? engine = await Entities.UpdateAsync(
            id,
            u => u.Set(e => e.Corpus, corpus),
            cancellationToken: cancellationToken
        );
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{id}'.");
        return engine.Corpus;
    }

    public async Task<Shared.Models.Corpus> ReplaceReferenceCorpusAsync(
        string id,
        Shared.Models.Corpus referenceCorpus,
        CancellationToken cancellationToken = default
    )
    {
        AssessmentEngine? engine = await Entities.UpdateAsync(
            id,
            u => u.Set(e => e.ReferenceCorpus, referenceCorpus),
            cancellationToken: cancellationToken
        );
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{id}'.");
        return engine.ReferenceCorpus!;
    }

    public Task RemoveDataFileFromAllCorporaAsync(string dataFileId, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAllAsync(
            e => e.Corpus.Files.Any(f => f.Id == dataFileId) || e.ReferenceCorpus!.Files.Any(f => f.Id == dataFileId),
            u =>
            {
                u.RemoveAll(e => e.Corpus.Files, f => f.Id == dataFileId);
                u.RemoveAll(e => e.ReferenceCorpus!.Files, f => f.Id == dataFileId);
            },
            cancellationToken
        );
    }

    private Engine.V1.Corpus Map(Shared.Models.Corpus source)
    {
        return new Engine.V1.Corpus { Language = source.Language, Files = { source.Files.Select(Map) } };
    }

    private Engine.V1.CorpusFile Map(Shared.Models.CorpusFile source)
    {
        return new Engine.V1.CorpusFile
        {
            TextId = source.TextId,
            Format = (Engine.V1.FileFormat)source.Format,
            Location = Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, source.Filename)
        };
    }
}
