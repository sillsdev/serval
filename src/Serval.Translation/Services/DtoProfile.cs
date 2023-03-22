namespace Serval.Translation.Services;

public class DtoProfile : Profile
{
    public DtoProfile()
    {
        CreateMap<TranslationEngine, TranslationEngineDto>().AfterMap<TranslationEngineDtoMappingAction>();
        CreateMap<Build, BuildDto>().AfterMap<BuildDtoMappingAction>();
        CreateMap<TranslationResult, TranslationResultDto>();
        CreateMap<AlignedWordPair, AlignedWordPairDto>();
        CreateMap<Phrase, PhraseDto>();
        CreateMap<WordGraph, WordGraphDto>();
        CreateMap<WordGraphArc, WordGraphArcDto>();
        CreateMap<Pretranslation, PretranslationDto>();
        CreateMap<Corpus, TranslationCorpusDto>().AfterMap<TranslationCorpusDtoMappingAction>();
        CreateMap<CorpusFile, TranslationCorpusFileDto>().AfterMap<TranslationCorpusFileDtoMappingAction>();
    }
}

public class TranslationEngineDtoMappingAction : IMappingAction<TranslationEngine, TranslationEngineDto>
{
    private readonly LinkGenerator _linkGenerator;

    public TranslationEngineDtoMappingAction(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    public void Process(TranslationEngine source, TranslationEngineDto destination, ResolutionContext context)
    {
        destination.Url = _linkGenerator.GetPathByAction(
            controller: "TranslationEngines",
            action: "Get",
            values: new { id = source.Id, version = "1" }
        )!;
    }
}

public class BuildDtoMappingAction : IMappingAction<Build, BuildDto>
{
    private readonly LinkGenerator _linkGenerator;

    public BuildDtoMappingAction(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    public void Process(Build source, BuildDto destination, ResolutionContext context)
    {
        destination.Url = _linkGenerator.GetPathByAction(
            controller: "TranslationEngines",
            action: "GetBuild",
            values: new
            {
                id = source.EngineRef,
                buildId = source.Id,
                version = "1"
            }
        )!;

        destination.Engine = new ResourceLinkDto
        {
            Id = source.EngineRef,
            Url = _linkGenerator.GetPathByAction(
                controller: "TranslationEngines",
                action: "Get",
                values: new { id = source.EngineRef, version = "1" }
            )!
        };
    }
}

public class TranslationCorpusDtoMappingAction : IMappingAction<Corpus, TranslationCorpusDto>
{
    private readonly LinkGenerator _linkGenerator;

    public TranslationCorpusDtoMappingAction(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    public void Process(Corpus source, TranslationCorpusDto destination, ResolutionContext context)
    {
        var engineId = (string)context.Items["EngineId"];
        destination.Url = _linkGenerator.GetPathByAction(
            controller: "TranslationEngines",
            action: "GetCorpus",
            values: new
            {
                id = engineId,
                corpusId = source.Id,
                version = "1"
            }
        )!;

        destination.Engine = new ResourceLinkDto
        {
            Id = engineId,
            Url = _linkGenerator.GetPathByAction(
                controller: "TranslationEngines",
                action: "Get",
                values: new { id = engineId, version = "1" }
            )!
        };
    }
}

public class TranslationCorpusFileDtoMappingAction : IMappingAction<CorpusFile, TranslationCorpusFileDto>
{
    private readonly LinkGenerator _linkGenerator;

    public TranslationCorpusFileDtoMappingAction(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    public void Process(CorpusFile source, TranslationCorpusFileDto destination, ResolutionContext context)
    {
        destination.File = new ResourceLinkDto
        {
            Id = source.Id,
            Url = _linkGenerator.GetPathByAction(
                controller: "DataFiles",
                action: "Get",
                values: new { id = source.Id, version = "1" }
            )!
        };
    }
}
