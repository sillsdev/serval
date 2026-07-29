namespace Serval.Machine.WordAlignment.Services;

[TestFixture]
public class PreprocessBuildJobTests
{
    [Test]
    public async Task ProcessWordAlignmentRowsAsync()
    {
        PreprocessStats stats = new();

        var stream = Substitute.For<Stream>();
        stream.CanWrite.Returns(true);

        var sourceTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> sourceSegments = [];
        sourceTrainWriter.When(x => x.WriteAsync(Arg.Any<string>())).Do(c => sourceSegments.Add(c.ArgAt<string>(0)));

        var targetTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> targetSegments = [];
        targetTrainWriter.When(x => x.WriteAsync(Arg.Any<string>())).Do(c => targetSegments.Add(c.ArgAt<string>(0)));

        var sourceKeyTermsTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> sourceKeyTermSegments = [];
        sourceKeyTermsTrainWriter
            .When(x => x.WriteAsync(Arg.Any<string>()))
            .Do(c => sourceKeyTermSegments.Add(c.ArgAt<string>(0)));

        var targetKeyTermsTrainWriter = Substitute.For<StreamWriter>(stream);
        List<string> targetKeyTermSegments = [];
        targetKeyTermsTrainWriter
            .When(x => x.WriteAsync(Arg.Any<string>()))
            .Do(c => targetKeyTermSegments.Add(c.ArgAt<string>(0)));

        (ParallelRowContract Row, TrainingDataType Type)[] trainingRows =
        [
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:1")],
                    [ScriptureRef.Parse("MAT 1:1")],
                    "Source Matthew 1:1",
                    "Target Matthew 1:1",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:2")],
                    [ScriptureRef.Parse("MAT 1:2")],
                    "Source Matthew 1:2",
                    "",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 2:1")],
                    [ScriptureRef.Parse("MAT 2:1")],
                    "Source Matthew 2:1",
                    "Target Matthew 2:1",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MRK",
                    [ScriptureRef.Parse("MRK 1:1")],
                    [ScriptureRef.Parse("MRK 1:1")],
                    "Source Mark 1:1",
                    "Target Mark 1:1",
                    1
                ),
                TrainingDataType.Text
            ),
            (
                new(
                    "MRK",
                    ["MajorBiblicalTerms:Isaac"],
                    ["MajorBiblicalTerms:Isaac"],
                    "Source Isaac",
                    "Target Isaac",
                    1
                ),
                TrainingDataType.KeyTerm
            ),
        ];
        foreach ((ParallelRowContract row, TrainingDataType type) in trainingRows)
        {
            await stats.ProcessWordAlignmentTrainingRowAsync(
                row,
                type,
                sourceTrainWriter,
                targetTrainWriter,
                sourceKeyTermsTrainWriter,
                targetKeyTermsTrainWriter
            );
        }

        (ParallelRowContract Row, bool IsInTrainingData)[] wordAlignRows =
        [
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:1/0:s")],
                    [ScriptureRef.Parse("MAT 1:1/0:s")],
                    "Source Matthew section header",
                    "",
                    1
                ),
                false
            ),
            (
                new(
                    "MAT",
                    [ScriptureRef.Parse("MAT 1:1")],
                    [ScriptureRef.Parse("MAT 1:1")],
                    "Source Matthew 1:1",
                    "Target Matthew 1:1",
                    1
                ),
                true
            ),
            (
                new("JHN", [ScriptureRef.Parse("JHN 1:1")], [ScriptureRef.Parse("JHN 1:1")], "Source John 1:1", "", 1),
                false
            ),
            (
                new(
                    "JHN",
                    [ScriptureRef.Parse("JHN 1:2")],
                    [ScriptureRef.Parse("JHN 1:2")],
                    "Source John 1:2",
                    "Target John 1:2",
                    1
                ),
                false
            ),
        ];

        using MemoryStream memoryStream = new();
        using (Utf8JsonWriter wordAlignmentWriter = new(memoryStream))
        {
            wordAlignmentWriter.WriteStartArray();
            foreach ((ParallelRowContract row, bool isInTrainingData) in wordAlignRows)
            {
                await stats.ProcessWordAlignRowAsync(row, isInTrainingData, "corpus1", wordAlignmentWriter);
            }
            wordAlignmentWriter.WriteEndArray();
        }

        memoryStream.Seek(0, SeekOrigin.Begin);
        StreamReader wordAlignmentReader = new(memoryStream);
        string wordAlignmentContent = await wordAlignmentReader.ReadToEndAsync();

        Assert.That(
            sourceSegments.SequenceEqual(["Source Matthew 1:1\n", "Source Matthew 2:1\n", "Source Mark 1:1\n"])
        );
        Assert.That(
            targetSegments.SequenceEqual(["Target Matthew 1:1\n", "Target Matthew 2:1\n", "Target Mark 1:1\n"])
        );
        Assert.That(sourceKeyTermSegments.SequenceEqual(["Source Isaac\n"]));
        Assert.That(targetKeyTermSegments.SequenceEqual(["Target Isaac\n"]));
        Assert.That(stats.TrainCount, Is.EqualTo(4));
        Assert.That(stats.TrainVerseCount, Has.Count.EqualTo(2));

        Assert.That(stats.TrainVerseCount, Does.ContainKey("MAT"));
        Assert.That(stats.TrainVerseCount["MAT"], Has.Count.EqualTo(2));
        Assert.That(stats.TrainVerseCount["MAT"], Does.ContainKey("1"));
        Assert.That(stats.TrainVerseCount["MAT"]["1"], Is.EqualTo(1));
        Assert.That(stats.TrainVerseCount["MAT"], Does.ContainKey("2"));
        Assert.That(stats.TrainVerseCount["MAT"]["2"], Is.EqualTo(1));

        Assert.That(stats.TrainVerseCount, Does.ContainKey("MRK"));
        Assert.That(stats.TrainVerseCount["MRK"], Has.Count.EqualTo(1));
        Assert.That(stats.TrainVerseCount["MRK"], Does.ContainKey("1"));
        Assert.That(stats.TrainVerseCount["MRK"]["1"], Is.EqualTo(1));

        Assert.That(
            wordAlignmentContent,
            Is.EqualTo(
                "["
                    + "{\"corpusId\":\"corpus1\",\"textId\":\"JHN\",\"sourceRefs\":[\"JHN 1:2\"],\"targetRefs\":[\"JHN 1:2\"],\"source\":\"Source John 1:2\",\"target\":\"Target John 1:2\"}"
                    + "]"
            )
        );
        Assert.That(stats.InferenceCount, Is.EqualTo(1));
        Assert.That(stats.InferenceVerseCount, Has.Count.EqualTo(1));
        Assert.That(stats.InferenceVerseCount, Does.ContainKey("JHN"));
        Assert.That(stats.InferenceVerseCount["JHN"], Has.Count.EqualTo(1));
        Assert.That(stats.InferenceVerseCount["JHN"], Does.ContainKey("1"));
        Assert.That(stats.InferenceVerseCount["JHN"]["1"], Is.EqualTo(1));
    }
}
