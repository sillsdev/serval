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
                List = [1]
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
    public async Task UpdateAsync_Upsert()
    {
        MemoryRepository<TestEntity> repo = new();

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Set(e => e.Value, 2), upsert: true);

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Value, Is.EqualTo(2));
        Assert.That(repo.Count, Is.EqualTo(1));
        Assert.That(repo.Get("1").Value, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateAsync_SetOnInsert()
    {
        MemoryRepository<TestEntity> repo = new();

        TestEntity? entity = await repo.UpdateAsync("1", u => u.SetOnInsert(e => e.Value, 2), upsert: true);

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Value, Is.EqualTo(2));
        Assert.That(repo.Count, Is.EqualTo(1));
        Assert.That(repo.Get("1").Value, Is.EqualTo(2));

        entity = await repo.UpdateAsync("1", u => u.SetOnInsert(e => e.Value, 3), upsert: true);

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Value, Is.EqualTo(2));
        Assert.That(repo.Count, Is.EqualTo(1));
        Assert.That(repo.Get("1").Value, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateAsync_Set()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", Value = 1 });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Set(e => e.Value, 2));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Value, Is.EqualTo(2));
        Assert.That(repo.Get("1").Value, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateAsync_Unset()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", Value = 1 });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Unset(e => e.Value));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Value, Is.Null);
        Assert.That(repo.Get("1").Value, Is.Null);
    }

    [Test]
    public async Task UpdateAsync_Add_ReadOnlyList()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1" });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Add(e => e.ReadOnlyList, 1));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.ReadOnlyList, Is.EqualTo(new int[] { 1 }));
        Assert.That(repo.Get("1").ReadOnlyList, Is.EqualTo(new int[] { 1 }));

        entity = await repo.UpdateAsync("1", u => u.Add(e => e.ReadOnlyList, 2));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.ReadOnlyList, Is.EqualTo(new int[] { 1, 2 }));
        Assert.That(repo.Get("1").ReadOnlyList, Is.EqualTo(new int[] { 1, 2 }));
    }

    [Test]
    public async Task UpdateAsync_Add_List()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1" });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Add(e => e.List, 1));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.List, Is.EqualTo(new int[] { 1 }));
        Assert.That(repo.Get("1").List, Is.EqualTo(new int[] { 1 }));

        entity = await repo.UpdateAsync("1", u => u.Add(e => e.List, 2));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.List, Is.EqualTo(new int[] { 1, 2 }));
        Assert.That(repo.Get("1").List, Is.EqualTo(new int[] { 1, 2 }));
    }

    [Test]
    public async Task UpdateAsync_Add_Array()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1" });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Add(e => e.Array, 1));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Array, Is.EqualTo(new int[] { 1 }));
        Assert.That(repo.Get("1").Array, Is.EqualTo(new int[] { 1 }));

        entity = await repo.UpdateAsync("1", u => u.Add(e => e.Array, 2));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Array, Is.EqualTo(new int[] { 1, 2 }));
        Assert.That(repo.Get("1").Array, Is.EqualTo(new int[] { 1, 2 }));
    }

    [Test]
    public async Task UpdateAsync_Add_ReadOnlyCollection()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1" });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Add(e => e.ReadOnlyCollection, 1));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.ReadOnlyCollection, Is.EqualTo(new int[] { 1 }));
        Assert.That(repo.Get("1").ReadOnlyCollection, Is.EqualTo(new int[] { 1 }));

        entity = await repo.UpdateAsync("1", u => u.Add(e => e.ReadOnlyCollection, 2));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.ReadOnlyCollection, Is.EqualTo(new int[] { 1, 2 }));
        Assert.That(repo.Get("1").ReadOnlyCollection, Is.EqualTo(new int[] { 1, 2 }));
    }

    [Test]
    public async Task UpdateAsync_Remove_ReadOnlyList()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", ReadOnlyList = [1, 2] });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Remove(e => e.ReadOnlyList, 1));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.ReadOnlyList, Is.EqualTo(new int[] { 2 }));
        Assert.That(repo.Get("1").ReadOnlyList, Is.EqualTo(new int[] { 2 }));
    }

    [Test]
    public async Task UpdateAsync_Remove_List()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", List = [1, 2] });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Remove(e => e.List, 1));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.List, Is.EqualTo(new int[] { 2 }));
        Assert.That(repo.Get("1").List, Is.EqualTo(new int[] { 2 }));
    }

    [Test]
    public async Task UpdateAsync_Remove_Array()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", Array = [1, 2] });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Remove(e => e.Array, 1));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Array, Is.EqualTo(new int[] { 2 }));
        Assert.That(repo.Get("1").Array, Is.EqualTo(new int[] { 2 }));
    }

    [Test]
    public async Task UpdateAsync_Remove_ReadOnlyCollection()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", ReadOnlyCollection = new([1, 2]) });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.Remove(e => e.ReadOnlyCollection, 1));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.ReadOnlyCollection, Is.EqualTo(new int[] { 2 }));
        Assert.That(repo.Get("1").ReadOnlyCollection, Is.EqualTo(new int[] { 2 }));
    }

    [Test]
    public async Task UpdateAsync_RemoveAll_ReadOnlyList()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", ReadOnlyList = [1, 2, 2, 3] });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.RemoveAll(e => e.ReadOnlyList, i => i >= 2));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.ReadOnlyList, Is.EqualTo(new int[] { 1 }));
        Assert.That(repo.Get("1").ReadOnlyList, Is.EqualTo(new int[] { 1 }));
    }

    [Test]
    public async Task UpdateAsync_RemoveAll_List()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", List = [1, 2, 2, 3] });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.RemoveAll(e => e.List, i => i >= 2));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.List, Is.EqualTo(new int[] { 1 }));
        Assert.That(repo.Get("1").List, Is.EqualTo(new int[] { 1 }));
    }

    [Test]
    public async Task UpdateAsync_RemoveAll_Array()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", Array = [1, 2, 2, 3] });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.RemoveAll(e => e.Array, i => i >= 2));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Array, Is.EqualTo(new int[] { 1 }));
        Assert.That(repo.Get("1").Array, Is.EqualTo(new int[] { 1 }));
    }

    [Test]
    public async Task UpdateAsync_RemoveAll_ReadOnlyCollection()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(new TestEntity() { Id = "1", ReadOnlyCollection = new([1, 2, 2, 3]) });

        TestEntity? entity = await repo.UpdateAsync("1", u => u.RemoveAll(e => e.ReadOnlyCollection, i => i >= 2));

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.ReadOnlyCollection, Is.EqualTo(new int[] { 1 }));
        Assert.That(repo.Get("1").ReadOnlyCollection, Is.EqualTo(new int[] { 1 }));
    }

    [Test]
    public async Task UpdateAsync_SetAll()
    {
        MemoryRepository<TestEntity> repo = new();
        repo.Add(
            new TestEntity()
            {
                Id = "1",
                Children = [new Child { Field = 1 }, new Child { Field = 2 }, new Child { Field = 3 }]
            }
        );

        TestEntity? entity = await repo.UpdateAsync(
            "1",
            u => u.SetAll(e => e.Children, c => c.Field, 0, c => c.Field >= 2)
        );

        Assert.That(entity, Is.Not.Null);
        Assert.That(entity.Children, Is.Not.Null);
        Assert.That(entity.Children[0].Field, Is.EqualTo(1));
        Assert.That(entity.Children[1].Field, Is.EqualTo(0));
        Assert.That(entity.Children[2].Field, Is.EqualTo(0));
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
        public IReadOnlyList<int>? ReadOnlyList { get; init; }
        public List<int>? List { get; init; }
        public int[]? Array { get; init; }
        public ReadOnlyCollection<int>? ReadOnlyCollection { get; init; }
        public IReadOnlyList<Child>? Children { get; init; }
    }

    private record Child
    {
        public int Field { get; init; }
    }
}
