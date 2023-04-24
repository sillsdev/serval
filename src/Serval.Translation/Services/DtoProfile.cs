namespace Serval.Translation.Services;

public class DtoProfile : Profile
{
    public DtoProfile()
    {
        AllowNullCollections = true;
        CreateMap<Engine, TranslationEngineDto>().AfterMap<TranslationEngineDtoMappingAction>();
        CreateMap<Build, TranslationBuildDto>().AfterMap<BuildDtoMappingAction>();
        CreateMap<PretranslateCorpus, PretranslateCorpusDto>().AfterMap<PretranslateCorpusDtoMappingAction>();
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

public class TranslationEngineDtoMappingAction : IMappingAction<Engine, TranslationEngineDto>
{
    private readonly IUrlService _urlService;

    public TranslationEngineDtoMappingAction(IUrlService urlService)
    {
        _urlService = urlService;
    }

    public void Process(Engine source, TranslationEngineDto destination, ResolutionContext context)
    {
        destination.Url = _urlService.GetUrl("GetTranslationEngine", new { id = source.Id });
    }
}

public class BuildDtoMappingAction : IMappingAction<Build, TranslationBuildDto>
{
    private readonly IUrlService _urlService;

    public BuildDtoMappingAction(IUrlService urlService)
    {
        _urlService = urlService;
    }

    public void Process(Build source, TranslationBuildDto destination, ResolutionContext context)
    {
        destination.Url = _urlService.GetUrl("GetTranslationBuild", new { id = source.EngineRef, buildId = source.Id });

        destination.Engine = new ResourceLinkDto
        {
            Id = source.EngineRef,
            Url = _urlService.GetUrl("GetTranslationEngine", new { id = source.EngineRef })
        };
    }
}

public class TranslationCorpusDtoMappingAction : IMappingAction<Corpus, TranslationCorpusDto>
{
    private readonly IUrlService _urlService;

    public TranslationCorpusDtoMappingAction(IUrlService urlService)
    {
        _urlService = urlService;
    }

    public void Process(Corpus source, TranslationCorpusDto destination, ResolutionContext context)
    {
        var engineId = (string)context.Items["EngineId"];
        destination.Url = _urlService.GetUrl("GetTranslationCorpus", new { id = engineId, corpusId = source.Id });

        destination.Engine = new ResourceLinkDto
        {
            Id = engineId,
            Url = _urlService.GetUrl("GetTranslationEngine", new { id = engineId })
        };
    }
}

public class TranslationCorpusFileDtoMappingAction : IMappingAction<CorpusFile, TranslationCorpusFileDto>
{
    private readonly IUrlService _urlService;

    public TranslationCorpusFileDtoMappingAction(IUrlService urlService)
    {
        _urlService = urlService;
    }

    public void Process(CorpusFile source, TranslationCorpusFileDto destination, ResolutionContext context)
    {
        destination.File = new ResourceLinkDto
        {
            Id = source.Id,
            Url = _urlService.GetUrl("GetDataFile", new { id = source.Id })
        };
    }
}

public class PretranslateCorpusDtoMappingAction : IMappingAction<PretranslateCorpus, PretranslateCorpusDto>
{
    private readonly IUrlService _urlService;

    public PretranslateCorpusDtoMappingAction(IUrlService urlService)
    {
        _urlService = urlService;
    }

    public void Process(PretranslateCorpus source, PretranslateCorpusDto destination, ResolutionContext context)
    {
        var engineId = (string)context.Items["EngineId"];
        destination.Corpus = new ResourceLinkDto
        {
            Id = source.CorpusRef,
            Url = _urlService.GetUrl("GetTranslationCorpus", new { id = engineId, corpusId = source.CorpusRef })
        };
    }
}
