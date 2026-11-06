namespace Serval.Shared.Services;

[TestFixture]
public class ZipContainerTests
{
    [Test]
    public void EntryExists_IsCaseInsensitive()
    {
        using var env = new TestEnvironment();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(env.ZipContainer.EntryExists("27DANBSB.sfm"), Is.True);
            Assert.That(env.ZipContainer.EntryExists("27danbsb.sfm"), Is.True);
            Assert.That(env.ZipContainer.EntryExists("27DANBSB.SFM"), Is.True);
            Assert.That(env.ZipContainer.EntryExists("28HOSBSB.SFM"), Is.False);
        }
    }

    [Test]
    public async Task OpenEntry_IsCaseInsensitive()
    {
        using var env = new TestEnvironment();

        await using Stream exactCaseStream = env.ZipContainer.OpenEntry("27DANBSB.sfm");
        Assert.That(exactCaseStream, Is.Not.Null);

        await using Stream lowerCaseStream = env.ZipContainer.OpenEntry("27danbsb.sfm");
        Assert.That(lowerCaseStream, Is.Not.Null);

        await using Stream upperCaseStream = env.ZipContainer.OpenEntry("27DANBSB.SFM");
        Assert.That(upperCaseStream, Is.Not.Null);

        IZipContainer zipContainer = env.ZipContainer;
        Assert.Throws<ArgumentException>(() => zipContainer.OpenEntry("28HOSBSB.SFM"));
    }

    private class TestEnvironment : DisposableBase
    {
        private readonly string _tempFilePath;

        public TestEnvironment()
        {
            // Create a temporary zip file on disk
            _tempFilePath = Path.GetTempFileName();
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                ZipArchiveEntry entry = archive.CreateEntry("27DANBSB.sfm");
                using var writer = new StreamWriter(entry.Open());
                writer.Write(@"\id DAN \c 1 \v 1 Test data");
            }

            File.WriteAllBytes(_tempFilePath, memoryStream.ToArray());
            ZipContainer = new ZipContainer(_tempFilePath);
        }

        public IZipContainer ZipContainer { get; }

        protected override void DisposeManagedResources()
        {
            ZipContainer.Dispose();

            // Clean up the temporary file after tests complete
            if (File.Exists(_tempFilePath))
                File.Delete(_tempFilePath);
        }
    }
}
