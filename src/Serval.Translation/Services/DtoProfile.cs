using Serval.Translation.Engine.V1;

namespace Serval.Translation.Services;

public class DtoProfile : Profile
{
    private const string TranslationEnginesUrl = "/translation-engines";
    private const string CorporaUrl = "/corpora";

    public DtoProfile()
    {
        CreateMap<Entities.TranslationEngine, TranslationEngineDto>()
            .ForMember(dto => dto.Href, o => o.MapFrom((e, _) => $"{TranslationEnginesUrl}/{e.Id}"));
        CreateMap<TranslationEngineCorpus, TranslationEngineCorpusDto>()
            .ForMember(
                dto => dto.Corpus,
                o =>
                    o.MapFrom(
                        (tec, _) => new ResourceLinkDto { Id = tec.CorpusRef, Href = $"{CorporaUrl}/{tec.CorpusRef}" }
                    )
            )
            .ForMember(
                dto => dto.Href,
                o =>
                    o.MapFrom(
                        (tec, _, _, ctxt) => $"{TranslationEnginesUrl}/{ctxt.Items["EngineId"]}/corpora/{tec.CorpusRef}"
                    )
            );
        CreateMap<Build, BuildDto>()
            .ForMember(
                dto => dto.Href,
                o => o.MapFrom((b, _) => $"{TranslationEnginesUrl}/{b.EngineRef}/builds/{b.Id}")
            )
            .ForMember(
                dto => dto.Engine,
                o =>
                    o.MapFrom(
                        (b, _) =>
                            new ResourceLinkDto { Id = b.EngineRef, Href = $"{TranslationEnginesUrl}/{b.EngineRef}" }
                    )
            );
        CreateMap<TranslationResult, TranslationResultDto>();
        CreateMap<AlignedWordPair, AlignedWordPairDto>();
        CreateMap<Phrase, PhraseDto>();
        CreateMap<WordGraph, WordGraphDto>();
        CreateMap<WordGraphArc, WordGraphArcDto>();
        CreateMap<Pretranslation, PretranslationDto>();
    }
}
