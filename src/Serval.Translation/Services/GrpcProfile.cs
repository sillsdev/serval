﻿namespace Serval.Translation.Services;

public class GrpcProfile : Profile
{
    public GrpcProfile()
    {
        CreateMap<V1.TranslationResult, TranslationResult>();
        CreateMap<V1.WordGraph, WordGraph>();
        CreateMap<V1.WordGraphArc, WordGraphArc>();
        CreateMap<V1.Phrase, Phrase>();
        CreateMap<V1.AlignedWordPair, AlignedWordPair>();
        CreateMap<Corpus, V1.Corpus>();
        CreateMap<CorpusFile, V1.CorpusFile>()
            .ForMember(
                dest => dest.Location,
                o => o.MapFrom((cf, _, _, ctxt) => Path.Combine((string)ctxt.Items["Directory"], cf.Filename))
            )
            .ForMember(dest => dest.TextId, o => o.Condition(src => src.TextId is not null));
    }
}