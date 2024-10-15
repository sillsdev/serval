namespace SIL.ServiceToolkit.Utils;

public class ParallelCorpusPreprocessor
{
    private readonly ICorpusService _corpusService;
    private int _seed = 1234;
    private Random _random;

    public ParallelCorpusPreprocessor()
    {
        _corpusService = new CorpusService();
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

    public static void PreprocessCorpora(
        IReadOnlyList<ParallelCorpus> corpora,
        Action<Row> train,
        Action<Row, ParallelCorpus> pretranslate,
        bool useKeyTerms = false
    )
    {
        new ParallelCorpusPreprocessor().Preprocess(corpora, train, pretranslate, useKeyTerms);
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
                    ITextCorpus textCorpus = sc.TextCorpus;
                    if (sc.Corpus.TrainOnTextIds is not null)
                        textCorpus = textCorpus.FilterTexts(sc.Corpus.TrainOnTextIds);
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
                    ITextCorpus textCorpus = sc.TextCorpus;
                    if (sc.Corpus.PretranslateTextIds is not null)
                        textCorpus = textCorpus.FilterTexts(sc.Corpus.PretranslateTextIds);
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
                    ITextCorpus textCorpus = tc.TextCorpus;
                    if (tc.Corpus.TrainOnTextIds is not null)
                        textCorpus = textCorpus.FilterTexts(tc.Corpus.TrainOnTextIds);
                    return textCorpus.Where(row =>
                        row.Ref is not ScriptureRef sr
                        || tc.Corpus.TrainOnChapters is null
                        || IsInChapters(sr, tc.Corpus.TrainOnChapters)
                    );
                })
                .ToArray();

            if (sourceCorpora.Length == 0)
                continue;

