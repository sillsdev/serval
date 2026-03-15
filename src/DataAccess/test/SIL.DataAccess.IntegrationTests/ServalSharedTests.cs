namespace SIL.DataAccess;

[TestFixture]
[Category("Integration")]
public class ServalSharedTests
{
    private TestEnvironment _env;

    [SetUp]
    public void SetUp()
    {
        _env = new TestEnvironment();
    }

    [Test]
    public async Task InitializesRepositories()
    {
        // Setup
        IServalBuilder servalBuilder = new ServalBuilder(_env.Services, _env.Configuration);
        servalBuilder.AddMongoDataAccess(cfg =>
        {
            cfg.AddTranslationRepositories();
            cfg.AddWordAlignmentRepositories();
            cfg.AddDataFilesRepositories();
            cfg.AddWebhooksRepositories();
        });

        // SUT
        await _env.InitializeDatabaseAsync();

        // Verify schema versioning
        SchemaVersion? schemaVersion = await _env.SchemaVersions!.GetAsync(s => s.Collection == "schema_versions");
        Assert.That(schemaVersion!.Version, Is.EqualTo(1));
    }

    [Test]
    public async Task Migrates_TranslationEngines_ParallelCorpora()
    {
        // Setup
        IServalBuilder servalBuilder = new ServalBuilder(_env.Services, _env.Configuration);
        servalBuilder.AddMongoDataAccess(cfg =>
        {
            cfg.AddTranslationRepositories();
        });

        // Populate pre-migration-data
        await _env.SetupSchemaAsync("translation.engines", 2);
        var objectId = ObjectId.GenerateNewId();
        await _env.InsertDocumentAsync("translation.engines", new BsonDocument { { "_id", objectId } });

        // SUT
        await _env.InitializeDatabaseAsync();

        // Verify schema version change
        SchemaVersion? schemaVersion = await _env.SchemaVersions!.GetAsync(s => s.Collection == "translation.engines");
        Assert.That(schemaVersion!.Version, Is.GreaterThan(2));

        // Verify migration
        BsonDocument? document = await _env.GetDocumentAsync("translation.engines", objectId);
        Assert.Multiple(() =>
        {
            Assert.That(document.Contains("parallelCorpora"), Is.True);
            Assert.That(document["parallelCorpora"].IsBsonArray, Is.True);
            Assert.That(document["parallelCorpora"].AsBsonArray, Is.Empty);
        });
    }

    [TearDown]
    public void TearDown()
    {
        _env.Dispose();
    }
}
