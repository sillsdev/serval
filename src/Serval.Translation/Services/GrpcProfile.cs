namespace Serval.Translation.Services;

public class GrpcProfile : Profile
{
    public GrpcProfile()
    {
        CreateMap<V1.TranslationResult, TranslationResult>();
        CreateMap<V1.WordGraph, WordGraph>();
        CreateMap<V1.WordGraphArc, WordGraphArc>();
        CreateMap<V1.Phrase, Phrase>();
        CreateMap<V1.AlignedWordPair, AlignedWordPair>();
        CreateMap<ParallelCorpus, V1.ParallelCorpus>();
        CreateMap<ParallelCorpusFile, V1.ParallelCorpusFile>()
            .ForMember(
                dest => dest.Location,
                o => o.MapFrom((cf, _, _, ctxt) => Path.Combine((string)ctxt.Items["Directory"], cf.Filename))
            );
    }
}
