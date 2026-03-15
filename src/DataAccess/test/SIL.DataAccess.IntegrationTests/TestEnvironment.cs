namespace SIL.DataAccess;

public class TestEnvironment : DisposableBase
{
    public const string DatabaseName = "serval_test";
    public readonly MongoClient MongoClient = new();
    public IRepository<SchemaVersion>? SchemaVersions { get; private set; }
    public readonly ServiceCollection Services = [];
    public readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mongo"] = $"mongodb://localhost:27017/{DatabaseName}",
            }
        )
        .Build();

    public TestEnvironment()
    {
        Services.AddLogging();
        Services.AddSingleton<MongoDataAccessInitializeService>();
        ResetDatabase();
    }

    public async Task InitializeDatabaseAsync()
    {
        ServiceProvider provider = Services.BuildServiceProvider();

        SchemaVersions = provider.GetRequiredService<IRepository<SchemaVersion>>();
        MongoDataAccessInitializeService mongoDataAccessInitializeService =
            provider.GetRequiredService<MongoDataAccessInitializeService>();
        await mongoDataAccessInitializeService.StartAsync(CancellationToken.None);
    }

    public Task<BsonDocument> GetDocumentAsync(string collection, ObjectId objectId) =>
        MongoClient
            .GetDatabase(DatabaseName)
            .GetCollection<BsonDocument>(collection)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", objectId))
            .FirstOrDefaultAsync();

    public Task InsertDocumentAsync(string collection, BsonDocument document) =>
        MongoClient.GetDatabase(DatabaseName).GetCollection<BsonDocument>(collection).InsertOneAsync(document);

    public Task SetupSchemaAsync(string collection, int version) =>
        InsertDocumentAsync(
            "schema_versions",
            new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "collection", collection },
                { "version", version },
                { "revision", 1 },
            }
        );

    protected override void DisposeManagedResources()
    {
        ResetDatabase();
        MongoClient.Dispose();
    }

    private void ResetDatabase()
    {
        MongoClient.DropDatabase(DatabaseName);
    }
}