            int skipCount = 0;
            foreach (Row?[] rows in AlignTrainCorpus(sourceTrainingCorpora, targetTrainingCorpora))
            {
                if (skipCount > 0)
                {
                    skipCount--;
                    continue;
                }

                Row[] trainRows = rows.Where(r => r is not null).Cast<Row>().ToArray();
                if (trainRows.Length > 0)
                {
                    Row row = trainRows[0];
                    if (rows.Length > 1)
                    {
                        Row[] nonEmptyRows = trainRows.Where(r => r.SourceSegment.Length > 0).ToArray();
                        Row[] targetNonEmptyRows = nonEmptyRows.Where(r => r.TargetSegment.Length > 0).ToArray();
                        if (targetNonEmptyRows.Length > 0)
                            nonEmptyRows = targetNonEmptyRows;
                        if (nonEmptyRows.Length > 0)
                        {
                            nonEmptyRows = nonEmptyRows
                                .GroupBy(r => r.SourceSegment)
                                .Select(group => group.First())
                                .ToArray();
                            {
                                nonEmptyRows = nonEmptyRows
                                    .GroupBy(r => r.SourceSegment)
                                    .Select(group => group.First())
                                    .ToArray();
                                row = nonEmptyRows[_random.Next(nonEmptyRows.Length)];
                            }
                        }
                    }
                    skipCount = row.RowCount - 1;
                    train(row);
                }
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

            foreach (Row row in AlignPretranslateCorpus(sourcePretranslateCorpora, targetCorpora[0].TextCorpus))
            {
                if (row.SourceSegment.Length > 0)
                {
                    pretranslate(row, corpus);
                }
            }
        }
    }

    private static bool IsInChapters(ScriptureRef sr, Dictionary<string, HashSet<int>> selection)
    {
        return selection.TryGetValue(sr.Book, out HashSet<int>? chapters)
            && chapters != null
            && (chapters.Count == 0 || chapters.Contains(sr.ChapterNum));
    }

    private static IEnumerable<Row?[]> AlignTrainCorpus(
        IReadOnlyList<ITextCorpus> srcCorpora,
        IReadOnlyList<ITextCorpus> trgCorpora
    )
    {
        srcCorpora = srcCorpora.Select(sc => sc.Transform(CleanSegment)).ToArray();
        trgCorpora = trgCorpora.Select(tc => tc.Transform(CleanSegment)).ToArray();

        if (trgCorpora.All(tc => tc.IsScripture()))
        {
            return srcCorpora
                .SelectMany(sc => trgCorpora.Select(tc => AlignScripture(sc, tc)))
                .ZipMany(rows => rows.ToArray())
                // filter out every list that only contains completely empty rows
                .Where(rows => rows.Any(r => r is null || r.SourceSegment.Length > 0 || r.TargetSegment.Length > 0));
        }

        IEnumerable<Row[]> sourceOnlyRows = srcCorpora
            .SelectMany(sc => trgCorpora.Select(tc => sc.AlignRows(tc, allSourceRows: true)))
            .ZipMany(rows =>
                rows.Where(r => r.TargetSegment.Count == 0)
                    .Select(r => new Row(r.TextId, r.Refs, r.SourceText, r.TargetText, 1))
                    .ToArray()
            );

        IEnumerable<Row[]> targetRows = srcCorpora
            .SelectMany(sc => trgCorpora.Select(tc => sc.AlignRows(tc, allTargetRows: true)))
            .ZipMany(rows =>
                rows.Where(r => r.TargetSegment.Count > 0)
                    .Select(r => new Row(r.TextId, r.Refs, r.SourceText, r.TargetText, 1))
                    .ToArray()
            );

        return sourceOnlyRows
            .Concat(targetRows)
            // filter out every list that only contains completely empty rows
            .Where(rows => rows.Any(r => r.SourceSegment.Length > 0 || r.TargetSegment.Length > 0));
    }

    private static IEnumerable<Row?> AlignScripture(ITextCorpus srcCorpus, ITextCorpus trgCorpus)
    {
        int rowCount = 0;
        StringBuilder srcSegBuffer = new();
        StringBuilder trgSegBuffer = new();
        HashSet<VerseRef> vrefs = [];
        foreach (
            (VerseRef vref, string srcSegment, string trgSegment) in srcCorpus
                .ExtractScripture()
                .Select(r => (r.CorpusVerseRef, r.Text))
                .Zip(
                    trgCorpus.ExtractScripture().Select(r => r.Text),
                    (s, t) => (VerseRef: s.CorpusVerseRef, SourceSegment: s.Text, TargetSegment: t)
                )
        )
        {
            if (srcSegment == "<range>" && trgSegment == "<range>")
            {
                vrefs.UnionWith(vref.AllVerses());
                rowCount++;
            }
            else if (srcSegment == "<range>")
            {
                vrefs.UnionWith(vref.AllVerses());
                if (trgSegment.Length > 0)
                {
                    if (trgSegBuffer.Length > 0)
                        trgSegBuffer.Append(' ');
                    trgSegBuffer.Append(trgSegment);
                }
                rowCount++;
            }
            else if (trgSegment == "<range>")
            {
                vrefs.UnionWith(vref.AllVerses());
                if (srcSegment.Length > 0)
                {
                    if (srcSegBuffer.Length > 0)
                        srcSegBuffer.Append(' ');
                    srcSegBuffer.Append(srcSegment);
                }
                rowCount++;
            }
            else
            {
                if (rowCount > 0)
                {
                    yield return new(
                        vrefs.First().Book,
                        vrefs.Order().Select(v => new ScriptureRef(v)).Cast<object>().ToArray(),
                        srcSegBuffer.ToString(),
                        trgSegBuffer.ToString(),
                        rowCount
                    );
                    for (int i = 0; i < rowCount - 1; i++)
                        yield return null;
                    srcSegBuffer.Clear();
                    trgSegBuffer.Clear();
                    vrefs.Clear();
                    rowCount = 0;
                }
                vrefs.UnionWith(vref.AllVerses());
                srcSegBuffer.Append(srcSegment);
                trgSegBuffer.Append(trgSegment);
                rowCount++;
            }
        }

        if (rowCount > 0)
        {
            yield return new(
                vrefs.First().Book,
                vrefs.Order().Select(v => new ScriptureRef(v)).Cast<object>().ToArray(),
                srcSegBuffer.ToString(),
                trgSegBuffer.ToString(),
                rowCount
            );
            for (int i = 0; i < rowCount - 1; i++)
                yield return null;
        }
    }

    private static IEnumerable<Row> AlignPretranslateCorpus(ITextCorpus[] srcCorpora, ITextCorpus trgCorpus)
    {
        int rowCount = 0;
        StringBuilder srcSegBuffer = new();
        StringBuilder trgSegBuffer = new();
        List<object> refs = [];
        string textId = "";
        foreach (ParallelTextRow row in srcCorpora.SelectMany(sc => sc.AlignRows(trgCorpus, allSourceRows: true)))
        {
            if (!row.IsTargetRangeStart && row.IsTargetInRange)
            {
                refs.AddRange(row.TargetRefs);
                if (row.SourceText.Length > 0)
                {
                    if (srcSegBuffer.Length > 0)
                        srcSegBuffer.Append(' ');
                    srcSegBuffer.Append(row.SourceText);
                }
                rowCount++;
            }
            else
            {
                if (rowCount > 0)
                {
                    yield return new(textId, refs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1);
                    textId = "";
                    srcSegBuffer.Clear();
                    trgSegBuffer.Clear();
                    refs.Clear();
                    rowCount = 0;
                }

                textId = row.TextId;
                refs.AddRange(row.TargetRefs);
                srcSegBuffer.Append(row.SourceText);
                trgSegBuffer.Append(row.TargetText);
                rowCount++;
            }
        }

        if (rowCount > 0)
            yield return new(textId, refs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1);
    }

    private static TextRow CleanSegment(TextRow row)
    {
        if (row.Text == "...")
            row.Segment = [];
        return row;
    }
}
