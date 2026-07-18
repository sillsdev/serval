namespace Serval.ApiServer;

[TestFixture]
[Category("Integration")]
public class ApiKeysTests
{
    const string FILE_ID = "000000000000000000000000";
    const string FILE_NAME = "sample1.txt";
    const string DOES_NOT_EXIST_ID = "000000000000000000000001";

    TestEnvironment _env;

    [SetUp]
    public async Task Setup()
    {
        _env = new TestEnvironment();
        var dataFile = new DataFiles.Models.DataFile
        {
            Id = FILE_ID,
            Owner = "client1",
            Name = FILE_NAME,
            Filename = FILE_NAME,
            Format = Shared.Contracts.FileFormat.Text,
        };
        await _env.DataFiles.InsertAsync(dataFile);
    }

    [Test]
    [TestCase(null, 201)] //null gives all API key management privileges
    [TestCase(new string[] { Scopes.ReadFiles }, 403)] //Arbitrary unrelated privilege
    public async Task CreateApiKeyAsync(IEnumerable<string>? scope, int expectedStatusCode)
    {
        ApiKeysClient client = _env.CreateClient(scope);
        switch (expectedStatusCode)
        {
            case 201:
                ApiKeyCreated result = await client.CreateAsync(
                    new ApiKeyConfig
                    {
                        ClientId = "client1",
                        Name = "key1",
                        Scopes = { Scopes.ReadFiles },
                    }
                );
                Assert.Multiple(() =>
                {
                    Assert.That(result.Key, Does.StartWith("serval_"));
                    Assert.That(result.ClientId, Is.EqualTo("client1"));
                });
                ApiKey resultAfterCreate = await client.GetAsync(result.Id);
                Assert.That(resultAfterCreate.Name, Is.EqualTo("key1"));
                break;
            case 403:
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.CreateAsync(
                        new ApiKeyConfig
                        {
                            ClientId = "client1",
                            Name = "key1",
                            Scopes = { Scopes.ReadFiles },
                        }
                    );
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    [TestCase("unknown:scope")]
    [TestCase(Scopes.CreateApiKeys)]
    [TestCase(Scopes.ReadApiKeys)]
    [TestCase(Scopes.DeleteApiKeys)]
    public void CreateApiKeyAsync_InvalidScope(string apiKeyScope)
    {
        ApiKeysClient client = _env.CreateClient(null);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.CreateAsync(
                new ApiKeyConfig
                {
                    ClientId = "client1",
                    Name = "key1",
                    Scopes = { apiKeyScope },
                }
            );
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public void CreateApiKeyAsync_ExpirationInPast()
    {
        ApiKeysClient client = _env.CreateClient(null);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.CreateAsync(
                new ApiKeyConfig
                {
                    ClientId = "client1",
                    Name = "key1",
                    Scopes = { Scopes.ReadFiles },
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
                }
            );
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task GetAllApiKeysAsync_FilterByClientId()
    {
        ApiKeysClient client = _env.CreateClient(null);
        await client.CreateAsync(
            new ApiKeyConfig
            {
                ClientId = "client1",
                Name = "key1",
                Scopes = { Scopes.ReadFiles },
            }
        );
        await client.CreateAsync(
            new ApiKeyConfig
            {
                ClientId = "client2",
                Name = "key2",
                Scopes = { Scopes.ReadFiles },
            }
        );

        IList<ApiKey> allKeys = await client.GetAllAsync();
        IList<ApiKey> client2Keys = await client.GetAllAsync(clientId: "client2");

        Assert.Multiple(() =>
        {
            Assert.That(allKeys, Has.Count.EqualTo(2));
            Assert.That(client2Keys, Has.Count.EqualTo(1));
            Assert.That(client2Keys[0].Name, Is.EqualTo("key2"));
        });
    }

    [Test]
    [TestCase(null, 200)] //null gives all API key management privileges
    [TestCase(null, 404)]
    [TestCase(new string[] { Scopes.ReadFiles }, 403)] //Arbitrary unrelated privilege
    public async Task DeleteApiKeyByIdAsync(IEnumerable<string>? scope, int expectedStatusCode)
    {
        ApiKeysClient client = _env.CreateClient(scope);
        switch (expectedStatusCode)
        {
            case 200:
            {
                ApiKeyCreated apiKey = await _env.CreateClient(null)
                    .CreateAsync(
                        new ApiKeyConfig
                        {
                            ClientId = "client1",
                            Name = "key1",
                            Scopes = { Scopes.ReadFiles },
                        }
                    );
                await client.DeleteAsync(apiKey.Id);
                ApiKey revokedKey = await client.GetAsync(apiKey.Id);
                Assert.That(revokedKey.RevokedAt, Is.Not.Null);

                // revoking an already revoked key succeeds and preserves the original timestamp
                await client.DeleteAsync(apiKey.Id);
                ApiKey revokedAgainKey = await client.GetAsync(apiKey.Id);
                Assert.That(revokedAgainKey.RevokedAt, Is.EqualTo(revokedKey.RevokedAt));
                break;
            }
            case 403:
            case 404:
            {
                ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
                {
                    await client.DeleteAsync(DOES_NOT_EXIST_ID);
                });
                Assert.That(ex?.StatusCode, Is.EqualTo(expectedStatusCode));
                break;
            }
            default:
                Assert.Fail("Unanticipated expectedStatusCode. Check test case for typo.");
                break;
        }
    }

    [Test]
    public async Task AuthenticateWithApiKey()
    {
        ApiKeyCreated apiKey = await _env.CreateClient(null)
            .CreateAsync(
                new ApiKeyConfig
                {
                    ClientId = "client1",
                    Name = "key1",
                    Scopes = { Scopes.ReadFiles, Scopes.CreateFiles },
                }
            );

        DataFilesClient client = _env.CreateDataFilesClientWithApiKey(apiKey.Key);
        DataFile dataFile = await client.GetAsync(FILE_ID);
        Assert.That(dataFile.Id, Is.EqualTo(FILE_ID));
    }

    [Test]
    public async Task AuthenticateWithApiKey_OutOfScopeEndpoint()
    {
        ApiKeyCreated apiKey = await _env.CreateClient(null)
            .CreateAsync(
                new ApiKeyConfig
                {
                    ClientId = "client1",
                    Name = "key1",
                    Scopes = { Scopes.ReadFiles },
                }
            );

        DataFilesClient client = _env.CreateDataFilesClientWithApiKey(apiKey.Key);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.DeleteAsync(FILE_ID);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task AuthenticateWithApiKey_KeyCannotManageApiKeys()
    {
        ApiKeyCreated apiKey = await _env.CreateClient(null)
            .CreateAsync(
                new ApiKeyConfig
                {
                    ClientId = "client1",
                    Name = "key1",
                    Scopes = { Scopes.ReadFiles },
                }
            );

        HttpClient httpClient = _env.CreateHttpClientWithApiKey(apiKey.Key);
        var apiKeysClient = new ApiKeysClient(httpClient);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await apiKeysClient.GetAllAsync();
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [Test]
    [TestCase("garbage")]
    [TestCase("serval_000000000000000000000002_bm90LWEtcmVhbC1zZWNyZXQ")]
    public void AuthenticateWithApiKey_InvalidKey(string key)
    {
        DataFilesClient client = _env.CreateDataFilesClientWithApiKey(key);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.GetAsync(FILE_ID);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task AuthenticateWithApiKey_TamperedKey()
    {
        ApiKeyCreated apiKey = await _env.CreateClient(null)
            .CreateAsync(
                new ApiKeyConfig
                {
                    ClientId = "client1",
                    Name = "key1",
                    Scopes = { Scopes.ReadFiles },
                }
            );
        char lastChar = apiKey.Key[^1];
        string tamperedKey = apiKey.Key[..^1] + (lastChar == 'A' ? 'B' : 'A');

        DataFilesClient client = _env.CreateDataFilesClientWithApiKey(tamperedKey);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.GetAsync(FILE_ID);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task AuthenticateWithApiKey_RevokedKey()
    {
        ApiKeysClient apiKeysClient = _env.CreateClient(null);
        ApiKeyCreated apiKey = await apiKeysClient.CreateAsync(
            new ApiKeyConfig
            {
                ClientId = "client1",
                Name = "key1",
                Scopes = { Scopes.ReadFiles },
            }
        );
        await apiKeysClient.DeleteAsync(apiKey.Id);

        DataFilesClient client = _env.CreateDataFilesClientWithApiKey(apiKey.Key);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.GetAsync(FILE_ID);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task AuthenticateWithApiKey_ExpiredKey()
    {
        ApiKeyCreated apiKey = await _env.CreateClient(null)
            .CreateAsync(
                new ApiKeyConfig
                {
                    ClientId = "client1",
                    Name = "key1",
                    Scopes = { Scopes.ReadFiles },
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                }
            );
        await _env.ApiKeys.UpdateAsync(
            k => k.Id == apiKey.Id,
            u => u.Set(k => k.ExpiresAt, DateTime.UtcNow.AddSeconds(-1))
        );

        DataFilesClient client = _env.CreateDataFilesClientWithApiKey(apiKey.Key);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.GetAsync(FILE_ID);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task AuthenticateWithApiKey_CannotAccessOtherClientsEntities()
    {
        ApiKeyCreated apiKey = await _env.CreateClient(null)
            .CreateAsync(
                new ApiKeyConfig
                {
                    ClientId = "client2",
                    Name = "key1",
                    Scopes = { Scopes.ReadFiles },
                }
            );

        DataFilesClient client = _env.CreateDataFilesClientWithApiKey(apiKey.Key);
        ServalApiException? ex = Assert.ThrowsAsync<ServalApiException>(async () =>
        {
            await client.GetAsync(FILE_ID);
        });
        Assert.That(ex?.StatusCode, Is.EqualTo(403));
    }

    [TearDown]
    public void TearDown()
    {
        _env.Dispose();
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly IMongoClient _mongoClient;
        private readonly IServiceScope _scope;

        public TestEnvironment()
        {
            _mongoClient = new MongoClient();
            ResetDatabases();

            Factory = new ServalWebApplicationFactory();
            _scope = Factory.Services.CreateScope();
            DataFiles = _scope.ServiceProvider.GetRequiredService<IRepository<DataFiles.Models.DataFile>>();
            ApiKeys = _scope.ServiceProvider.GetRequiredService<IRepository<ApiKeys.Models.ApiKey>>();
        }

        ServalWebApplicationFactory Factory { get; }
        public IRepository<DataFiles.Models.DataFile> DataFiles { get; }
        public IRepository<ApiKeys.Models.ApiKey> ApiKeys { get; }

        public ApiKeysClient CreateClient(IEnumerable<string>? scope)
        {
            scope ??= new[] { Scopes.CreateApiKeys, Scopes.ReadApiKeys, Scopes.DeleteApiKeys };

            HttpClient httpClient = Factory.WithWebHostBuilder(_ => { }).CreateClient();
            httpClient.DefaultRequestHeaders.Add("Scope", string.Join(" ", scope));
            return new ApiKeysClient(httpClient);
        }

        public DataFilesClient CreateDataFilesClientWithApiKey(string key)
        {
            return new DataFilesClient(CreateHttpClientWithApiKey(key));
        }

        public HttpClient CreateHttpClientWithApiKey(string key)
        {
            HttpClient httpClient = Factory.WithWebHostBuilder(_ => { }).CreateClient();
            httpClient.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, key);
            return httpClient;
        }

        public void ResetDatabases()
        {
            _mongoClient.DropDatabase("serval_test");
            _mongoClient.DropDatabase("serval_test_jobs");
        }

        protected override void DisposeManagedResources()
        {
            _scope.Dispose();
            Factory.Dispose();
            ResetDatabases();
        }
    }
}
