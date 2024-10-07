namespace Serval.Assessment.Services;

public class AssessmentJobService(
    IDataAccessContext dataAccessContext,
    IRepository<AssessmentBuild> jobs,
    IRepository<AssessmentResult> results
) : BuildService<AssessmentBuild>(jobs)
{
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IRepository<AssessmentResult> _results = results;

    public override Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return _dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                IBuild? job = await Entities.DeleteAsync(id, ct);
                if (job is null)
                    throw new EntityNotFoundException($"Could not find the Job '{id}'.");

                await _results.DeleteAllAsync(r => r.JobRef == id, ct);
            },
            cancellationToken
        );
    }
}
