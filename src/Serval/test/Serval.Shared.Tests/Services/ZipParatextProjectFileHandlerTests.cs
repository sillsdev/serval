namespace Serval.Shared.Services;

[TestFixture]
public class ZipParatextProjectFileHandlerTests
{
    [Test]
    public void Find_IsCaseInsensitive()
    {
        var env = new TestEnvironment();
        string firstEntry = env.ZipContainer.Entries.First();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(env.Handler.Find(".sfm"), Is.EqualTo(firstEntry));
            Assert.That(env.Handler.Find(".SFM"), Is.EqualTo(firstEntry));
            Assert.That(env.Handler.Find(".sFM"), Is.EqualTo(firstEntry));
            Assert.That(env.Handler.Find(".xml"), Is.Not.EqualTo(firstEntry));
            Assert.That(env.Handler.Find(".txt"), Is.Null);
        }
    }

    private class TestEnvironment
    {
        public TestEnvironment()
        {
            ZipContainer = Substitute.For<IZipContainer>();
            ZipContainer.Entries.Returns(["27DANBSB.sfm", "28HOSBSB.SFM", "Settings.xml"]);
            Handler = new ZipParatextProjectFileHandler(ZipContainer);
        }

        public IParatextProjectFileHandler Handler { get; }

        public IZipContainer ZipContainer { get; }
    }
}
