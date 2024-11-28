namespace Serval.DataFiles.Services;

[TestFixture]
public class CorpusServiceTests
{
    private const string CorpusId = "c00000000000000000000001";

    private static readonly DataFileReference DefaultDataFile =
        new()
        {
            Id = "df0000000000000000000001",
            Name = "file1",
            Format = FileFormat.Text
        };
    private static readonly Corpus DefaultCorpus =
        new()
        {
            Id = CorpusId,
            Owner = "owner1",
            Name = "corpus1",
            Language = "en",
            Files = new List<CorpusFile>() { new() { FileReference = DefaultDataFile } }
        };

    [Test]
    public async Task CreateAsync()
    {
        var env = new TestEnvironment();
        Corpus corpus = await env.Service.CreateAsync(DefaultCorpus);
        Assert.That(corpus.Name, Is.EqualTo((await env.Service.GetAsync(CorpusId)).Name));
    }

    [Test]
    public async Task UpdateAsync()
    {
        var env = new TestEnvironment();
        await env.Service.CreateAsync(DefaultCorpus);
        await env.Service.UpdateAsync(CorpusId, new List<CorpusFile>());
        Corpus corpus = await env.Service.GetAsync(CorpusId);
        Assert.That(corpus.Files, Has.Count.EqualTo(0));
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            Corpora = new MemoryRepository<Corpus>();
            Service = new CorpusService(Corpora);
        }

        public MemoryRepository<Corpus> Corpora { get; }

        public CorpusService Service { get; }
    }
}
