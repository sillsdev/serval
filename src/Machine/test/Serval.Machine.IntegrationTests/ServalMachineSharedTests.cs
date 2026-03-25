namespace Serval.Machine.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class ServalMachineSharedTests
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
        IMachineBuilder machineBuilder = new MachineBuilder(_env.Services, _env.Configuration);
        machineBuilder.AddMongoDataAccess();

        // SUT
        await _env.InitializeDatabaseAsync();

        // Verify schema versioning
        SchemaVersion? schemaVersion = await _env.SchemaVersions!.GetAsync(s => s.Collection == "schema_versions");
        Assert.That(schemaVersion!.Version, Is.EqualTo(1));
    }

    [TearDown]
    public void TearDown()
    {
        _env.Dispose();
    }
}
