using Google.Protobuf.WellKnownTypes;
using Serval.Assessment.V1;

namespace Serval.Aqua.Shared.Services;

public class ServalAssessmentEngineServiceV1(ICorpusService corpusService, IJobService jobService)
    : AssessmentEngineApi.AssessmentEngineApiBase
{
    private static readonly Empty Empty = new();

    private readonly ICorpusService _corpusService = corpusService;
    private readonly IJobService _jobService = jobService;

    public override async Task<Empty> Create(CreateRequest request, ServerCallContext context)
    {
        await _corpusService.AddEngineAsync(Map(request.Corpus), request.EngineId, context.CancellationToken);
        if (request.ReferenceCorpus is not null)
        {
            await _corpusService.AddEngineAsync(
                Map(request.ReferenceCorpus),
                request.EngineId,
                context.CancellationToken
            );
        }
        return Empty;
    }

    public override async Task<Empty> Delete(DeleteRequest request, ServerCallContext context)
    {
        await _corpusService.RemoveEngineAsync(request.EngineId, context.CancellationToken);
        return Empty;
    }

    public override async Task<Empty> StartJob(StartJobRequest request, ServerCallContext context)
    {
        (JobData jobData, CorpusFilter corpusFilter) = Map(request);

        await _jobService.CreateAsync(
            request.EngineId,
            request.JobId,
            System.Enum.Parse<AssessmentType>(request.EngineType),
            jobData,
            request.HasOptions ? request.Options : null,
            corpusFilter,
            context.CancellationToken
        );
        return Empty;
    }

    public override async Task<Empty> CancelJob(CancelJobRequest request, ServerCallContext context)
    {
        await _jobService.CancelAsync(request.JobId, context.CancellationToken);
        return Empty;
    }

    private static CorpusData Map(Assessment.V1.Corpus source)
    {
        return new CorpusData
        {
            Id = source.Id,
            Language = source.Language,
            DataRevision = source.Revision,
            Files = source.Files.Select(Map).ToArray()
        };
    }

    private static Models.CorpusFile Map(Assessment.V1.CorpusFile source)
    {
        return new Models.CorpusFile
        {
            Location = source.Location,
            Format = (Models.FileFormat)source.Format,
            TextId = source.TextId
        };
    }

    private static (JobData, CorpusFilter) Map(StartJobRequest source)
    {
        JobData jobData =
            new()
            {
                CorpusData = Map(source.Corpus),
                ReferenceCorpusData = source.ReferenceCorpus is null ? null : Map(source.ReferenceCorpus)
            };

        CorpusFilter corpusFilter =
            new()
            {
                IncludeAll = source.IncludeAll,
                IncludeChapters = source.IncludeChapters.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Chapters.ToHashSet()
                ),
                IncludeTextIds = source.IncludeTextIds.ToHashSet()
            };

        return (jobData, corpusFilter);
    }
}
