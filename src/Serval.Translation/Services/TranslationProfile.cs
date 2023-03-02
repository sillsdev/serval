using Serval.Translation.Engine.V1;
using Corpus = Serval.Translation.Entities.Corpus;
using CorpusFile = Serval.Translation.Entities.CorpusFile;

namespace Serval.Translation.Services;

public class TranslationProfile : Profile
{
    private const string TranslationEnginesUrl = "/translation/engines";
    private const string CorporaUrl = "/translation/corpora";
    private const string FilesUrl = "/files";

    public TranslationProfile()
    {
        CreateMap<TranslationEngine, TranslationEngineDto>()
            .ForMember(dto => dto.Url, o => o.MapFrom((e, _) => $"{TranslationEnginesUrl}/{e.Id}"));
        CreateMap<TranslationEngineCorpus, TranslationEngineCorpusDto>()
            .ForMember(
                dto => dto.Corpus,
                o =>
                    o.MapFrom(
                        (tec, _) => new ResourceLinkDto { Id = tec.CorpusRef, Url = $"{CorporaUrl}/{tec.CorpusRef}" }
                    )
            )
            .ForMember(
                dto => dto.Url,
                o =>
                    o.MapFrom(
                        (tec, _, _, ctxt) => $"{TranslationEnginesUrl}/{ctxt.Items["EngineId"]}/corpora/{tec.CorpusRef}"
                    )
            );
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

        CreateMap<Corpus, CorpusDto>().ForMember(dto => dto.Url, o => o.MapFrom((c, _) => $"{CorporaUrl}/{c.Id}"));
        CreateMap<CorpusFile, CorpusFileDto>()
            .ForMember(
                dto => dto.Url,
                o => o.MapFrom((f, _, _, ctxt) => $"{CorporaUrl}/{ctxt.Items["CorpusId"]}/files/{f.Id}")
            )
            .ForMember(
                dto => dto.File,
                o =>
                    o.MapFrom(
                        (cf, _) => new ResourceLinkDto { Id = cf.DataFileRef, Url = $"{FilesUrl}/{cf.DataFileRef}" }
                    )
            );
    }
}
