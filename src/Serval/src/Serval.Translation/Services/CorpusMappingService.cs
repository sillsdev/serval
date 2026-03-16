namespace Serval.Translation.Services;

public class CorpusMappingService(IOptionsMonitor<DataFileOptions> dataFileOptions) : ICorpusMappingService
{
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;

    public SIL.ServiceToolkit.Models.ParallelCorpus Map(ParallelCorpus source)
    {
        return new SIL.ServiceToolkit.Models.ParallelCorpus
        {
            Id = source.Id,
            SourceCorpora = source.SourceCorpora.Select(Map).ToArray(),
            TargetCorpora = source.TargetCorpora.Select(Map).ToArray(),
        };
    }

    private SIL.ServiceToolkit.Models.MonolingualCorpus Map(MonolingualCorpus source)
    {
        return new SIL.ServiceToolkit.Models.MonolingualCorpus
        {
            Id = source.Id,
            Language = source.Language,
            Files = source.Files.Select(Map).ToList(),
        };
    }

    public SIL.ServiceToolkit.Models.ParallelCorpus Map(Corpus source, Engine engine)
    {
        return new SIL.ServiceToolkit.Models.ParallelCorpus
        {
            Id = source.Id,
            SourceCorpora = source.SourceFiles.Select(f => Map(f, engine.SourceLanguage)).ToArray(),
            TargetCorpora = source.TargetFiles.Select(f => Map(f, engine.TargetLanguage)).ToArray(),
        };
    }

    private SIL.ServiceToolkit.Models.MonolingualCorpus Map(CorpusFile source, string language)
    {
        return new SIL.ServiceToolkit.Models.MonolingualCorpus
        {
            Id = source.Id,
            Language = language,
            Files = [Map(source)],
        };
    }

    private SIL.ServiceToolkit.Models.CorpusFile Map(CorpusFile source)
    {
        return new SIL.ServiceToolkit.Models.CorpusFile
        {
            Location = GetFilePath(source.Filename),
            Format = (SIL.ServiceToolkit.Models.FileFormat)source.Format,
            TextId = source.TextId,
        };
    }

    public string GetFilePath(string filename)
    {
        return Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, filename);
    }
}
