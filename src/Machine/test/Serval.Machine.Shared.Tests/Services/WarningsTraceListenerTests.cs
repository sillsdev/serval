namespace Serval.Machine.Shared.Services;

[TestFixture]
public class WarningsTraceListenerTests
{
    [Test]
    public void CapturesChapterParsingWarnings()
    {
        using var testEnvironment = new TestEnvironment();
        const string Chapter = "1.";

        var verseRef = new VerseRef { Chapter = Chapter };
        Assert.Multiple(() =>
        {
            Assert.That(verseRef.Chapter, Is.Empty);
            Assert.That(testEnvironment.Warnings, Has.Count.EqualTo(1));
            Assert.That(
                testEnvironment.Warnings[0],
                Is.EqualTo(testEnvironment.Prefix + "Just failed to parse a chapter number: " + Chapter)
            );
        });
    }

    [Test]
    public void CapturesVerseParsingWarnings()
    {
        using var testEnvironment = new TestEnvironment();
        const string Verse = "v1";

        var verseRef = new VerseRef { Verse = Verse };
        Assert.Multiple(() =>
        {
            Assert.That(verseRef.Chapter, Is.EqualTo("0"));
            Assert.That(testEnvironment.Warnings, Has.Count.EqualTo(1));
            Assert.That(
                testEnvironment.Warnings[0],
                Is.EqualTo(testEnvironment.Prefix + "Just failed to parse a verse number: " + Verse)
            );
        });
    }

    public class TestEnvironment : DisposableBase
    {
        private readonly WarningsTraceListener _listener;

        public TestEnvironment()
        {
            _listener = new WarningsTraceListener(Warnings, Prefix);
            Trace.Listeners.Add(_listener);
        }

        public string Prefix { get; } = "USFM Parsing error: ";
        public List<string> Warnings { get; } = [];

        protected override void DisposeManagedResources()
        {
            Trace.Listeners.Remove(_listener);
            _listener.Dispose();
        }
    }
}
