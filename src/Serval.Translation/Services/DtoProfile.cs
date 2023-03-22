namespace Serval.Translation.Services;

public class DtoProfile : Profile
{
    public DtoProfile()
    {
        CreateMap<TranslationEngine, TranslationEngineDto>()
            .ForMember(dest => dest.Url, o => o.MapFrom((src, _) => $"{Urls.TranslationEngines}/{src.Id}"));
        CreateMap<Build, BuildDto>()
            .ForMember(
                dest => dest.Url,
                o => o.MapFrom((src, _) => $"{Urls.TranslationEngines}/{src.EngineRef}/builds/{src.Id}")
            )
            .ForMember(
                dest => dest.Engine,
                o =>
                    o.MapFrom(
                        (src, _) =>
                            new ResourceLinkDto
                            {
                                Id = src.EngineRef,
                                Url = $"{Urls.TranslationEngines}/{src.EngineRef}"
                            }
                    )
            );
        CreateMap<TranslationResult, TranslationResultDto>();
        CreateMap<AlignedWordPair, AlignedWordPairDto>();
        CreateMap<Phrase, PhraseDto>();
        CreateMap<WordGraph, WordGraphDto>();
        CreateMap<WordGraphArc, WordGraphArcDto>();
        CreateMap<Pretranslation, PretranslationDto>();
        CreateMap<Corpus, TranslationCorpusDto>()
            .ForMember(
                dest => dest.Url,
                o =>
                    o.MapFrom(
                        (src, _, _, ctxt) => $"{Urls.TranslationEngines}/{ctxt.Items["EngineId"]}/corpora/{src.Id}"
                    )
            )
            .ForMember(
                dest => dest.Engine,
                o =>
                    o.MapFrom(
                        (src, _, _, ctxt) =>
                            new ResourceLinkDto
                            {
                                Id = (string)ctxt.Items["EngineId"],
                                Url = $"{Urls.TranslationEngines}/{ctxt.Items["EngineId"]}"
                            }
                    )
            );
        CreateMap<CorpusFile, TranslationCorpusFileDto>()
            .ForMember(
                dest => dest.File,
                o => o.MapFrom((src, _) => new ResourceLinkDto { Id = src.Id, Url = $"{Urls.Files}/{src.Id}" })
            );
    }
}
