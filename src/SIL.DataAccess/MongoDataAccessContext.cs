namespace SIL.DataAccess;

public class MongoDataAccessContext(IMongoClient client) : DisposableBase, IMongoDataAccessContext
{
    private readonly IMongoClient _client = client;
    private readonly object _lock = new();
    private Task<IClientSessionHandle>? _startSession;
    public IClientSessionHandle? Session { get; private set; }

    public async Task<TResult> WithTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> callbackAsync,
        CancellationToken cancellationToken = default
    )
    {
        IClientSessionHandle session = await StartSession(cancellationToken).ConfigureAwait(false);

        return await session
            .WithTransactionAsync(
                async (session, ct) => await callbackAsync(ct).ConfigureAwait(false),
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task WithTransactionAsync(
        Func<CancellationToken, Task> callbackAsync,
        CancellationToken cancellationToken = default
    )
    {
        _ = await WithTransactionAsync(
                async (ct) =>
                {
                    await callbackAsync(ct).ConfigureAwait(false);
                    // Assign dummy value to avoid warning about not returning a value
                    return new object();
                },
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }

    private Task<IClientSessionHandle> StartSession(CancellationToken cancellationToken)
    {
        async Task<IClientSessionHandle> Start()
        {
            var sessionOptions = new ClientSessionOptions
            {
                DefaultTransactionOptions = new TransactionOptions(
                    ReadConcern.Majority,
                    ReadPreference.Primary,
                    WriteConcern.WMajority
                )
            };
            IClientSessionHandle handle = await _client
                .StartSessionAsync(sessionOptions, cancellationToken)
                .ConfigureAwait(false);

            Session = handle;
            return handle;
        }

        lock (_lock)
        {
            if (_startSession != null)
                return _startSession;
            _startSession = Start();
            return _startSession;
        }
    }

    protected override void DisposeManagedResources()
    {
        Session?.Dispose();
    }
}
