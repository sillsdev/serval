namespace SIL.DataAccess;

[TestFixture]
public class MemoryRepositoryTests
{
    [Test]
    public async Task InsertAsync_DoesNotExist()
    {
        MemoryRepository<TestEntity> repo = new();

        await repo.InsertAsync(
            new TestEntity()
            {
                Id = "1",
                Value = 1,
                List = new List<int> { 1 }
            }
        );

        Assert.That(repo.Count, Is.EqualTo(1));
        TestEntity entity = repo.Get("1");
        Assert.That(entity.Value, Is.EqualTo(1));
        Assert.That(entity.List, Is.EqualTo(new int[] { 1 }));
    }

    [Test]
    public void InsertAsync_Exists()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", Value = 1 });

        Assert.ThrowsAsync<DuplicateKeyException>(() => repo.InsertAsync(new TestEntity() { Id = "1", Value = 1 }));
    }

    [Test]
    public async Task InsertAsync_ReadOnlyCollectionExpression()
    {
        MemoryRepository<TestEntity> repo = new();

        await repo.InsertAsync(new TestEntity() { Id = "1", List = [1] });

        Assert.That(repo.Count, Is.EqualTo(1));
        TestEntity entity = repo.Get("1");
        Assert.That(entity.List, Is.EqualTo(new int[] { 1 }));
    }

    [Test]
    public async Task UpdateAsync_DoesNotExist()
    {
        MemoryRepository<TestEntity> repo = new();

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Set(e => e.Value, 2));

        Assert.That(entity, Is.Null);
    }

    [Test]
    public async Task UpdateAsync_DoesNotExistUpsert()
    {
        MemoryRepository<TestEntity> repo = new();

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Set(e => e.Value, 2), upsert: true);

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Value, Is.EqualTo(2));
        Assert.That(repo.Count, Is.EqualTo(1));
        Assert.That(repo.Get("1").Value, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateAsync_Exists()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", Value = 1 });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Set(e => e.Value, 2));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Value, Is.EqualTo(2));
        Assert.That(repo.Count, Is.EqualTo(1));
        Assert.That(repo.Get("1").Value, Is.EqualTo(2));
    }

    [Test]
    public async Task DeleteAsync_DoesNotExist()
    {
        MemoryRepository<TestEntity> repo = new();

        TestEntity? entity = await repo.DeleteAsync("1");

        Assert.That(entity, Is.Null);
    }

    [Test]
    public async Task DeleteAsync_Exists()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", Value = 1 });
        Assert.That(repo.Count, Is.EqualTo(1));

        TestEntity? entity = await repo.DeleteAsync("1");

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Id, Is.EqualTo("1"));
        Assert.That(repo.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAsync_DoesNotExist()
    {
        MemoryRepository<TestEntity> repo = new();

        TestEntity? entity = await repo.GetAsync("1");

        Assert.That(entity, Is.Null);
    }

    [Test]
    public async Task GetAsync_Exists()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", Value = 1 });
        Assert.That(repo.Count, Is.EqualTo(1));

        TestEntity? entity = await repo.GetAsync("1");

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Id, Is.EqualTo("1"));
    }

    private record TestEntity : IEntity
    {
        public string Id { get; set; } = "";
        public int Revision { get; set; } = 1;
        public int? Value { get; init; }
        public IReadOnlyList<int>? List { get; init; }
    }
}
