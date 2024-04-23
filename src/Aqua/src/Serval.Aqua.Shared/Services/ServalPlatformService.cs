using Google.Protobuf.WellKnownTypes;
using Serval.Assessment.V1;

namespace Serval.Aqua.Shared.Services;

public class ServalPlatformService(AssessmentPlatformApi.AssessmentPlatformApiClient client) : IPlatformService
{
    private readonly AssessmentPlatformApi.AssessmentPlatformApiClient _client = client;

    public async Task JobStartedAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await _client.JobStartedAsync(new JobStartedRequest { JobId = jobId }, cancellationToken: cancellationToken);
    }

    public async Task JobCompletedAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await _client.JobCompletedAsync(
            new JobCompletedRequest { JobId = jobId },
            cancellationToken: cancellationToken
        );
    }

    public async Task JobCanceledAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await _client.JobCanceledAsync(new JobCanceledRequest { JobId = jobId }, cancellationToken: cancellationToken);
    }

    public async Task JobFaultedAsync(string jobId, string message, CancellationToken cancellationToken = default)
    {
        await _client.JobFaultedAsync(
            new JobFaultedRequest { JobId = jobId, Message = message },
            cancellationToken: cancellationToken
        );
    }

    public async Task JobRestartingAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await _client.JobRestartingAsync(
            new JobRestartingRequest { JobId = jobId },
            cancellationToken: cancellationToken
        );
    }

    public async Task InsertResultsAsync(
        string jobId,
        IEnumerable<Result> results,
        CancellationToken cancellationToken = default
    )
    {
        using AsyncClientStreamingCall<InsertResultsRequest, Empty> call = _client.InsertResults(
            cancellationToken: cancellationToken
        );
        foreach (Result result in results)
        {
            InsertResultsRequest request =
                new()
                {
                    JobId = jobId,
                    TextId = result.TextId,
                    Ref = result.Ref,
                    Score = result.Score
                };
            if (result.Description is not null)
                request.Description = result.Description;
            await call.RequestStream.WriteAsync(request, cancellationToken);
        }
        await call.RequestStream.CompleteAsync();
        await call;
    }
}
