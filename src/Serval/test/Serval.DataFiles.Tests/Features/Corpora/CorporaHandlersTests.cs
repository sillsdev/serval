namespace Serval.DataFiles.Features.Corpora;

[TestFixture]
public class CorporaHandlersTests
{
    private const string Owner = "owner1";

    [Test]
    public async Task GetAllCorpora()
    {
        var env = new TestEnvironment();
        await env.CreateCorpusAsync("c00000000000000000000001");
        await env.CreateCorpusAsync("c00000000000000000000002");
        await env.Corpora.InsertAsync(
            new Corpus
            {
                Id = "c00000000000000000000003",
                Owner = "owner2",
                Language = "es",
                Files = [],
            }
        );
        GetAllCorporaHandler handler = new(env.Corpora, env.Mapper);
        GetAllCorporaResponse response = await handler.HandleAsync(new(Owner), CancellationToken.None);
        Assert.That(response.Corpora.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetCorpus_CorpusExists()
    {
        var env = new TestEnvironment();
        Corpus corpus = await env.CreateCorpusAsync();
        GetCorpusHandler handler = new(env.Corpora, env.Mapper);
        GetCorpusResponse response = await handler.HandleAsync(new(Owner, corpus.Id), CancellationToken.None);
        Assert.That(response.Corpus.Id, Is.EqualTo(corpus.Id));
    }

    [Test]
    public void GetCorpus_CorpusDoesNotExist()
    {
        var env = new TestEnvironment();
        GetCorpusHandler handler = new(env.Corpora, env.Mapper);
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(new(Owner, "c00000000000000000000001"), CancellationToken.None)
        );
    }

    [Test]
    public async Task GetCorpus_WrongOwner()
    {
        var env = new TestEnvironment();
        Corpus corpus = await env.CreateCorpusAsync();
        GetCorpusHandler handler = new(env.Corpora, env.Mapper);
        Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(new("owner2", corpus.Id), CancellationToken.None)
        );
    }

    [Test]
    public async Task CreateCorpus()
    {
        var env = new TestEnvironment();
        await env.CreateDataFileAsync();
        CreateCorpusHandler handler = new(env.Corpora, env.DataFiles, env.IdGenerator, env.Mapper);
        CreateCorpusResponse response = await handler.HandleAsync(
            new(
                Owner,
                new CorpusConfigDto
                {
                    Name = "corpus1",
                    Language = "en",
                    Files = [new CorpusFileConfigDto { FileId = "df0000000000000000000001", TextId = "text1" }],
                }
            ),
            CancellationToken.None
        );
        Corpus? corpus = await env.Corpora.GetAsync(response.Corpus.Id, CancellationToken.None);
        Assert.That(corpus, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(corpus.Name, Is.EqualTo("corpus1"));
            Assert.That(corpus.Language, Is.EqualTo("en"));
            Assert.That(corpus.Files, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void CreateCorpus_EmptyLanguage()
    {
        var env = new TestEnvironment();
        CreateCorpusHandler handler = new(env.Corpora, env.DataFiles, env.IdGenerator, env.Mapper);
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new(Owner, new CorpusConfigDto { Language = "", Files = [] }), CancellationToken.None)
        );
    }

    [Test]
    public void CreateCorpus_DataFileDoesNotExist()
    {
        var env = new TestEnvironment();
        CreateCorpusHandler handler = new(env.Corpora, env.DataFiles, env.IdGenerator, env.Mapper);
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(
                new(
                    Owner,
                    new CorpusConfigDto
                    {
                        Language = "en",
                        Files = [new CorpusFileConfigDto { FileId = "df0000000000000000000001" }],
                    }
                ),
                CancellationToken.None
            )
        );
    }

