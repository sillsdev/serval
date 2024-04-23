namespace Serval.Aqua.Shared.Services;

public class CorpusService(IDataAccessContext dataAccessContext, IRepository<Corpus> corpora, IAquaService aquaService)
    : ICorpusService
{
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IRepository<Corpus> _corpora = corpora;
    private readonly IAquaService _aquaService = aquaService;

    public Task<Corpus?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return _corpora.GetAsync(id, cancellationToken);
    }

    public Task AddEngineAsync(CorpusData corpusData, string engineId, CancellationToken cancellationToken = default)
    {
        return _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                Corpus? corpus = await _corpora.UpdateAsync(
                    corpusData.Id,
                    u =>
                    {
                        u.SetOnInsert(c => c.Language, corpusData.Language);
                        u.SetOnInsert(c => c.DataRevision, corpusData.DataRevision);
                        u.Add(u => u.Engines, engineId);
                    },
                    upsert: true,
                    cancellationToken: cancellationToken
                );
                if (corpus?.VersionId is null)
                {
                    ParatextProjectSettings settings = GetProjectSettings(corpusData.Files);
                    VersionDto version = await _aquaService.CreateVersionAsync(
                        settings.FullName,
                        corpusData.Language,
                        settings.Name,
                        cancellationToken
                    );
                    await _corpora.UpdateAsync(
                        corpusData.Id,
                        u => u.Set(c => c.VersionId, version.Id),
                        cancellationToken: cancellationToken
                    );
                }
            },
            cancellationToken
        );
    }

    public async Task<int> CreateRevisionAsync(CorpusData corpusData, CancellationToken cancellationToken = default)
    {
        Corpus? corpus = await _corpora.GetAsync(corpusData.Id, cancellationToken);
        if (corpus is null || corpus.VersionId is null)
            throw new OperationCanceledException($"Could not find the corpus '{corpusData.Id}'.");

        if (corpus.RevisionId is not null && corpus.DataRevision == corpusData.DataRevision)
            return corpus.RevisionId.Value;

        ITextCorpus textCorpus = CreateTextCorpus(corpusData.Files);

        using TempFile tempFile = new();
        using (StreamWriter writer = new(tempFile.Path))
        {
            foreach ((string text, _, _) in textCorpus.ExtractScripture())
                await writer.WriteAsync(text + "\n");
        }

        RevisionDto revision = await _aquaService.CreateRevisionAsync(
            corpus.VersionId.Value,
            tempFile.Path,
            cancellationToken
        );
        await _corpora.UpdateAsync(
            corpusData.Id,
            u =>
            {
                u.Set(c => c.RevisionId, revision.Id);
                u.Set(c => c.DataRevision, corpusData.DataRevision);
            },
            cancellationToken: cancellationToken
        );
        return revision.Id;
    }

    public Task RemoveEngineAsync(string engineId, CancellationToken cancellationToken = default)
    {
        return _corpora.UpdateAllAsync(
            c => c.Engines.Contains(engineId),
            u => u.Remove(c => c.Engines, engineId),
            cancellationToken
        );
    }

    private static ParatextProjectSettings GetProjectSettings(IReadOnlyList<CorpusFile> files)
    {
        if (files.Count == 1 && files[0].Format == FileFormat.Paratext)
        {
            using ZipArchive archive = ZipFile.OpenRead(files[0].Location);
            ZipParatextProjectSettingsParser settingsParser = new(archive);
            return settingsParser.Parse();
        }
        throw new NotSupportedException("The file format is not supported.");
    }

    private static ITextCorpus CreateTextCorpus(IReadOnlyList<CorpusFile> files)
    {
        if (files.Count == 1 && files[0].Format == FileFormat.Paratext)
            return new ParatextBackupTextCorpus(files[0].Location);
        throw new NotSupportedException("");
    }
}
