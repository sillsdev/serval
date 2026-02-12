namespace Serval.DataFiles.Services;

[TestFixture]
public class CorpusServiceTests
{
    private const string CorpusId = "c00000000000000000000001";

    private static readonly DataFile DefaultDataFile = new()
    {
        Id = "df0000000000000000000001",
        Owner = "owner1",
        Name = "file1",
        Filename = "file1.txt",
        Format = FileFormat.Text,
    };
    private static readonly Corpus DefaultCorpus = new()
    {
        Id = CorpusId,
        Owner = "owner1",
        Name = "corpus1",
        Language = "en",
        Files = new List<CorpusFile>() { new() { FileRef = DefaultDataFile.Id } },
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
            DataAccessContext = Substitute.For<IDataAccessContext>();
            DataAccessContext
                .WithTransactionAsync(Arg.Any<Func<CancellationToken, Task<Corpus>>>(), Arg.Any<CancellationToken>())
                .Returns(x =>
                {
                    return ((Func<CancellationToken, Task<Corpus>>)x[0])((CancellationToken)x[1]);
                });
            Service = new CorpusService(
                Corpora,
                Substitute.For<IRepository<DataFile>>(),
                DataAccessContext,
                Substitute.For<IScopedMediator>()
            );
        }

        public MemoryRepository<Corpus> Corpora { get; }

        public CorpusService Service { get; }

        public IDataAccessContext DataAccessContext { get; }
    }
}
