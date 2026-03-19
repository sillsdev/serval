using SIL.ServiceToolkit.Utils;

namespace SIL.ServiceToolkit.Services;

public class CorpusBundleTests
{
    [Test]
    public void GetSettings()
    {
        using TestEnvironment env = new(addParatext: true, addText: false);
        string fileLocation = env.CorpusBundle.ParallelCorpora[0].SourceCorpora[0].Files[0].Location;
        ParatextProjectSettings? settings = env.CorpusBundle.GetSettings(fileLocation);
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.Name, Is.EqualTo("Te1"));
        Assert.That(env.CorpusBundle.ParentOf(fileLocation), Is.Null);
    }

    [Test]
    public void GetSettings_TextFile()
    {
        using TestEnvironment env = new(addParatext: false, addText: true);
        string fileLocation = env.CorpusBundle.ParallelCorpora[0].SourceCorpora[0].Files[0].Location;
        ParatextProjectSettings? settings = env.CorpusBundle.GetSettings(fileLocation);
        Assert.That(settings, Is.Null);
        Assert.That(env.CorpusBundle.ParentOf(fileLocation), Is.Null);
    }

    [Test]
    public void GetTextUpdater()
    {
        using TestEnvironment env = new(addParatext: true, addText: false);
        string fileLocation = env.CorpusBundle.ParallelCorpora[0].SourceCorpora[0].Files[0].Location;
        using ZipParatextProjectTextUpdater updater = env.CorpusBundle.GetTextUpdater(fileLocation);
        Assert.That(
            updater.UpdateUsfm("MAT", [], textBehavior: UpdateUsfmTextBehavior.PreferExisting).ReplaceLineEndings("\n"),
            Is.EqualTo(
                    $@"\id MAT - Test
\h Matthew
\mt Matthew
\ip An introduction to Matthew
\c 1
\p
\v 1 Source one, chapter one, verse one.
\v 2-3 Source one, chapter one, verse two and three.
\v 4 Source one, chapter one, verse four.
\v 5 Source one, chapter one, verse five.
\v 6 Source one, chapter one, verse six.
\v 7-9 Source one, chapter one, verse seven, eight, and nine.
\v 10 Source one, chapter one, verse ten.
\c 2
\p
\v 1 Source one, chapter two, verse one.
\v 2 Source one, chapter two, verse two. “a quotation”
\v 3 ...
\v 4 ...
"
                )
                .IgnoreLineEndings()
        );
    }

    [Test]
    public void GetTextUpdater_TextFile()
    {
        using TestEnvironment env = new(addParatext: false, addText: true);
        string fileLocation = env.CorpusBundle.ParallelCorpora[0].SourceCorpora[0].Files[0].Location;
        Assert.Throws<InvalidDataException>(() => env.CorpusBundle.GetTextUpdater(fileLocation));
    }

    [Test]
    public void GetTextCorpora()
    {
        using TestEnvironment env = new(addParatext: true, addText: true);

        Assert.That(env.CorpusBundle.ParallelCorpora, Has.Count.EqualTo(3));

        Assert.That(env.CorpusBundle.SourceTermCorpora.Count(c => c.TextCorpora.Any()), Is.EqualTo(2));
        Assert.That(
            env.CorpusBundle.SourceTermCorpora.SelectMany(c => c.TextCorpora)
                .All(tc => tc.First().ContentType == TextRowContentType.Word)
        );
        Assert.That(env.CorpusBundle.TargetTermCorpora.Count(c => c.TextCorpora.Any()), Is.EqualTo(2));
        Assert.That(
            env.CorpusBundle.TargetTermCorpora.SelectMany(c => c.TextCorpora)
                .All(tc => tc.First().ContentType == TextRowContentType.Word)
        );

        Assert.That(env.CorpusBundle.SourceTextCorpora.SelectMany(c => c.TextCorpora).Count(), Is.EqualTo(4));
        Assert.That(
            env.CorpusBundle.SourceTextCorpora.SelectMany(c => c.TextCorpora)
                .All(tc => tc.First().ContentType == TextRowContentType.Segment)
        );
        Assert.That(env.CorpusBundle.TargetTextCorpora.SelectMany(c => c.TextCorpora).Count(), Is.EqualTo(3));
        Assert.That(
            env.CorpusBundle.TargetTextCorpora.SelectMany(c => c.TextCorpora)
                .All(tc => tc.First().ContentType == TextRowContentType.Segment)
        );
    }

    private class TestEnvironment : DisposableBase
    {
        public TestEnvironment(bool addParatext, bool addText)
        {
            CorpusBundle = new CorpusBundle(GetCorpora(addParatext, addText));
        }

        public CorpusBundle CorpusBundle { get; }

        private static readonly string TestDataPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "Services",
            "data"
        );
        private readonly TempDirectory _tempDir = new(name: "CorpusBundleTests");

        public ParallelCorpus[] GetCorpora(bool addParatext, bool addText)
        {
            List<ParallelCorpus> parallelCorpora = [];
            if (addParatext)
            {
                parallelCorpora.AddRange(
                    new ParallelCorpus
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new MonolingualCorpus
                            {
                                Id = "pt-source1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFile
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-source1"),
                                    },
                                ],
                                InferenceTextIds = [],
                            },
                        ],
                        TargetCorpora =
                        [
                            new MonolingualCorpus
                            {
                                Id = "pt-target1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFile
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-target1"),
                                    },
                                ],
                            },
                        ],
                    },
                    new ParallelCorpus
                    {
                        Id = "corpus2",
                        SourceCorpora =
                        [
                            new MonolingualCorpus
                            {
                                Id = "pt-source1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFile
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-source1"),
                                    },
                                ],
                                TrainOnTextIds = [],
                            },
                        ],
                        TargetCorpora =
                        [
                            new MonolingualCorpus
                            {
                                Id = "pt-target1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFile
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Paratext,
                                        Location = ZipParatextProject("pt-target1"),
                                    },
                                ],
                                TrainOnTextIds = [],
                            },
                        ],
                    }
                );
            }
            if (addText)
            {
                parallelCorpora.AddRange(
                    new ParallelCorpus
                    {
                        Id = "corpus1",
                        SourceCorpora =
                        [
                            new MonolingualCorpus
                            {
                                Id = "source-corpus1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFile
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Text,
                                        Location = Path.Combine(TestDataPath, "source1.txt"),
                                    },
                                ],
                            },
                            new MonolingualCorpus
                            {
                                Id = "source-corpus2",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFile
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Text,
                                        Location = Path.Combine(TestDataPath, "source2.txt"),
                                    },
                                ],
                            },
                        ],
                        TargetCorpora =
                        [
                            new MonolingualCorpus
                            {
                                Id = "target-corpus1",
                                Language = "en",
                                Files =
                                [
                                    new CorpusFile
                                    {
                                        TextId = "textId1",
                                        Format = FileFormat.Text,
                                        Location = Path.Combine(TestDataPath, "target1.txt"),
                                    },
                                ],
                            },
                        ],
                    }
                );
            }
            return parallelCorpora.ToArray();
        }

        protected override void DisposeManagedResources()
        {
            _tempDir.Dispose();
        }

        private string ZipParatextProject(string name)
        {
            string fileName = Path.Combine(_tempDir.Path, $"{name}.zip");
            if (!File.Exists(fileName))
                ZipFile.CreateFromDirectory(Path.Combine(TestDataPath, name), fileName);
            return fileName;
        }
    }
}
