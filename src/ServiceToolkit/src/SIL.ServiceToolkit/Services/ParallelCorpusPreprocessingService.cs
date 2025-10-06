namespace SIL.ServiceToolkit.Services;

public class ParallelCorpusPreprocessingService(ITextCorpusService textCorpusService)
    : IParallelCorpusPreprocessingService
{
    private readonly ITextCorpusService _textCorpusService = textCorpusService;
    private const int Seed = 1234;

    public QuoteConventionAnalysis? AnalyzeTargetCorpusQuoteConvention(ParallelCorpus parallelCorpus)
    {
        var targetHandler = new QuoteConventionDetector();
        foreach (MonolingualCorpus targetMonolingualCorpus in parallelCorpus.TargetCorpora)
        {
            foreach (CorpusFile file in targetMonolingualCorpus.Files.Where(f => f.Format == FileFormat.Paratext))
            {
                using ZipArchive zipArchive = ZipFile.OpenRead(file.Location);
                var quoteConventionDetector = new ZipParatextProjectQuoteConventionDetector(zipArchive);
                Dictionary<string, List<int>>? chapters = null;
                if (targetMonolingualCorpus.TrainOnTextIds is not null)
                {
                    chapters = targetMonolingualCorpus.TrainOnTextIds.ToDictionary(id => id, _ => new List<int>());
                }
                else if (targetMonolingualCorpus.TrainOnChapters is not null)
                {
                    chapters = targetMonolingualCorpus.TrainOnChapters.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.ToList()
                    );
                }
                quoteConventionDetector.GetQuoteConventionAnalysis(targetHandler, chapters);
            }
        }

        return targetHandler.DetectQuoteConvention();
    }

    public async Task PreprocessAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, Task> train,
        Func<Row, bool, ParallelCorpus, Task> inference,
        bool useKeyTerms = false
    )
    {
        bool parallelTrainingDataPresent = false;
        List<Row> keyTermTrainingData = new();
        foreach (ParallelCorpus corpus in corpora)
        {
            (MonolingualCorpus Corpus, ITextCorpus TextCorpus)[] sourceCorpora = corpus
                .SourceCorpora.SelectMany(c => _textCorpusService.CreateTextCorpora(c.Files).Select(tc => (c, tc)))
                .ToArray();

            if (sourceCorpora.Length == 0)
                continue;

            ITextCorpus[] sourceTrainingCorpora = sourceCorpora
                .Select(sc => FilterTrainingCorpora(sc.Corpus, sc.TextCorpus))
                .ToArray();

            ITextCorpus[] sourcePretranslateCorpora = sourceCorpora
                .Select(sc => FilterPretranslateCorpora(sc.Corpus, sc.TextCorpus))
                .ToArray();

            (MonolingualCorpus Corpus, ITextCorpus TextCorpus)[] targetCorpora = corpus
                .TargetCorpora.SelectMany(c => _textCorpusService.CreateTextCorpora(c.Files).Select(tc => (c, tc)))
                .ToArray();

            ITextCorpus[] targetTrainingCorpora = targetCorpora
                .Select(tc => FilterTrainingCorpora(tc.Corpus, tc.TextCorpus))
                .ToArray();

            ITextCorpus targetPretranslateCorpus = targetCorpora
                .Select(tc => FilterPretranslateCorpora(tc.Corpus, tc.TextCorpus))
                .ToArray()
                .ChooseRandom(Seed);

            ITextCorpus sourceTrainingCorpus = sourceTrainingCorpora.ChooseRandom(Seed);
            if (sourceTrainingCorpus.IsScripture())
            {
                sourceTrainingCorpus = sourceTrainingCorpus.Where(IsScriptureRow);
            }

            ITextCorpus targetCorpus = targetTrainingCorpora.ChooseFirst();

            ITextCorpus targetTrainingCorpus = targetCorpus;
            if (targetTrainingCorpus.IsScripture())
            {
                targetTrainingCorpus = targetTrainingCorpus.Where(IsScriptureRow);
            }

            ParallelTextRow[] trainingRows = sourceTrainingCorpus
                .AlignRows(targetTrainingCorpus, allSourceRows: true, allTargetRows: true)
                .ToArray();

            foreach (Row row in CollapseRanges(trainingRows))
            {
                await train(row);
                if (!parallelTrainingDataPresent && row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                {
                    parallelTrainingDataPresent = true;
                }
            }

            if (useKeyTerms)
            {
                ITextCorpus[]? sourceTermCorpora = _textCorpusService
                    .CreateTermCorpora(sourceCorpora.SelectMany(corpus => corpus.Corpus.Files).ToArray())
                    .ToArray();
                ITextCorpus[]? targetTermCorpora = _textCorpusService
                    .CreateTermCorpora(targetCorpora.SelectMany(corpus => corpus.Corpus.Files).ToArray())
                    .ToArray();
                if (sourceTermCorpora is not null && targetTermCorpora is not null)
                {
                    IParallelTextCorpus parallelKeyTermsCorpus = sourceTermCorpora
                        .ChooseRandom(Seed)
                        .AlignRows(targetTermCorpora.ChooseFirst());
                    foreach (
                        ParallelTextRow row in parallelKeyTermsCorpus.DistinctBy(row =>
                            (row.SourceText, row.TargetText)
                        )
                    )
                    {
                        keyTermTrainingData.Add(new Row(row.TextId, row.Refs, row.SourceText, row.TargetText, 1));
                    }
                }
            }
            ITextCorpus sourcePretranslateCorpus = sourcePretranslateCorpora.ChooseFirst();

            INParallelTextCorpus pretranslateCorpus = new ITextCorpus[]
            {
                sourcePretranslateCorpus,
                targetPretranslateCorpus,
                targetCorpus
            }.AlignMany([true, false, false]);

            foreach ((Row row, bool isInTrainingData) in CollapsePretranslateRanges(pretranslateCorpus.ToArray()))
            {
                await inference(row, isInTrainingData, corpus);
            }
        }

        if (useKeyTerms && parallelTrainingDataPresent)
        {
            foreach (Row row in keyTermTrainingData)
            {
                await train(row);
            }
        }
    }

    private static ITextCorpus FilterPretranslateCorpora(MonolingualCorpus corpus, ITextCorpus textCorpus)
    {
        textCorpus = textCorpus.Transform(CleanSegment);
        if (corpus.InferenceTextIds is not null)
        {
            return textCorpus.FilterTexts(corpus.InferenceTextIds);
        }
        if (corpus.InferenceChapters is not null)
        {
            return textCorpus
                .FilterTexts(corpus.InferenceChapters.Keys)
                .Where(row => row.Ref is not ScriptureRef sr || IsInChapters(sr, corpus.InferenceChapters));
        }
        return textCorpus;
    }

    private static ITextCorpus FilterTrainingCorpora(MonolingualCorpus corpus, ITextCorpus textCorpus)
    {
        textCorpus = textCorpus.Transform(CleanSegment);
        if (corpus.TrainOnTextIds is not null)
        {
            return textCorpus.FilterTexts(corpus.TrainOnTextIds);
        }
        if (corpus.TrainOnChapters is not null)
        {
            return textCorpus
                .FilterTexts(corpus.TrainOnChapters.Keys)
                .Where(row => row.Ref is not ScriptureRef sr || IsInChapters(sr, corpus.TrainOnChapters));
        }
        return textCorpus;
    }

    private static IEnumerable<Row> CollapseRanges(ParallelTextRow[] rows)
    {
        StringBuilder srcSegBuffer = new();
        StringBuilder trgSegBuffer = new();
        List<object> refs = [];
        string textId = "";
        bool hasUnfinishedRange = false;

        foreach (ParallelTextRow row in rows)
        {
            if (
                hasUnfinishedRange
                && (!row.IsTargetInRange || row.IsTargetRangeStart)
                && (!row.IsSourceInRange || row.IsSourceRangeStart)
            )
            {
                yield return new Row(textId, refs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1);

                srcSegBuffer.Clear();
                trgSegBuffer.Clear();
                refs.Clear();

                hasUnfinishedRange = false;
            }

            textId = row.TextId;
            refs.AddRange(row.TargetRefs);
            if (row.SourceText.Length > 0)
            {
                if (srcSegBuffer.Length > 0)
                    srcSegBuffer.Append(' ');
                srcSegBuffer.Append(row.SourceText);
            }
            if (row.TargetText.Length > 0)
            {
                if (trgSegBuffer.Length > 0)
                    trgSegBuffer.Append(' ');
                trgSegBuffer.Append(row.TargetText);
            }

            if (row.IsTargetInRange || row.IsSourceInRange)
            {
                hasUnfinishedRange = true;
                continue;
            }

            yield return new Row(textId, refs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1);

            srcSegBuffer.Clear();
            trgSegBuffer.Clear();
            refs.Clear();
        }
        if (hasUnfinishedRange)
        {
            yield return new Row(textId, refs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1);
        }
    }

    private static IEnumerable<(Row, bool)> CollapsePretranslateRanges(NParallelTextRow[] rows)
    {
        StringBuilder srcSegBuffer = new();
        StringBuilder trgSegBuffer = new();
        List<object> refs = [];
        string textId = "";
        bool hasUnfinishedRange = false;
        bool isInTrainingData = false;

        foreach (NParallelTextRow row in rows)
        {
            //row at 0 is source filtered for pretranslation, row at 1 is target filtered for pretranslation, row at 2 is target filtered for training
            if (
                hasUnfinishedRange
                && (!row.IsInRange(0) || row.IsRangeStart(0))
                && (!row.IsInRange(1) || row.IsRangeStart(1))
                && (!row.IsInRange(2) || row.IsRangeStart(2))
            )
            {
                yield return (
                    new Row(textId, refs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1),
                    isInTrainingData
                );

                srcSegBuffer.Clear();
                trgSegBuffer.Clear();
                refs.Clear();
                isInTrainingData = false;
                hasUnfinishedRange = false;
            }

            textId = row.TextId;
            refs.AddRange(row.NRefs[2].Count > 0 ? row.NRefs[2] : row.NRefs[1]);
            isInTrainingData = isInTrainingData || row.Text(2).Length > 0;

            if (row.Text(0).Length > 0)
            {
                if (srcSegBuffer.Length > 0)
                    srcSegBuffer.Append(' ');
                srcSegBuffer.Append(row.Text(0));
            }
            if (row.Text(1).Length > 0)
            {
                if (trgSegBuffer.Length > 0)
                    trgSegBuffer.Append(' ');
                trgSegBuffer.Append(row.Text(1));
            }

            if (row.IsInRange(0) || row.IsInRange(1) || row.IsInRange(2))
            {
                hasUnfinishedRange = true;
                continue;
            }

            yield return (new Row(textId, refs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1), isInTrainingData);

            srcSegBuffer.Clear();
            trgSegBuffer.Clear();
            refs.Clear();
            isInTrainingData = false;
        }
        if (hasUnfinishedRange)
        {
            yield return (new Row(textId, refs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1), isInTrainingData);
        }
    }

    private static bool IsScriptureRow(TextRow parallelTextRow)
    {
        return parallelTextRow.Ref is ScriptureRef sr && sr.IsVerse;
    }

    private static bool IsInChapters(ScriptureRef sr, Dictionary<string, HashSet<int>> selection)
    {
        return selection.TryGetValue(sr.Book, out HashSet<int>? chapters)
            && chapters != null
            && (chapters.Count == 0 || chapters.Contains(sr.ChapterNum));
    }

    private static TextRow CleanSegment(TextRow row)
    {
        if (row.Text == "...")
            row.Segment = [];
        return row;
    }
}
