namespace SIL.DataAccess;

public class MongoDataAccessContext : DisposableBase, IMongoDataAccessContext
{
    private readonly IMongoClient _client;
    private readonly object _lock;
    private Task<IClientSessionHandle>? _startSession;

    public MongoDataAccessContext(IMongoClient client)
    {
        _client = client;
        _lock = new object();
    }

    public IClientSessionHandle? Session { get; private set; }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        IClientSessionHandle session = await StartSession(cancellationToken).ConfigureAwait(false);
        if (!session.IsInTransaction)
            session.StartTransaction();
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Session is null)
            throw new InvalidOperationException("No session has been created");

        if (!Session.IsInTransaction)
            throw new InvalidOperationException("The session is not in an active transaction");

        await Session.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AbortTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Session is null)
            throw new InvalidOperationException("No session has been created");

        if (!Session.IsInTransaction)
            throw new InvalidOperationException("The session is not in an active transaction");

        await Session.AbortTransactionAsync(cancellationToken).ConfigureAwait(false);
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
