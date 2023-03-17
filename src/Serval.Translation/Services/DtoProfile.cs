namespace Serval.Translation.Services;

public class DtoProfile : Profile
{
    private const string TranslationEnginesUrl = "/translation/engines";
    private const string FilesUrl = "/files";

    public DtoProfile()
    {
        CreateMap<TranslationEngine, TranslationEngineDto>()
            .ForMember(dest => dest.Url, o => o.MapFrom((src, _) => $"{TranslationEnginesUrl}/{src.Id}"));
        CreateMap<Build, BuildDto>()
            .ForMember(
                dest => dest.Url,
                o => o.MapFrom((src, _) => $"{TranslationEnginesUrl}/{src.EngineRef}/builds/{src.Id}")
            )
            .ForMember(
                dest => dest.Engine,
                o =>
                    o.MapFrom(
                        (src, _) =>
                            new ResourceLinkDto { Id = src.EngineRef, Url = $"{TranslationEnginesUrl}/{src.EngineRef}" }
                    )
            );
        CreateMap<TranslationResult, TranslationResultDto>();
        CreateMap<AlignedWordPair, AlignedWordPairDto>();
        CreateMap<Phrase, PhraseDto>();
        CreateMap<WordGraph, WordGraphDto>();
        CreateMap<WordGraphArc, WordGraphArcDto>();
        CreateMap<Pretranslation, PretranslationDto>();
        CreateMap<ParallelCorpus, ParallelCorpusDto>()
            .ForMember(
                dest => dest.Url,
                o =>
                    o.MapFrom((src, _, _, ctxt) => $"{TranslationEnginesUrl}/{ctxt.Items["EngineId"]}/corpora/{src.Id}")
            )
            .ForMember(
                dest => dest.Engine,
                o =>
                    o.MapFrom(
                        (src, _, _, ctxt) =>
                            new ResourceLinkDto
                            {
                                Id = (string)ctxt.Items["EngineId"],
                                Url = $"{TranslationEnginesUrl}/{ctxt.Items["EngineId"]}"
                            }
                    )
            );
        CreateMap<ParallelCorpusFile, ParallelCorpusFileDto>()
            .ForMember(
                dest => dest.File,
                o => o.MapFrom((src, _) => new ResourceLinkDto { Id = src.Id, Url = $"{FilesUrl}/{src.Id}" })
            );
    }
}
