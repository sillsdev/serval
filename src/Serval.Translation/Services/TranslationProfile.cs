namespace Serval.Translation.Services;

public class TranslationProfile : Profile
{
    private const string TranslationEnginesUrl = "/translation_engines";
    private const string FilesUrl = "/files";

    public TranslationProfile()
    {
        CreateMap<TranslationEngine, TranslationEngineDto>()
            .ForMember(dto => dto.Url, o => o.MapFrom((e, _) => $"{TranslationEnginesUrl}/{e.Id}"));
        CreateMap<Build, BuildDto>()
            .ForMember(dto => dto.Url, o => o.MapFrom((b, _) => $"{TranslationEnginesUrl}/{b.EngineRef}/builds/{b.Id}"))
            .ForMember(
                dto => dto.Engine,
                o =>
                    o.MapFrom(
                        (b, _) =>
                            new ResourceLinkDto { Id = b.EngineRef, Url = $"{TranslationEnginesUrl}/{b.EngineRef}" }
                    )
            );
        CreateMap<TranslationResult, TranslationResultDto>();
        CreateMap<AlignedWordPair, AlignedWordPairDto>();
        CreateMap<Phrase, PhraseDto>();
        CreateMap<WordGraph, WordGraphDto>();
        CreateMap<WordGraphArc, WordGraphArcDto>();
        CreateMap<Pretranslation, PretranslationDto>();
        CreateMap<Corpus, CorpusDto>()
            .ForMember(
                dto => dto.Url,
                o => o.MapFrom((c, _, _, ctxt) => $"{TranslationEnginesUrl}/{ctxt.Items["EngineId"]}/corpora/{c.Id}")
            );
        CreateMap<CorpusFile, CorpusFileDto>()
            .ForMember(
                dto => dto.File,
                o => o.MapFrom((cf, _) => new ResourceLinkDto { Id = cf.Id, Url = $"{FilesUrl}/{cf.Id}" })
            );
    }
}
