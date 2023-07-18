namespace Serval.E2ETests
{
    [TestFixture]
    [Category("E2EMissingServices")]
    public class E2EMissingServicesTests
    {
        private ServalClientHelper? _helperClient;

        [SetUp]
        public void Setup()
        {
            _helperClient = InitializeClient();
        }

        [Test]
        [Category("MongoWorking")]
        public void UseMongoAndAuth0Async()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                await _helperClient!.dataFilesClient.GetAllAsync();
            });
        }

        [Test]
        [Category("EngineServerWorking")]
        public void UseEngineServerAsync()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                string engineId = await _helperClient!.CreateNewEngine("SmtTransfer", "es", "en", "SMT3");
                var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
                await _helperClient.PostTextCorpusToEngine(engineId, books, "es", "en", false);
                await _helperClient.BuildEngine(engineId);
            });
        }

        [Test]
        [Category("ClearMLNotWorking")]
        public void UseMissingClearMLAsync()
        {
            Assert.ThrowsAsync<ServalApiException>(async () =>
            {
                string engineId = await _helperClient!.CreateNewEngine("Nmt", "es", "en", "NMT1");
                var books = new string[] { "MAT.txt", "1JN.txt", "2JN.txt" };
                await _helperClient.PostTextCorpusToEngine(engineId, books, "es", "en", false);
                var cId = await _helperClient.PostTextCorpusToEngine(
                    engineId,
                    new string[] { "3JN.txt" },
                    "es",
                    "en",
                    true
                );
                await _helperClient.BuildEngine(engineId);
                IList<Pretranslation> lTrans = await _helperClient.translationEnginesClient.GetAllPretranslationsAsync(
                    engineId,
                    cId
                );
            });
        }

        [Test]
        [Category("AWSNotWorking")]
        public void UseMissingAWSAsync()
        {
            Assert.ThrowsAsync<ServalApiException>(async () =>
            {
                string engineId = await _helperClient!.CreateNewEngine("Nmt", "es", "en", "NMT1");
                var books = new string[] { "MAT.txt", "1JN.txt", "2JN.txt" };
                await _helperClient.PostTextCorpusToEngine(engineId, books, "es", "en", false);
                var cId = await _helperClient.PostTextCorpusToEngine(
                    engineId,
                    new string[] { "3JN.txt" },
                    "es",
                    "en",
                    true
                );
                await _helperClient.BuildEngine(engineId);
                IList<TranslationBuild>? builds = await _helperClient.translationEnginesClient.GetAllBuildsAsync(
                    engineId
                );
                Assert.That(builds.First().State, Is.EqualTo(JobState.Faulted));
            });
        }

        [Test]
        [Category("MongoNotWorking")]
        public void UseMissingMongoAsync()
        {
            ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
            {
                await _helperClient!.dataFilesClient.GetAllAsync();
            });
            Assert.NotNull(ex);
            Assert.That(ex!.StatusCode, Is.EqualTo(503));
        }

        [Test]
        [Category("EngineServerNotWorking")]
        public void UseMissingEngineServerAsync()
        {
            ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
            {
                string engineId = await _helperClient!.CreateNewEngine("SmtTransfer", "es", "en", "SMT3");
                var books = new string[] { "1JN.txt", "2JN.txt", "3JN.txt" };
                await _helperClient.PostTextCorpusToEngine(engineId, books, "es", "en", false);
                await _helperClient.BuildEngine(engineId);
            });
            Assert.NotNull(ex);
            Assert.That(ex!.StatusCode, Is.EqualTo(503));
        }

        [TearDown]
        public void TearDown() { }

        private ServalClientHelper InitializeClient()
        {
            var hostUrl = Environment.GetEnvironmentVariable("SERVAL_HOST_URL");
            var clientId = Environment.GetEnvironmentVariable("SERVAL_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("SERVAL_CLIENT_SECRET");
            var authUrl = Environment.GetEnvironmentVariable("SERVAL_AUTH_URL");
            if (hostUrl == null)
            {
                Console.WriteLine(
                    "You need a serval host url in the environment variable SERVAL_HOST_URL!  Look at README for instructions on getting one."
                );
            }
            else if (clientId == null)
            {
                Console.WriteLine(
                    "You need an auth0 client_id in the environment variable SERVAL_CLIENT_ID!  Look at README for instructions on getting one."
                );
            }
            else if (clientSecret == null)
            {
                Console.WriteLine(
                    "You need an auth0 client_secret in the environment variable SERVAL_CLIENT_SECRET!  Look at README for instructions on getting one."
                );
            }
            else if (authUrl == null)
            {
                Console.WriteLine(
                    "You need an auth0 authorization url in the environment variable SERVAL_AUTH_URL!  Look at README for instructions on getting one."
                );
            }

            return new ServalClientHelper(
                hostUrl,
                authUrl,
                "https://machine.sil.org",
                clientId,
                clientSecret,
                ignoreSSLErrors: true
            );
        }
    }
}
