namespace Serval.ApiKeys.Services;

[TestFixture]
public class ApiKeyServiceTests
{
    private const string Owner = "client1";

    [Test]
    public async Task CreateAsync_DoesNotStorePlaintextKey()
    {
        var env = new TestEnvironment();
        (ApiKey apiKey, string key) = await env.Service.CreateAsync(Owner, "key1", ["read:files"], expiresAt: null);

        Assert.Multiple(() =>
        {
            Assert.That(key, Does.StartWith(ApiKeyDefaults.KeyPrefix));
            Assert.That(apiKey.HashedKey, Is.Not.Empty);
            Assert.That(key, Does.Not.Contain(apiKey.HashedKey));
        });
        ApiKey? stored = await env.ApiKeys.GetAsync(apiKey.Id);
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored.HashedKey, Is.EqualTo(apiKey.HashedKey));
    }

    [Test]
    public async Task ValidateAsync_ValidKey()
    {
        var env = new TestEnvironment();
        (ApiKey apiKey, string key) = await env.Service.CreateAsync(
            Owner,
            "key1",
            ["read:files", "create:files"],
            expiresAt: null
        );

        ApiKey? validated = await env.Service.ValidateAsync(key);

        Assert.That(validated, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(validated.Id, Is.EqualTo(apiKey.Id));
            Assert.That(validated.Owner, Is.EqualTo(Owner));
            Assert.That(validated.Scopes, Is.EqualTo(new[] { "read:files", "create:files" }));
        });
    }

    [Test]
    public async Task ValidateAsync_TamperedSecret()
    {
        var env = new TestEnvironment();
        (ApiKey _, string key) = await env.Service.CreateAsync(Owner, "key1", ["read:files"], expiresAt: null);
        char lastChar = key[^1];
        string tamperedKey = key[..^1] + (lastChar == 'A' ? 'B' : 'A');

        ApiKey? validated = await env.Service.ValidateAsync(tamperedKey);

        Assert.That(validated, Is.Null);
    }

    [Test]
    public async Task ValidateAsync_UnknownId()
    {
        var env = new TestEnvironment();
        (ApiKey apiKey, string key) = await env.Service.CreateAsync(Owner, "key1", ["read:files"], expiresAt: null);
        await env.ApiKeys.DeleteAsync(apiKey.Id);

        ApiKey? validated = await env.Service.ValidateAsync(key);

        Assert.That(validated, Is.Null);
    }

    [TestCase("")]
    [TestCase("garbage")]
    [TestCase("serval_notanobjectid_secret")]
    [TestCase("serval_0123456789abcdef01234567")]
    public async Task ValidateAsync_MalformedKey(string key)
    {
        var env = new TestEnvironment();

        ApiKey? validated = await env.Service.ValidateAsync(key);

        Assert.That(validated, Is.Null);
    }

    [Test]
    public async Task ValidateAsync_MalformedStoredHash()
    {
        var env = new TestEnvironment();
        (ApiKey apiKey, string key) = await env.Service.CreateAsync(Owner, "key1", ["read:files"], expiresAt: null);
        await env.ApiKeys.UpdateAsync(k => k.Id == apiKey.Id, u => u.Set(k => k.HashedKey, "not-a-hex-string"));

        ApiKey? validated = await env.Service.ValidateAsync(key);

        Assert.That(validated, Is.Null);
    }

    [Test]
    public async Task ValidateAsync_ExpiredKey()
    {
        var env = new TestEnvironment();
        (ApiKey apiKey, string key) = await env.Service.CreateAsync(
            Owner,
            "key1",
            ["read:files"],
            expiresAt: DateTime.UtcNow.AddMilliseconds(-1)
        );

        ApiKey? validated = await env.Service.ValidateAsync(key);

        Assert.That(validated, Is.Null);
        Assert.That(await env.ApiKeys.ExistsAsync(k => k.Id == apiKey.Id), Is.True);
    }

    [Test]
    public async Task ValidateAsync_UnexpiredKey()
    {
        var env = new TestEnvironment();
        (ApiKey _, string key) = await env.Service.CreateAsync(
            Owner,
            "key1",
            ["read:files"],
            expiresAt: DateTime.UtcNow.AddHours(1)
        );

        ApiKey? validated = await env.Service.ValidateAsync(key);

        Assert.That(validated, Is.Not.Null);
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            ApiKeys = new MemoryRepository<ApiKey>();
            Service = new ApiKeyService(ApiKeys, NullLogger<ApiKeyService>.Instance);
        }

        public MemoryRepository<ApiKey> ApiKeys { get; }
        public ApiKeyService Service { get; }
    }
}
