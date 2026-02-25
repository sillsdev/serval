using SIL.Scripture;

namespace SIL.ServiceToolkit.Services;

public class ParallelCorpusPreprocessingService(ITextCorpusService textCorpusService)
    : IParallelCorpusPreprocessingService
{
    private readonly ITextCorpusService _textCorpusService = textCorpusService;
    private const int Seed = 1234;

    public IReadOnlyList<(string CorpusId, IReadOnlyList<UsfmVersificationError> Errors)> AnalyzeUsfmVersification(
        ParallelCorpus parallelCorpus
    )
    {
        List<(string CorpusId, IReadOnlyList<UsfmVersificationError> Errors)> errorsPerCorpus = [];
        foreach (
            (CorpusFile file, MonolingualCorpus monolingualCorpus, bool isSource) in parallelCorpus
                .SourceCorpora.SelectMany(c =>
                    c.Files.Where(f => f.Format == FileFormat.Paratext).Select(f => (f, c, true))
                )
                .Concat(
                    parallelCorpus.TargetCorpora.SelectMany(c =>
                        c.Files.Where(f => f.Format == FileFormat.Paratext).Select(f => (f, c, false))
                    )
                )
                .DistinctBy(tuple => tuple.f.Location)
        )
        {
            using ZipArchive zipArchive = ZipFile.OpenRead(file.Location);
            IReadOnlyList<UsfmVersificationError> errors = new ZipParatextProjectVersificationErrorDetector(
                zipArchive
            ).GetUsfmVersificationErrors(books: GetBooks(monolingualCorpus, isSource));
            if (errors.Count > 0)
            {
                errorsPerCorpus.Add((monolingualCorpus.Id, errors));
            }
        }
        return errorsPerCorpus;
    }

    private static HashSet<int>? GetBooks(MonolingualCorpus corpus, bool isSource)
    {
        if (!corpus.IsFiltered)
            return null;

        List<string> books = [];
        if (corpus.TrainOnTextIds != null)
        {
            books.AddRange(corpus.TrainOnTextIds);
        }
        else if (corpus.TrainOnChapters != null)
        {
            books.AddRange(corpus.TrainOnChapters.Keys);
        }

        if (isSource)
        {
            if (corpus.InferenceTextIds != null)
            {
                books.AddRange(corpus.InferenceTextIds);
            }
            else if (corpus.InferenceChapters != null)
            {
                books.AddRange(corpus.InferenceChapters.Keys);
            }
        }
        return [.. books.Select(bookName => Canon.BookIdToNumber(bookName))];
    }

    public QuoteConventionAnalysis? AnalyzeTargetCorpusQuoteConvention(ParallelCorpus parallelCorpus)
    {
        List<QuoteConventionAnalysis> analyses = [];
        foreach (MonolingualCorpus targetMonolingualCorpus in parallelCorpus.TargetCorpora)
        {
            foreach (CorpusFile file in targetMonolingualCorpus.Files.Where(f => f.Format == FileFormat.Paratext))
            {
                using ZipArchive zipArchive = ZipFile.OpenRead(file.Location);
                var quoteConventionDetector = new ZipParatextProjectQuoteConventionDetector(zipArchive);
                Dictionary<int, List<int>>? chapters = null;
                if (targetMonolingualCorpus.TrainOnTextIds is not null)
                {
                    chapters = targetMonolingualCorpus.TrainOnTextIds.ToDictionary(
                        id => Canon.BookIdToNumber(id),
                        _ => new List<int>()
                    );
                }
                else if (targetMonolingualCorpus.TrainOnChapters is not null)
                {
                    chapters = targetMonolingualCorpus.TrainOnChapters.ToDictionary(
                        kvp => Canon.BookIdToNumber(kvp.Key),
                        kvp => kvp.Value.ToList()
                    );
                }
                if (chapters != null)
                    analyses.Add(quoteConventionDetector.GetQuoteConventionAnalysis(chapters));
                else
                    analyses.Add(quoteConventionDetector.GetQuoteConventionAnalysis());
            }
        }

        return QuoteConventionAnalysis.CombineWithWeightedAverage(analyses);
    }

    public async Task PreprocessAsync(
        IReadOnlyList<ParallelCorpus> corpora,
        Func<Row, TrainingDataType, Task> train,
        Func<Row, bool, ParallelCorpus, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    )
    {
        ignoreUsfmMarkers ??= [];

        bool parallelTrainingDataPresent = false;
        List<Row> keyTermTrainingData = new();

        // Create source and target dictionaries that map from a parallel corpus id
        // to an array of all of that parallel corpus' monolingual corpora and associated text corpora
        Dictionary<string, (MonolingualCorpus Corpus, ITextCorpus TextCorpus)[]> sourceCorpora = corpora
            .Select(corpus =>
                (
                    CorpusId: corpus.Id,
                    Corpora: corpus
                        .SourceCorpora.SelectMany(c =>
                            _textCorpusService.CreateTextCorpora(c.Files).Select(tc => (c, tc))
                        )
                        .ToArray()
                )
            )
            .ToDictionary(tup => tup.CorpusId, tup => tup.Corpora);

        Dictionary<string, (MonolingualCorpus Corpus, ITextCorpus TextCorpus)[]> targetCorpora = corpora
            .Select(corpus =>
                (
                    CorpusId: corpus.Id,
                    Corpora: corpus
                        .TargetCorpora.SelectMany(c =>
                            _textCorpusService.CreateTextCorpora(c.Files).Select(tc => (c, tc))
                        )
                        .ToArray()
                )
            )
            .ToDictionary(tup => tup.CorpusId, tup => tup.Corpora);

        // Filter the text corpora for training based on the filters specified in the monolingual corpora
        ITextCorpus[] sourceTrainingCorpora = sourceCorpora
            .Values.SelectMany(sc => sc)
            .Select(sc => FilterTrainingCorpora(sc.Corpus, sc.TextCorpus))
            .ToArray();

        ITextCorpus[] targetTrainingCorpora = targetCorpora
            .Values.SelectMany(tc => tc)
            .Select(tc => FilterTrainingCorpora(tc.Corpus, tc.TextCorpus))
            .ToArray();

        // To support mixed source, collapse multiple source text corpora into one text corpus
        // by randomly interlacing content from each of the source text corpora
        ITextCorpus sourceTrainingCorpus = sourceTrainingCorpora.ChooseRandom(Seed);
        if (sourceTrainingCorpus.IsScripture())
        {
            // Filter out all non-scripture; we only train on scripture content
            sourceTrainingCorpus = sourceTrainingCorpus.Where(IsScriptureRow);
        }

        // Instead of interlacing rows from the target text corpora randomly, just take the
        // text row from the first target text corpus that has content for that row
        ITextCorpus targetTrainingCorpus = targetTrainingCorpora.ChooseFirst();
        if (targetTrainingCorpus.IsScripture())
        {
            // Filter out all non-scripture; we only train on scripture content
            targetTrainingCorpus = targetTrainingCorpus.Where(IsScriptureRow);
        }

        // Align source and target training data
        ParallelTextRow[] trainingRows = sourceTrainingCorpus
            .AlignRows(targetTrainingCorpus, allSourceRows: true, allTargetRows: true)
            .ToArray();

        // After merging segments across ranges, run the 'train' preprocessing function
        // on each training row and record whether any parallel training data was present
        foreach (Row row in CollapseRanges(trainingRows))
        {
            await train(row, TrainingDataType.Text);
            if (!parallelTrainingDataPresent && row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
            {
                parallelTrainingDataPresent = true;
            }
        }

        if (useKeyTerms)
        {
            // Create a terms corpus for each corpus file
            ITextCorpus[]? sourceTermCorpora = _textCorpusService
                .CreateTermCorpora(
                    sourceCorpora.Values.SelectMany(sc => sc).SelectMany(corpus => corpus.Corpus.Files).ToArray()
                )
                .ToArray();
            ITextCorpus[]? targetTermCorpora = _textCorpusService
                .CreateTermCorpora(
                    targetCorpora.Values.SelectMany(tc => tc).SelectMany(corpus => corpus.Corpus.Files).ToArray()
                )
                .ToArray();

            if (sourceTermCorpora is not null && targetTermCorpora is not null)
            {
                // As with scripture data, interlace the source rows randomly
                // but choose the first non-empty target row, then align
                IParallelTextCorpus parallelKeyTermsCorpus = sourceTermCorpora
                    .ChooseRandom(Seed)
                    .AlignRows(targetTermCorpora.ChooseFirst());

                // Only train on unique key terms pairs
                foreach (
                    ParallelTextRow row in parallelKeyTermsCorpus.DistinctBy(row => (row.SourceText, row.TargetText))
                )
                {
                    keyTermTrainingData.Add(
                        new Row(row.TextId, row.SourceRefs, row.TargetRefs, row.SourceText, row.TargetText, 1)
                    );
                }
            }
        }

        // Since we ultimately need to provide inferences for a particular parallel corpus,
        // we need to preprocess the content on which to inference per parallel corpus
        foreach (ParallelCorpus corpus in corpora)
        {
            // Filter the text corpora based on the filters specified in the monolingual corpora
            ITextCorpus sourceInferencingCorpus = sourceCorpora[corpus.Id]
                .Select(sc => FilterInferencingCorpora(sc.Corpus, sc.TextCorpus, ignoreUsfmMarkers))
                .ChooseFirst();

            ITextCorpus targetInferencingCorpus = targetCorpora[corpus.Id]
                .Select(tc => FilterInferencingCorpora(tc.Corpus, tc.TextCorpus, ignoreUsfmMarkers))
                .ChooseFirst();

            // We need to align all three of these corpora because we need both the source and target
            // content for inferencing (the target is only needed in some contexts like word alignment)
            // as well as the target training corpus in order to determine whether a row was already
            // used in training.
            INParallelTextCorpus inferencingCorpus = new ITextCorpus[]
            {
                sourceInferencingCorpus,
                targetInferencingCorpus,
                targetTrainingCorpus,
            }.AlignMany([true, false, false]);

            foreach ((Row row, bool isInTrainingData) in CollapseInferencingRanges(inferencingCorpus.ToArray()))
            {
                await inference(row, isInTrainingData, corpus);
            }
        }

        // Only train on key terms if there were other parallel scripture data.
        // This is necessary to support inference-only jobs since the terms are not
        // filtered by the filters specified in the monolingual corpora.
        if (useKeyTerms && parallelTrainingDataPresent)
        {
            foreach (Row row in keyTermTrainingData)
            {
                await train(row, TrainingDataType.KeyTerms);
            }
        }
    }

    private static ITextCorpus FilterInferencingCorpora(
        MonolingualCorpus corpus,
        ITextCorpus textCorpus,
        HashSet<string> ignoreUsfmMarkers
    )
    {
        textCorpus = textCorpus.Transform(CleanSegment);
        if (corpus.InferenceTextIds is not null)
        {
            textCorpus = textCorpus.FilterTexts(corpus.InferenceTextIds);
        }
        else if (corpus.InferenceChapters is not null)
        {
            textCorpus = textCorpus
                .FilterTexts(corpus.InferenceChapters.Keys)
                .Where(row => row.Ref is not ScriptureRef sr || IsInChapters(sr, corpus.InferenceChapters));
        }
        return textCorpus.Where(row => row.Ref is not ScriptureRef sr || !HasIgnorableMarker(sr, ignoreUsfmMarkers));
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
        List<object> sourceRefs = [];
        List<object> targetRefs = [];
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
                yield return new Row(
                    textId,
                    sourceRefs,
                    targetRefs,
                    srcSegBuffer.ToString(),
                    trgSegBuffer.ToString(),
                    1
                );

                srcSegBuffer.Clear();
                trgSegBuffer.Clear();
                targetRefs.Clear();

                hasUnfinishedRange = false;
            }

            textId = row.TextId;
            sourceRefs.AddRange(row.SourceRefs);
            targetRefs.AddRange(row.TargetRefs);
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

            yield return new Row(textId, sourceRefs, targetRefs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1);

            srcSegBuffer.Clear();
            trgSegBuffer.Clear();
            sourceRefs.Clear();
            targetRefs.Clear();
        }
        if (hasUnfinishedRange)
        {
            yield return new Row(textId, sourceRefs, targetRefs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1);
        }
    }

    private static IEnumerable<(Row, bool)> CollapseInferencingRanges(NParallelTextRow[] rows)
    {
        StringBuilder srcSegBuffer = new();
        StringBuilder trgSegBuffer = new();
        List<object> sourceRefs = [];
        List<object> targetRefs = [];
        string textId = "";
        bool hasUnfinishedRange = false;
        bool isInTrainingData = false;

        foreach (NParallelTextRow row in rows)
        {
            //row at 0 is source filtered for inferencing, row at 1 is target filtered for inferencing, row at 2 is target filtered for training
            if (
                hasUnfinishedRange
                && (!row.IsInRange(0) || row.IsRangeStart(0))
                && (!row.IsInRange(1) || row.IsRangeStart(1))
                && (!row.IsInRange(2) || row.IsRangeStart(2))
            )
            {
                yield return (
                    new Row(textId, sourceRefs, targetRefs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1),
                    isInTrainingData
                );

                srcSegBuffer.Clear();
                trgSegBuffer.Clear();
                sourceRefs.Clear();
                targetRefs.Clear();
                isInTrainingData = false;
                hasUnfinishedRange = false;
            }

            textId = row.TextId;
            sourceRefs.AddRange(row.NRefs[0]);
            targetRefs.AddRange(row.NRefs[2].Count > 0 ? row.NRefs[2] : row.NRefs[1]);
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

            yield return (
                new Row(textId, sourceRefs, targetRefs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1),
                isInTrainingData
            );

            srcSegBuffer.Clear();
            trgSegBuffer.Clear();
            sourceRefs.Clear();
            targetRefs.Clear();
            isInTrainingData = false;
        }
        if (hasUnfinishedRange)
        {
            yield return (
                new Row(textId, sourceRefs, targetRefs, srcSegBuffer.ToString(), trgSegBuffer.ToString(), 1),
                isInTrainingData
            );
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

    private static bool HasIgnorableMarker(ScriptureRef sr, HashSet<string> ignoreUsfmMarkers)
    {
        return sr.Path.Any(e => ignoreUsfmMarkers.Contains(e.Name));
    }

    private static TextRow CleanSegment(TextRow row)
    {
        if (row.Text == "...")
            row.Segment = [];
        return row;
    }
}
