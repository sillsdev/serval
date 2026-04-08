namespace Serval.IntegrationTests;

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
        IServalBuilder builder = _env.Services.AddServal(_env.Configuration);
        builder.AddTranslationDataAccess();
        builder.AddWordAlignmentDataAccess();
        builder.AddDataFilesDataAccess();
        builder.AddWebhooksDataAccess();

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
        IServalBuilder builder = _env.Services.AddServal(_env.Configuration);
        builder.AddTranslation();

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
