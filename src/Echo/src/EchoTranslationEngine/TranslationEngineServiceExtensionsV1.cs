namespace EchoTranslationEngine;

public class TranslationEngineServiceExtensionsV1 : TranslationEngineApi.TranslationEngineApiBase
{
    private static readonly Empty Empty = new();

    public override Task<TranslateResponse> Translate(TranslateRequest request, ServerCallContext context)
    {
        string[] tokens = request.Segment.Split();
        var response = new TranslateResponse
        {
            Results =
            {
                new TranslationResult
                {
                    Translation = request.Segment,
                    SourceTokens = { tokens },
                    TargetTokens = { tokens },
                    Confidences = { Enumerable.Repeat(1.0, tokens.Length) },
                    Sources =
                    {
                        Enumerable.Repeat(
                            new TranslationSources { Values = { TranslationSource.Primary } },
                            tokens.Length
                        )
                    },
                    Alignment =
                    {
                        Enumerable
                            .Range(0, tokens.Length)
                            .Select(i => new AlignedWordPair { SourceIndex = i, TargetIndex = i })
                    },
                    Phrases =
                    {
                        new Phrase
                        {
                            SourceSegmentStart = 0,
                            SourceSegmentEnd = tokens.Length,
                            TargetSegmentCut = tokens.Length
                        }
                    }
                }
            }
        };
        return Task.FromResult(response);
    }

    public override Task<Empty> TrainSegmentPair(TrainSegmentPairRequest request, ServerCallContext context)
    {
        return Task.FromResult(Empty);
    }

    public override Task<GetWordGraphResponse> GetWordGraph(GetWordGraphRequest request, ServerCallContext context)
    {
        string[] tokens = request.Segment.Split();
        return Task.FromResult(
            new GetWordGraphResponse
            {
                WordGraph = new WordGraph
                {
                    InitialStateScore = 0.0,
                    SourceTokens = { tokens },
                    Arcs =
                    {
                        Enumerable
                            .Range(0, tokens.Length - 1)
                            .Select(index => new WordGraphArc
                            {
                                PrevState = index,
                                NextState = index + 1,
                                Score = 1.0,
                                TargetTokens = { tokens[index] },
                                Confidences = { 1.0 },
                                SourceSegmentStart = index,
                                SourceSegmentEnd = index + 1,
                                Alignment =
                                {
                                    new AlignedWordPair { SourceIndex = 0, TargetIndex = 0 }
                                }
                            })
                    },
                    FinalStates = { tokens.Length }
                }
            }
        );
    }

    public override Task<GetModelDownloadUrlResponse> GetModelDownloadUrl(
        GetModelDownloadUrlRequest request,
        ServerCallContext context
    )
    {
        var response = new GetModelDownloadUrlResponse
        {
            Url = "https://example.com/model",
            ModelRevision = 1,
            ExpiresAt = DateTime.UtcNow.AddHours(1).ToTimestamp()
        };
        return Task.FromResult(response);
    }

    public override Task<GetLanguageInfoResponse> GetLanguageInfo(
        GetLanguageInfoRequest request,
        ServerCallContext context
    )
    {
        return Task.FromResult(
            new GetLanguageInfoResponse { InternalCode = request.Language + "_echo", IsNative = true, }
        );
    }
}
