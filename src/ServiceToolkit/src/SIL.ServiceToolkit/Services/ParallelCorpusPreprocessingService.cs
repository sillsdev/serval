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

    public void Preprocess(
        IReadOnlyList<ParallelCorpus> corpora,
        Action<Row> train,
        Action<Row, ParallelCorpus> pretranslate,
        bool useKeyTerms = false
    )
    {
        foreach (ParallelCorpus corpus in corpora)
        {
            (MonolingualCorpus Corpus, ITextCorpus TextCorpus)[] sourceCorpora = corpus
                .SourceCorpora.SelectMany(c => _corpusService.CreateTextCorpora(c.Files).Select(tc => (c, tc)))
                .ToArray();
            ITextCorpus[] sourceTrainingCorpora = sourceCorpora
                .Select(sc =>
                {
                    ITextCorpus textCorpus = sc.TextCorpus.Transform(CleanSegment);
                    if (sc.Corpus.TrainOnTextIds is not null)
                        return textCorpus.FilterTexts(sc.Corpus.TrainOnTextIds);
                    return textCorpus.Where(row =>
                        row.Ref is not ScriptureRef sr
                        || sc.Corpus.TrainOnChapters is null
                        || IsInChapters(sr, sc.Corpus.TrainOnChapters)
                    );
                })
                .ToArray();
            ITextCorpus[] sourcePretranslateCorpora = sourceCorpora
                .Select(sc =>
                {
                    ITextCorpus textCorpus = sc.TextCorpus.Transform(CleanSegment);
                    if (sc.Corpus.PretranslateTextIds is not null)
                    {
                        return textCorpus.FilterTexts(
                            sc.Corpus.PretranslateTextIds.Except(sc.Corpus.TrainOnTextIds ?? new())
                        );
                    }
                    return textCorpus.Where(row =>
                        row.Ref is not ScriptureRef sr
                        || sc.Corpus.PretranslateChapters is null
                        || (
                            IsInChapters(sr, sc.Corpus.PretranslateChapters)
                            && !IsInChapters(sr, sc.Corpus.TrainOnChapters ?? new())
                        )
                    );
                })
                .ToArray();

            (MonolingualCorpus Corpus, ITextCorpus TextCorpus)[] targetCorpora = corpus
                .TargetCorpora.SelectMany(c => _corpusService.CreateTextCorpora(c.Files).Select(tc => (c, tc)))
                .ToArray();
            ITextCorpus[] targetTrainingCorpora = targetCorpora
                .Select(tc =>
                {
                    ITextCorpus textCorpus = tc.TextCorpus.Transform(CleanSegment);
                    if (tc.Corpus.TrainOnTextIds is not null)
                        return textCorpus = textCorpus.FilterTexts(tc.Corpus.TrainOnTextIds);
                    return textCorpus.Where(row =>
                        row.Ref is not ScriptureRef sr
                        || tc.Corpus.TrainOnChapters is null
                        || IsInChapters(sr, tc.Corpus.TrainOnChapters)
                    );
                })
                .ToArray();

            if (sourceCorpora.Length == 0)
                continue;

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
                train(row);
            }

            if (useKeyTerms)
            {
                ITextCorpus? sourceTermCorpus = _corpusService
                    .CreateTermCorpora(corpus.SourceCorpora.SelectMany(sc => sc.Files).ToList())
                    .FirstOrDefault();
                ITextCorpus? targetTermCorpus = _corpusService
                    .CreateTermCorpora(corpus.TargetCorpora.SelectMany(tc => tc.Files).ToList())
                    .FirstOrDefault();
                if (sourceTermCorpus is not null && targetTermCorpus is not null)
                {
                    IParallelTextCorpus parallelKeyTermsCorpus = sourceTermCorpus.AlignRows(targetTermCorpus);
                    foreach (ParallelTextRow row in parallelKeyTermsCorpus)
                    {
                        train(new Row(row.TextId, row.Refs, row.SourceText, row.TargetText, 1));
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
                pretranslate(row, corpus);
            }
        }
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
