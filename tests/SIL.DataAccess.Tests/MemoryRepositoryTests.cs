namespace SIL.DataAccess;

[TestFixture]
public class MemoryRepositoryTests
{
    [Test]
    public async Task AddAndRemove()
    {
        var mr = new MemoryRepository<IntegerEntity>();
        mr.Init();
        mr.Add(new IntegerEntity(1) { Id = "1" });
        var entityToRemove = new IntegerEntity(2) { Id = "2" };
        mr.Add(entityToRemove);
        mr.Remove(entityToRemove);
        Assert.That((await mr.GetAllAsync(_ => true)).Count, Is.EqualTo(1));
        IntegerEntity entity = mr.Get("1");
        Assert.That(entity.Value, Is.EqualTo(1));
        mr.Add(
            new IntegerEntity[]
            {
                new IntegerEntity(1) { Id = "3" },
                new IntegerEntity(2) { Id = "4" }
            }
        );
        Assert.That((await mr.GetAllAsync(_ => true)).Count, Is.EqualTo(3));
        Assert.That(await mr.ExistsAsync(entity => entity.Value == 2));
    }

    [Test]
    public async Task InsertAndUpdate()
    {
        var mr = new MemoryRepository<IntegerEntity>();
        mr.Init();
        await mr.InsertAllAsync(
            new IntegerEntity[]
            {
                new IntegerEntity(1) { Id = "1" },
                new IntegerEntity(2) { Id = "2" }
            }
        );
        Assert.ThrowsAsync<DuplicateKeyException>(async () =>
        {
            await mr.InsertAllAsync(
                new IntegerEntity[]
                {
                    new IntegerEntity(1) { Id = "1" },
                    new IntegerEntity(2) { Id = "2" }
                }
            );
        });
        Assert.That((await mr.GetAllAsync(_ => true)).Count, Is.EqualTo(2));
        await mr.UpdateAsync(e => e.Id == "0", e => e.Set(r => r.Value, 0), upsert: true);
        Assert.That((await mr.GetAllAsync(_ => true)).Count, Is.EqualTo(3));
        await mr.UpdateAsync(e => e.Id == "0", e => e.Set(r => r.Value, 100));
        Assert.That((await mr.GetAllAsync(_ => true)).Count, Is.EqualTo(3));
        Assert.That(mr.Get("0").Value, Is.EqualTo(100));
        await mr.UpdateAsync(e => e.Id == "1", e => e.Set(r => r.Value, 100));
        await mr.UpdateAllAsync(e => e.Value == 100, e => e.Set(r => r.Value, -100));
        Assert.That(mr.Get("0").Value, Is.EqualTo(-100));
        Assert.That(mr.Get("1").Value, Is.EqualTo(-100));
    }

    private class IntegerEntity : IEntity
    {
        public string Id { get; set; } = default!;
        public int Revision { get; set; } = 1;
        public int Value { get; set; }

        public IntegerEntity(int value)
        {
            Value = value;
        }

        public IntegerEntity() { }
    }
}