    [Test]
    public async Task UpdateCorpus()
    {
        var env = new TestEnvironment();
        Corpus corpus = await env.CreateCorpusAsync();
        await env.CreateDataFileAsync();
        UpdateCorpusHandler handler = new(
            env.Corpora,
            env.DataFiles,
            env.DataAccessContext,
            env.EventRouter,
            env.Mapper
        );
        UpdateCorpusResponse response = await handler.HandleAsync(
            new(Owner, corpus.Id, [new CorpusFileConfigDto { FileId = "df0000000000000000000001", TextId = "text1" }]),
            CancellationToken.None
        );
        Assert.That(response.Corpus.Files, Has.Count.EqualTo(1));
    }

    [Test]
    public void UpdateCorpus_CorpusDoesNotExist()
    {
        var env = new TestEnvironment();
        UpdateCorpusHandler handler = new(
            env.Corpora,
            env.DataFiles,
            env.DataAccessContext,
            env.EventRouter,
            env.Mapper
        );
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(new(Owner, "c00000000000000000000001", []), CancellationToken.None)
        );
    }

    [Test]
    public async Task UpdateCorpus_DataFileDoesNotExist()
    {
        var env = new TestEnvironment();
        Corpus corpus = await env.CreateCorpusAsync();
        UpdateCorpusHandler handler = new(
            env.Corpora,
            env.DataFiles,
            env.DataAccessContext,
            env.EventRouter,
            env.Mapper
        );
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(
                new(Owner, corpus.Id, [new CorpusFileConfigDto { FileId = "df0000000000000000000099" }]),
                CancellationToken.None
            )
        );
    }

    [Test]
    public async Task DeleteCorpus_CorpusExists()
    {
        var env = new TestEnvironment();
        Corpus corpus = await env.CreateCorpusAsync();
        DeleteCorpusHandler handler = new(env.Corpora);
        await handler.HandleAsync(new(Owner, corpus.Id), CancellationToken.None);
        Corpus? deleted = await env.Corpora.GetAsync(corpus.Id, CancellationToken.None);
        Assert.That(deleted, Is.Null);
    }

    [Test]
    public void DeleteCorpus_CorpusDoesNotExist()
    {
        var env = new TestEnvironment();
        DeleteCorpusHandler handler = new(env.Corpora);
        Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.HandleAsync(new(Owner, "c00000000000000000000001"), CancellationToken.None)
        );
    }

    [Test]
    public async Task DeleteCorpus_WrongOwner()
    {
        var env = new TestEnvironment();
        Corpus corpus = await env.CreateCorpusAsync();
        DeleteCorpusHandler handler = new(env.Corpora);
        Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(new("owner2", corpus.Id), CancellationToken.None)
        );
    }

    private class TestEnvironment
    {
        private static readonly DataFile DefaultDataFile = new()
        {
            Id = "df0000000000000000000001",
            Owner = Owner,
            Name = "file1",
            Filename = "file1.txt",
            Format = FileFormat.Text,
        };

        public TestEnvironment()
        {
            Corpora = new MemoryRepository<Corpus>();
            DataFiles = new MemoryRepository<DataFile>();
            EventRouter = Substitute.For<IEventRouter>();
            DataAccessContext = new MemoryDataAccessContext();
            Mapper = new DtoMapper(Substitute.For<IUrlService>());
            IdGenerator = Substitute.For<IIdGenerator>();
            IdGenerator.GenerateId().Returns("c00000000000000000000001");
        }

        public MemoryRepository<Corpus> Corpora { get; }
        public MemoryRepository<DataFile> DataFiles { get; }
        public IEventRouter EventRouter { get; }
        public IDataAccessContext DataAccessContext { get; }
        public DtoMapper Mapper { get; }
        public IIdGenerator IdGenerator { get; }

        public async Task<Corpus> CreateCorpusAsync(string id = "c00000000000000000000001")
        {
            var corpus = new Corpus
            {
                Id = id,
                Owner = Owner,
                Name = "corpus1",
                Language = "en",
                Files = [new CorpusFile { FileRef = DefaultDataFile.Id }],
            };
            await Corpora.InsertAsync(corpus);
            return corpus;
        }

        public Task CreateDataFileAsync()
        {
            return DataFiles.InsertAsync(DefaultDataFile);
        }
    }
}
