using Serval.Engine.Translation.V1;

namespace Serval.AspNetCore.Services;

public class DtoProfile : Profile
{
    private const string TranslationEnginesUrl = "/translation-engines";
    private const string WebhooksUrl = "/hooks";
    private const string CorporaUrl = "/corpora";

    public DtoProfile()
    {
        CreateMap<TranslationEngineResult, TranslationEngineDto>()
            .ForMember(dto => dto.Href, o => o.MapFrom((e, _) => $"{TranslationEnginesUrl}/{e.EngineId}"));
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
                o => o.MapFrom((b, _) => $"{TranslationEnginesUrl}/{b.ParentRef}/builds/{b.Id}")
            )
            .ForMember(
                dto => dto.Engine,
                o =>
                    o.MapFrom(
                        (b, _) =>
                            new ResourceLinkDto { Id = b.ParentRef, Href = $"{TranslationEnginesUrl}/{b.ParentRef}" }
                    )
            );
        CreateMap<Corpus, CorpusDto>().ForMember(dto => dto.Href, o => o.MapFrom((c, _) => $"{CorporaUrl}/{c.Id}"));
        CreateMap<DataFile, DataFileDto>()
            .ForMember(
                dto => dto.Href,
                o => o.MapFrom((f, _, _, ctxt) => $"{CorporaUrl}/{ctxt.Items["CorpusId"]}/files/{f.Id}")
            );
        CreateMap<Contracts.TranslationResult, TranslationResultDto>();
        CreateMap<Contracts.AlignedWordPair, AlignedWordPairDto>();
        CreateMap<Contracts.Phrase, PhraseDto>();
        CreateMap<WordGraph, WordGraphDto>();
        CreateMap<WordGraphArc, WordGraphArcDto>();
        CreateMap<Webhook, WebhookDto>().ForMember(dto => dto.Href, o => o.MapFrom((h, _) => $"{WebhooksUrl}/{h.Id}"));
        CreateMap<Pretranslation, PretranslationDto>();
    }
}
