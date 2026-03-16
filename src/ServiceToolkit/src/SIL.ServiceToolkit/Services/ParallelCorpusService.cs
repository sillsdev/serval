using System.Globalization;
using SIL.Machine.Translation;
using SIL.Scripture;

namespace SIL.ServiceToolkit.Services;

public class ParallelCorpusService : IParallelCorpusService
{
    private const int Seed = 1234;

    public IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        IReadOnlyList<UsfmVersificationError> Errors
    )> AnalyzeUsfmVersification(IEnumerable<ParallelCorpus> parallelCorpora)
    {
        CorpusBundle corpusBundle = new(parallelCorpora);
        List<(
            string ParallelCorpusId,
            string MonolingualCorpusId,
            IReadOnlyList<UsfmVersificationError> Errors
        )> errorsPerCorpus = [];
        foreach (
            (
                ParallelCorpus parallelCorpus,
                MonolingualCorpus monolingualCorpus,
                IReadOnlyList<CorpusFile> files,
                _
            ) in corpusBundle.TextCorpora
        )
        {
            foreach (CorpusFile file in files.Where(f => f.Format == FileFormat.Paratext))
            {
                using ZipArchive zipArchive = ZipFile.OpenRead(file.Location);
                IReadOnlyList<UsfmVersificationError> errors = new ZipParatextProjectVersificationErrorDetector(
                    zipArchive,
                    corpusBundle.ParentOf(file.Location)?.Settings
                ).GetUsfmVersificationErrors(books: GetBooks(monolingualCorpus));
                if (errors.Count > 0)
                {
                    errorsPerCorpus.Add((parallelCorpus.Id, monolingualCorpus.Id, errors));
                }
            }
        }
        return errorsPerCorpus;
    }

    public QuoteConventionAnalysis AnalyzeTargetQuoteConvention(IEnumerable<ParallelCorpus> parallelCorpora)
    {
        CorpusBundle corpusBundle = new(parallelCorpora);
        Dictionary<string, List<QuoteConventionAnalysis>> analyses = [];
        foreach (
            (
                ParallelCorpus parallelCorpus,
                MonolingualCorpus targetMonolingualCorpus,
                IReadOnlyList<CorpusFile> corpusFiles,
                _
            ) in corpusBundle.TargetTextCorpora
        )
        {
            foreach (CorpusFile file in corpusFiles.Where(f => f.Format == FileFormat.Paratext))
            {
                using ZipArchive zipArchive = ZipFile.OpenRead(file.Location);
                var quoteConventionDetector = new ZipParatextProjectQuoteConventionDetector(
                    zipArchive,
                    corpusBundle.ParentOf(file.Location)?.Settings
                );
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
                if (!analyses.ContainsKey(parallelCorpus.Id))
                    analyses[parallelCorpus.Id] = [];
                if (chapters != null)
                    analyses[parallelCorpus.Id].Add(quoteConventionDetector.GetQuoteConventionAnalysis(chapters));
                else
                    analyses[parallelCorpus.Id].Add(quoteConventionDetector.GetQuoteConventionAnalysis());
            }
        }

        return QuoteConventionAnalysis.CombineWithWeightedAverage(
            analyses.Select(kvp => QuoteConventionAnalysis.CombineWithWeightedAverage(kvp.Value)).ToList()
        );
    }

    public IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        MissingParentProjectError
    )> FindMissingParentProjects(IEnumerable<ParallelCorpus> parallelCorpora)
    {
        CorpusBundle corpusBundle = new(parallelCorpora);
        List<(string, string, MissingParentProjectError)> errors = [];
        foreach (
            (
                ParallelCorpus parallelCorpus,
                MonolingualCorpus monolingualCorpus,
                IReadOnlyList<CorpusFile> files,
                _
            ) in corpusBundle.TextCorpora
        )
        {
            foreach (CorpusFile file in files.Where(f => f.Format == FileFormat.Paratext))
            {
                using ZipArchive archive = ZipFile.OpenRead(file.Location);
                ParatextProjectSettings settings = Machine.Corpora.ZipParatextProjectSettingsParser.Parse(archive);
                if (settings.HasParent && corpusBundle.ParentOf(file.Location) == null)
                {
                    errors.Add(
                        (
                            parallelCorpus.Id,
                            monolingualCorpus.Id,
                            new() { ProjectName = settings.Name, ParentProjectName = settings.ParentName }
                        )
                    );
                }
            }
        }

        return errors;
    }

    public async Task PreprocessAsync(
        IEnumerable<ParallelCorpus> parallelCorpora,
        Func<Row, TrainingDataType, Task> train,
        Func<Row, bool, string, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    )
    {
        await PreprocessAsync(new CorpusBundle(parallelCorpora), train, inference, useKeyTerms, ignoreUsfmMarkers);
    }

    public async Task PreprocessAsync(
        CorpusBundle corpusBundle,
        Func<Row, TrainingDataType, Task> train,
        Func<Row, bool, string, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    )
    {
        ignoreUsfmMarkers ??= [];

        bool parallelTrainingDataPresent = false;
        List<Row> keyTermTrainingData = new();

        // Create source and target arrays of text corpora filtered for training
        // based on the filters specified in the associated monolingual corpora
        ITextCorpus[] sourceTrainingCorpora = corpusBundle
            .SourceTextCorpora.SelectMany(c =>
                c.TextCorpora.Select(tc => FilterTrainingCorpora(c.MonolingualCorpus, tc))
            )
            .ToArray();

        ITextCorpus[] targetTrainingCorpora = corpusBundle
            .TargetTextCorpora.SelectMany(c =>
                c.TextCorpora.Select(tc => FilterTrainingCorpora(c.MonolingualCorpus, tc))
            )
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
            ITextCorpus[] sourceTermCorpora = corpusBundle.SourceTermCorpora.SelectMany(c => c.TextCorpora).ToArray();
            ITextCorpus[] targetTermCorpora = corpusBundle.TargetTermCorpora.SelectMany(c => c.TextCorpora).ToArray();

            // As with scripture data, interlace the source rows randomly
            // but choose the first non-empty target row, then align
            IParallelTextCorpus parallelKeyTermCorpus = sourceTermCorpora
                .ChooseRandom(Seed)
                .AlignRows(targetTermCorpora.ChooseFirst());

            // Only train on unique key terms pairs
            foreach (ParallelTextRow row in parallelKeyTermCorpus.DistinctBy(row => (row.SourceText, row.TargetText)))
            {
                keyTermTrainingData.Add(
                    new Row(row.TextId, row.SourceRefs, row.TargetRefs, row.SourceText, row.TargetText, 1)
                );
            }
        }

        // Since we ultimately need to provide inferences for a particular parallel corpus,
        // we need to preprocess the content on which to inference per parallel corpus
        foreach (ParallelCorpus parallelCorpus in corpusBundle.ParallelCorpora)
        {
            // Filter the text corpora based on the filters specified in the monolingual corpora
            ITextCorpus sourceInferencingCorpus = corpusBundle
                .SourceTextCorpora.Where(c => c.ParallelCorpus.Id == parallelCorpus.Id)
                .SelectMany(sc =>
                    sc.TextCorpora.Select(textCorpus =>
                        FilterInferencingCorpora(sc.MonolingualCorpus, textCorpus, ignoreUsfmMarkers)
                    )
                )
                .ChooseFirst();

            ITextCorpus targetInferencingCorpus = corpusBundle
                .TargetTextCorpora.Where(c => c.ParallelCorpus.Id == parallelCorpus.Id)
                .SelectMany(tc =>
                    tc.TextCorpora.Select(textCorpus =>
                        FilterInferencingCorpora(tc.MonolingualCorpus, textCorpus, ignoreUsfmMarkers)
                    )
                )
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
                await inference(row, isInTrainingData, parallelCorpus.Id);
            }
        }

        // Only train on key terms if there were other parallel scripture data.
        // This is necessary to support inference-only jobs since the terms are not
        // filtered by the filters specified in the monolingual corpora.
        if (useKeyTerms && parallelTrainingDataPresent)
        {
            foreach (Row row in keyTermTrainingData)
            {
                await train(row, TrainingDataType.KeyTerm);
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

    private static HashSet<int>? GetBooks(MonolingualCorpus corpus)
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

        if (corpus.InferenceTextIds != null)
        {
            books.AddRange(corpus.InferenceTextIds);
        }
        else if (corpus.InferenceChapters != null)
        {
            books.AddRange(corpus.InferenceChapters.Keys);
        }

        return [.. books.Select(bookName => Canon.BookIdToNumber(bookName))];
    }

    public string UpdateSourceUsfm(
        IReadOnlyList<ParallelCorpus> parallelCorpora,
        string corpusId,
        string bookId,
        IReadOnlyList<ParallelRow> rows,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        bool placeParagraphMarkers,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention
    )
    {
        return UpdateUsfm(
            parallelCorpora,
            corpusId,
            bookId,
            rows,
            UpdateUsfmTextBehavior.StripExisting,
            paragraphBehavior,
            embedBehavior,
            styleBehavior,
            placeParagraphMarkers ? [new PlaceMarkersUsfmUpdateBlockHandler()] : null,
            remarks,
            targetQuoteConvention,
            isSource: true
        );
    }

    public string UpdateTargetUsfm(
        IReadOnlyList<ParallelCorpus> parallelCorpora,
        string corpusId,
        string bookId,
        IReadOnlyList<ParallelRow> rows,
        UpdateUsfmTextBehavior textBehavior,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention
    )
    {
        return UpdateUsfm(
            parallelCorpora,
            corpusId,
            bookId,
            rows,
            textBehavior,
            paragraphBehavior,
            embedBehavior,
            styleBehavior,
            updateBlockHandlers: null,
            remarks,
            targetQuoteConvention,
            isSource: false
        );
    }

    private static string UpdateUsfm(
        IReadOnlyList<ParallelCorpus> parallelCorpora,
        string corpusId,
        string bookId,
        IEnumerable<ParallelRow> rows,
        UpdateUsfmTextBehavior textBehavior,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior embedBehavior,
        UpdateUsfmMarkerBehavior styleBehavior,
        IEnumerable<IUsfmUpdateBlockHandler>? updateBlockHandlers,
        IEnumerable<string>? remarks,
        string? targetQuoteConvention,
        bool isSource
    )
    {
        CorpusBundle corpusBundle = new(parallelCorpora);
        ParallelCorpus corpus = corpusBundle.ParallelCorpora.Single(c => c.Id == corpusId);
        CorpusFile sourceFile = corpus.SourceCorpora[0].Files[0];
        CorpusFile targetFile = corpus.TargetCorpora[0].Files[0];
        ParatextProjectSettings? sourceSettings = corpusBundle.GetSettings(sourceFile.Location);
        ParatextProjectSettings? targetSettings = corpusBundle.GetSettings(targetFile.Location);

        using ZipParatextProjectTextUpdater updater = corpusBundle.GetTextUpdater(
            isSource ? sourceFile.Location : targetFile.Location
        );
        string usfm =
            updater.UpdateUsfm(
                bookId,
                rows.Select(p =>
                        Map(
                            p,
                            isSource,
                            sourceSettings?.Versification,
                            targetSettings?.Versification,
                            paragraphBehavior,
                            styleBehavior
                        )
                    )
                    .Where(row => row.Refs.Any())
                    .OrderBy(row => row.Refs[0])
                    .ToArray(),
                isSource ? sourceSettings?.FullName : targetSettings?.FullName,
                textBehavior,
                paragraphBehavior,
                embedBehavior,
                styleBehavior,
                updateBlockHandlers: updateBlockHandlers,
                remarks: remarks,
                errorHandler: (_) => true,
                compareSegments: isSource
            ) ?? "";

        if (!string.IsNullOrEmpty(targetQuoteConvention))
            usfm = DenormalizeQuotationMarks(usfm, targetQuoteConvention);
        return usfm;
    }

    private static UpdateUsfmRow Map(
        ParallelRow row,
        bool isSource,
        ScrVers? sourceVersification,
        ScrVers? targetVersification,
        UpdateUsfmMarkerBehavior paragraphBehavior,
        UpdateUsfmMarkerBehavior styleBehavior
    )
    {
        Dictionary<string, object>? metadata = null;
        if (row.Alignment is not null)
        {
            metadata = new Dictionary<string, object>
            {
                {
                    PlaceMarkersAlignmentInfo.MetadataKey,
                    new PlaceMarkersAlignmentInfo(
                        row.SourceTokens,
                        row.TargetTokens,
                        CreateWordAlignmentMatrix(row),
                        paragraphBehavior,
                        styleBehavior
                    )
                },
            };
        }

        ScriptureRef[] refs;
        if (isSource)
        {
            refs = (
                row.SourceRefs.Any()
                    ? Map(row.SourceRefs, sourceVersification)
                    : Map(row.TargetRefs, targetVersification)
            ).ToArray();
        }
        else
        {
            // the pretranslations are generated from the source book and inserted into the target book
            // use relaxed references since the USFM structure may not be the same
            refs = Map(row.TargetRefs, targetVersification).Select(r => r.ToRelaxed()).ToArray();
        }

        return new UpdateUsfmRow(refs, row.TargetText, metadata);
    }

    private static IEnumerable<ScriptureRef> Map(IEnumerable<string> refs, ScrVers? versification)
    {
        return refs.Select(r =>
            {
                ScriptureRef.TryParse(r, versification, out ScriptureRef sr);
                return sr;
            })
            .Where(r => !r.IsEmpty);
    }

    private static WordAlignmentMatrix? CreateWordAlignmentMatrix(ParallelRow row)
    {
        if (row.Alignment is null || row.SourceTokens is null || row.TargetTokens is null)
        {
            return null;
        }

        var matrix = new WordAlignmentMatrix(row.SourceTokens.Count, row.TargetTokens.Count);
        foreach (AlignedWordPair wordPair in row.Alignment)
            matrix[wordPair.SourceIndex, wordPair.TargetIndex] = true;

        return matrix;
    }

    private static string DenormalizeQuotationMarks(string usfm, string quoteConvention)
    {
        QuoteConvention targetQuoteConvention = QuoteConventions.Standard.GetQuoteConventionByName(quoteConvention);
        if (targetQuoteConvention is null)
            return usfm;

        QuotationMarkDenormalizationFirstPass quotationMarkDenormalizationFirstPass = new(targetQuoteConvention);

        UsfmParser.Parse(usfm, quotationMarkDenormalizationFirstPass);
        List<(int ChapterNumber, QuotationMarkUpdateStrategy Strategy)> bestChapterStrategies =
            quotationMarkDenormalizationFirstPass.FindBestChapterStrategies();

        QuotationMarkDenormalizationUsfmUpdateBlockHandler quotationMarkDenormalizer = new(
            targetQuoteConvention,
            new QuotationMarkUpdateSettings(
                chapterStrategies: bestChapterStrategies.Select(tuple => tuple.Strategy).ToList()
            )
        );
        int denormalizableChapterCount = bestChapterStrategies.Count(tup =>
            tup.Strategy != QuotationMarkUpdateStrategy.Skip
        );
        List<string> remarks = [];
        string quotationDenormalizationRemark;
        if (denormalizableChapterCount == bestChapterStrategies.Count)
        {
            quotationDenormalizationRemark =
                "The quote style in all chapters has been automatically adjusted to match the rest of the project.";
        }
        else if (denormalizableChapterCount > 0)
        {
            quotationDenormalizationRemark =
                "The quote style in the following chapters has been automatically adjusted to match the rest of the project: "
                + GetChapterRangesString(
                    bestChapterStrategies
                        .Where(tuple => tuple.Strategy != QuotationMarkUpdateStrategy.Skip)
                        .Select(tuple => tuple.ChapterNumber)
                        .ToList()
                )
                + ".";
        }
        else
        {
            quotationDenormalizationRemark =
                "The quote style was not automatically adjusted to match the rest of your project in any chapters.";
        }
        remarks.Add(quotationDenormalizationRemark);

        var updater = new UpdateUsfmParserHandler(updateBlockHandlers: [quotationMarkDenormalizer], remarks: remarks);
        UsfmParser.Parse(usfm, updater);

        usfm = updater.GetUsfm();
        return usfm;
    }

    public static string GetChapterRangesString(List<int> chapterNumbers)
    {
        chapterNumbers = chapterNumbers.Order().ToList();
        int start = chapterNumbers[0];
        int end = chapterNumbers[0];
        List<string> chapterRangeStrings = [];
        foreach (int chapterNumber in chapterNumbers[1..])
        {
            if (chapterNumber == end + 1)
            {
                end = chapterNumber;
            }
            else
            {
                if (start == end)
                {
                    chapterRangeStrings.Add(start.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    chapterRangeStrings.Add($"{start}-{end}");
                }
                start = chapterNumber;
                end = chapterNumber;
            }
        }
        if (start == end)
        {
            chapterRangeStrings.Add(start.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            chapterRangeStrings.Add($"{start}-{end}");
        }
        return string.Join(", ", chapterRangeStrings);
    }

    public Dictionary<string, List<int>> GetChapters(
        IReadOnlyList<ParallelCorpus> parallelCorpora,
        string fileLocation,
        string scriptureRange
    )
    {
        CorpusBundle corpusBundle = new(parallelCorpora);
        try
        {
            return ScriptureRangeParser.GetChapters(
                scriptureRange,
                corpusBundle.GetSettings(fileLocation)?.Versification
            );
        }
        catch (ArgumentException ae)
        {
            throw new InvalidOperationException($"The scripture range {scriptureRange} is not valid: {ae.Message}");
        }
    }
}
