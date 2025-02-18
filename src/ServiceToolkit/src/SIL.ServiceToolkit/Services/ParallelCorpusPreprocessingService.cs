namespace SIL.ServiceToolkit.Services;

public class ParallelCorpusPreprocessingService : IParallelCorpusPreprocessingService
{
    private readonly ICorpusService _corpusService;
    private int _seed = 1234;
    private Random _random;

    public ParallelCorpusPreprocessingService(ICorpusService corpusService)
    {
        _corpusService = corpusService;
        _random = new Random(_seed);
    }

    internal int Seed
    {
        get => _seed;
        set
        {
            if (_seed != value)
            {
                _seed = value;
                _random = new Random(_seed);
            }
        }
    }

    public async Task PreprocessAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, Task> train,
        Func<Row, ParallelCorpus, Task> pretranslate,
        bool useKeyTerms = false
    )
    {
        bool parallelTrainingDataPresent = false;
        List<Row> keyTermTrainingData = new();
        foreach (ParallelCorpus corpus in corpora)
        {
            (MonolingualCorpus Corpus, ITextCorpus TextCorpus)[] sourceCorpora = corpus
                .SourceCorpora.SelectMany(c => _corpusService.CreateTextCorpora(c.Files).Select(tc => (c, tc)))
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
                .TargetCorpora.SelectMany(c => _corpusService.CreateTextCorpora(c.Files).Select(tc => (c, tc)))
                .ToArray();

            ITextCorpus[] targetTrainingCorpora = targetCorpora
                .Select(tc => FilterTrainingCorpora(tc.Corpus, tc.TextCorpus))
                .ToArray();

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
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                {
                    parallelTrainingDataPresent = true;
                }
            }

            if (useKeyTerms)
            {
                ITextCorpus[]? sourceTermCorpora = _corpusService
                    .CreateTermCorpora(sourceCorpora.SelectMany(corpus => corpus.Corpus.Files).ToArray())
                    .ToArray();
                ITextCorpus[]? targetTermCorpora = _corpusService
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

            IParallelTextCorpus pretranslateCorpus = sourcePretranslateCorpus.AlignRows(
                targetCorpus,
                allSourceRows: true
            );

            foreach (Row row in CollapseRanges(pretranslateCorpus.ToArray()))
            {
                await pretranslate(row, corpus);
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

    private static IEnumerable<(CorpusFile File, Dictionary<string, HashSet<int>> Chapters)> GetChaptersPerFile(
        MonolingualCorpus mc,
        ITextCorpus tc
    )
    {
        Dictionary<string, HashSet<int>>? chapters = mc.TrainOnChapters;
        if (chapters is null && mc.TrainOnTextIds is not null)
        {
            chapters = mc.TrainOnTextIds.Select(tid => (tid, new HashSet<int> { })).ToDictionary();
        }
        chapters ??= tc.Texts.Select(t => (t.Id, new HashSet<int>() { })).ToDictionary();
        return mc.Files.Select(f => (f, chapters));
    }

    private static ITextCorpus FilterPretranslateCorpora(MonolingualCorpus corpus, ITextCorpus textCorpus)
    {
        textCorpus = textCorpus.Transform(CleanSegment);
        if (corpus.PretranslateTextIds is not null)
        {
            return textCorpus.FilterTexts(corpus.PretranslateTextIds);
        }
        if (corpus.PretranslateChapters is not null)
        {
            return textCorpus
                .FilterTexts(corpus.PretranslateChapters.Keys)
                .Where(row => row.Ref is not ScriptureRef sr || IsInChapters(sr, corpus.PretranslateChapters));
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
