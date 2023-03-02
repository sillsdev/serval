namespace Serval.Shared.Services;

public class MassTransitMongoDataAccessContext : DisposableBase, IMongoDataAccessContext
{
    private readonly MongoDbContext _context;

    public MassTransitMongoDataAccessContext(MongoDbContext context)
    {
        _context = context;
    }

    public IClientSessionHandle? Session => _context.Session;

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return _context.BeginTransaction(cancellationToken);
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        return _context.CommitTransaction(cancellationToken);
    }

    public Task AbortTransactionAsync(CancellationToken cancellationToken = default)
    {
        return _context.AbortTransaction(cancellationToken);
    }

    protected override void DisposeManagedResources()
    {
        _context.Dispose();
    }
}
