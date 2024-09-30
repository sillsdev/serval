namespace Serval.Machine.Shared.Services;

public class PreprocessBuildJob : HangfireBuildJob<IReadOnlyList<Corpus>>
{
    private static readonly JsonWriterOptions PretranslateWriterOptions = new() { Indented = true };

    internal BuildJobRunnerType TrainJobRunnerType { get; init; } = BuildJobRunnerType.ClearML;

    private readonly ISharedFileService _sharedFileService;
    private readonly ICorpusService _corpusService;
    private int _seed = 1234;
    private Random _random;

    public PreprocessBuildJob(
        IPlatformService platformService,
        IRepository<TranslationEngine> engines,
        IDataAccessContext dataAccessContext,
        ILogger<PreprocessBuildJob> logger,
        IBuildJobService buildJobService,
        ISharedFileService sharedFileService,
        ICorpusService corpusService
    )
        : base(platformService, engines, dataAccessContext, buildJobService, logger)
    {
        _sharedFileService = sharedFileService;
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

    protected override async Task DoWorkAsync(
        string engineId,
        string buildId,
        IReadOnlyList<Corpus> data,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        TranslationEngine? engine = await Engines.GetAsync(e => e.EngineId == engineId, cancellationToken);
        if (engine is null)
            throw new OperationCanceledException($"Engine {engineId} does not exist.  Build canceled.");

        bool sourceTagInBaseModel = ResolveLanguageCodeForBaseModel(engine.SourceLanguage, out string srcLang);
        bool targetTagInBaseModel = ResolveLanguageCodeForBaseModel(engine.TargetLanguage, out string trgLang);

        (int trainCount, int pretranslateCount) = await WriteDataFilesAsync(
            buildId,
            data,
            buildOptions,
            cancellationToken
        );
        // Log summary of build data
        JsonObject buildPreprocessSummary =
            new()
            {
                { "Event", "BuildPreprocess" },
                { "EngineId", engineId },
                { "BuildId", buildId },
                { "NumTrainRows", trainCount },
                { "NumPretranslateRows", pretranslateCount },
                { "SourceLanguageResolved", srcLang },
                { "TargetLanguageResolved", trgLang }
            };
        Logger.LogInformation("{summary}", buildPreprocessSummary.ToJsonString());

        if (trainCount == 0 && (!sourceTagInBaseModel || !targetTagInBaseModel))
        {
            throw new InvalidOperationException(
                $"At least one language code in build {buildId} is unknown to the base model, and the data specified for training was empty. Build canceled."
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

        bool canceling = !await BuildJobService.StartBuildJobAsync(
            TrainJobRunnerType,
            engine.Type,
            engineId,
            buildId,
            BuildStage.Train,
            buildOptions: buildOptions,
            cancellationToken: cancellationToken
        );
        if (canceling)
            throw new OperationCanceledException();
    }

    private async Task<(int TrainCount, int PretranslateCount)> WriteDataFilesAsync(
        string buildId,
        IReadOnlyList<Corpus> corpora,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        JsonObject? buildOptionsObject = null;
        if (buildOptions is not null)
            buildOptionsObject = JsonSerializer.Deserialize<JsonObject>(buildOptions);
        await using StreamWriter sourceTrainWriter =
            new(await _sharedFileService.OpenWriteAsync($"builds/{buildId}/train.src.txt", cancellationToken));
        await using StreamWriter targetTrainWriter =
            new(await _sharedFileService.OpenWriteAsync($"builds/{buildId}/train.trg.txt", cancellationToken));

        await using Stream pretranslateStream = await _sharedFileService.OpenWriteAsync(
            $"builds/{buildId}/pretranslate.src.json",
            cancellationToken
        );
        await using Utf8JsonWriter pretranslateWriter = new(pretranslateStream, PretranslateWriterOptions);

        int trainCount = 0;
        int pretranslateCount = 0;
        pretranslateWriter.WriteStartArray();
        foreach (Corpus corpus in corpora)
        {
            ITextCorpus[] sourceTextCorpora = _corpusService.CreateTextCorpora(corpus.SourceFiles).ToArray();
            ITextCorpus targetTextCorpus =
                _corpusService.CreateTextCorpora(corpus.TargetFiles).FirstOrDefault() ?? new DictionaryTextCorpus();

            if (sourceTextCorpora.Length == 0)
                continue;

            int skipCount = 0;
            foreach (Row?[] rows in AlignTrainCorpus(corpus, sourceTextCorpora, targetTextCorpus))
            {
                if (skipCount > 0)
                {
                    skipCount--;
                    continue;
                }

                Row[] trainRows = rows.Where(r => r is not null && IsInTrain(r, corpus)).Cast<Row>().ToArray();
                if (trainRows.Length > 0)
                {
                    Row row = trainRows[0];
                    if (rows.Length > 1)
                    {
                        Row[] nonEmptyRows = trainRows.Where(r => r.SourceSegment.Length > 0).ToArray();
                        if (nonEmptyRows.Length > 0)
                            row = nonEmptyRows[_random.Next(nonEmptyRows.Length)];
                    }

                    await sourceTrainWriter.WriteAsync($"{row.SourceSegment}\n");
                    await targetTrainWriter.WriteAsync($"{row.TargetSegment}\n");
                    skipCount = row.RowCount - 1;
                    if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                        trainCount++;
                }
            }

            if ((bool?)buildOptionsObject?["use_key_terms"] ?? true)
            {
                ITextCorpus? sourceTermCorpus = _corpusService.CreateTermCorpora(corpus.SourceFiles).FirstOrDefault();
                ITextCorpus? targetTermCorpus = _corpusService.CreateTermCorpora(corpus.TargetFiles).FirstOrDefault();
                if (sourceTermCorpus is not null && targetTermCorpus is not null)
                {
                    IParallelTextCorpus parallelKeyTermsCorpus = sourceTermCorpus.AlignRows(targetTermCorpus);
                    foreach (ParallelTextRow row in parallelKeyTermsCorpus)
                    {
                        await sourceTrainWriter.WriteAsync($"{row.SourceText}\n");
                        await targetTrainWriter.WriteAsync($"{row.TargetText}\n");
                        trainCount++;
                    }
                }
            }

            foreach (Row row in AlignPretranslateCorpus(corpus, sourceTextCorpora[0], targetTextCorpus))
            {
                if (
                    IsInPretranslate(row, corpus)
                    && row.SourceSegment.Length > 0
                    && (row.TargetSegment.Length == 0 || !IsInTrain(row, corpus))
                )
                {
                    pretranslateWriter.WriteStartObject();
                    pretranslateWriter.WriteString("corpusId", corpus.Id);
                    pretranslateWriter.WriteString("textId", row.TextId);
                    pretranslateWriter.WriteStartArray("refs");
                    foreach (object rowRef in row.Refs)
                        pretranslateWriter.WriteStringValue(rowRef.ToString());
                    pretranslateWriter.WriteEndArray();
                    pretranslateWriter.WriteString("translation", row.SourceSegment);
                    pretranslateWriter.WriteEndObject();
                    pretranslateCount++;
                }
            }
        }

        pretranslateWriter.WriteEndArray();

        return (trainCount, pretranslateCount);
    }

    public record ParallelCorpusSubcorpus
    {
        public required string Id { get; set; }
        public required string Language { get; set; }
        public required IReadOnlyList<CorpusFile> Files { get; set; }
        public HashSet<string>? TrainOnTextIds { get; set; }
        public Dictionary<string, HashSet<int>>? TrainOnChapters { get; set; }
        public HashSet<string>? PretranslateTextIds { get; set; }
        public Dictionary<string, HashSet<int>>? PretranslateChapters { get; set; }
    }

    public record ParallelCorpus
    {
        public required string Id { get; set; }
        public IReadOnlyList<ParallelCorpusSubcorpus> SourceCorpora { get; set; } = new List<ParallelCorpusSubcorpus>();
        public IReadOnlyList<ParallelCorpusSubcorpus> TargetCorpora { get; set; } = new List<ParallelCorpusSubcorpus>();
    }

    public async Task<(int TrainCount, int PretranslateCount)> WriteDataFilesAsync(
        string buildId,
        IReadOnlyList<ParallelCorpus> corpora,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        JsonObject? buildOptionsObject = null;
        if (buildOptions is not null)
            buildOptionsObject = JsonSerializer.Deserialize<JsonObject>(buildOptions);
        await using StreamWriter sourceTrainWriter =
            new(await _sharedFileService.OpenWriteAsync($"builds/{buildId}/train.src.txt", cancellationToken));
        await using StreamWriter targetTrainWriter =
            new(await _sharedFileService.OpenWriteAsync($"builds/{buildId}/train.trg.txt", cancellationToken));

        await using Stream pretranslateStream = await _sharedFileService.OpenWriteAsync(
            $"builds/{buildId}/pretranslate.src.json",
            cancellationToken
        );
        await using Utf8JsonWriter pretranslateWriter = new(pretranslateStream, PretranslateWriterOptions);

        int trainCount = 0;
        int pretranslateCount = 0;
        pretranslateWriter.WriteStartArray();
        foreach (ParallelCorpus corpus in corpora)
        {
            (ParallelCorpusSubcorpus Subcorpus, ITextCorpus TextCorpus)[] sourceCorpora = corpus
                .SourceCorpora.SelectMany(c => _corpusService.CreateTextCorpora(c.Files).Select(tc => (c, tc)))
                .ToArray();
            ITextCorpus[] sourceTrainingCorpora = sourceCorpora
                .Select(sc =>
                    (
                        sc.Subcorpus,
                        sc.TextCorpus.FilterTexts(
                            (sc.Subcorpus.TrainOnTextIds ?? new()).Union((sc.Subcorpus.TrainOnChapters ?? new()).Keys)
                        )
                    )
                )
                .Select(sc =>
                    sc.Item2.FilterTextRows(row =>
                        row.Ref is not ScriptureRef sr
                        || (
                            (sc.Subcorpus.TrainOnChapters ?? new()).TryGetValue(sr.Book, out HashSet<int>? chapters)
                            && chapters != null
                            && (chapters.Count == 0 || chapters.Contains(sr.ChapterNum))
                        )
                    )
                )
                .ToArray();
            ITextCorpus[] sourcePretranslateCorpora = sourceCorpora
                .Select(sc =>
                    (
                        sc.Subcorpus,
                        sc.TextCorpus.FilterTexts(
                            (sc.Subcorpus.PretranslateTextIds ?? new())
                                .Union((sc.Subcorpus.PretranslateChapters ?? new()).Keys)
                                .Except(
                                    (sc.Subcorpus.TrainOnTextIds ?? new()).Union(
                                        (sc.Subcorpus.TrainOnChapters ?? new()).Keys
                                    )
                                )
                        )
                    )
                )
                .Select(sc =>
                    sc.Item2.FilterTextRows(row =>
                        row.Ref is not ScriptureRef sr
                        || (
                            (sc.Subcorpus.PretranslateChapters ?? new()).TryGetValue(
                                sr.Book,
                                out HashSet<int>? chapters
                            )
                            && chapters != null
                            && (chapters.Count == 0 || chapters.Contains(sr.ChapterNum))
                        )
                            && !(
                                (
                                    (sc.Subcorpus.TrainOnChapters ?? new()).TryGetValue(
                                        sr.Book,
                                        out HashSet<int>? trainChapters
                                    )
                                    && trainChapters != null
                                    && (trainChapters.Count == 0 || trainChapters.Contains(sr.ChapterNum))
                                )
                            )
                    )
                )
                .ToArray();

            (ParallelCorpusSubcorpus Subcorpus, ITextCorpus TextCorpus)[] targetCorpora = corpus
                .TargetCorpora.SelectMany(c => _corpusService.CreateTextCorpora(c.Files).Select(tc => (c, tc)))
                .ToArray();
            ITextCorpus[] targetTrainingCorpora = targetCorpora
                .Select(tc =>
                    (
                        tc.Subcorpus,
                        tc.TextCorpus.FilterTexts(
                            (tc.Subcorpus.TrainOnTextIds ?? new()).Union((tc.Subcorpus.TrainOnChapters ?? new()).Keys)
                        )
                    )
                )
                .Select(tc =>
                    tc.Item2.FilterTextRows(row =>
                        row.Ref is not ScriptureRef sr
                        || (
                            (tc.Subcorpus.TrainOnChapters ?? new()).TryGetValue(sr.Book, out HashSet<int>? chapters)
                            && chapters != null
                            && (chapters.Count == 0 || chapters.Contains(sr.ChapterNum))
                        )
                    )
                )
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
                            row = nonEmptyRows[_random.Next(nonEmptyRows.Length)];
                        }
                    }

                    await sourceTrainWriter.WriteAsync($"{row.SourceSegment}\n");
                    await targetTrainWriter.WriteAsync($"{row.TargetSegment}\n");
                    skipCount = row.RowCount - 1;
                    if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                        trainCount++;
                }
            }

            if ((bool?)buildOptionsObject?["use_key_terms"] ?? true)
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
                        await sourceTrainWriter.WriteAsync($"{row.SourceText}\n");
                        await targetTrainWriter.WriteAsync($"{row.TargetText}\n");
                        trainCount++;
                    }
                }
            }

            foreach (Row row in AlignPretranslateCorpus(sourcePretranslateCorpora, targetCorpora[0].TextCorpus))
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length == 0)
                {
                    pretranslateWriter.WriteStartObject();
                    pretranslateWriter.WriteString("corpusId", corpus.Id);
                    pretranslateWriter.WriteString("textId", row.TextId);
                    pretranslateWriter.WriteStartArray("refs");
                    foreach (object rowRef in row.Refs)
                        pretranslateWriter.WriteStringValue(rowRef.ToString());
                    pretranslateWriter.WriteEndArray();
                    pretranslateWriter.WriteString("translation", row.SourceSegment);
                    pretranslateWriter.WriteEndObject();
                    pretranslateCount++;
                }
            }
        }

        pretranslateWriter.WriteEndArray();

        return (trainCount, pretranslateCount);
    }

    protected override async Task CleanupAsync(
        string engineId,
        string buildId,
        IReadOnlyList<Corpus> data,
        JobCompletionStatus completionStatus
    )
    {
        if (completionStatus is JobCompletionStatus.Canceled)
        {
            try
            {
                await _sharedFileService.DeleteAsync($"builds/{buildId}/");
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, "Unable to to delete job data for build {BuildId}.", buildId);
            }
        }
    }

    private static bool IsInTrain(Row row, Corpus corpus)
    {
        return IsIncluded(row, corpus.TrainOnTextIds, corpus.TrainOnChapters);
    }

    // private static bool IsInTrain(Row row, ParallelCorpus corpus)
    // {
    //     return corpus.SourceCorpora.Any(sc => IsIncluded(row, sc.TrainOnTextIds, sc.TrainOnChapters))
    //         && corpus.TargetCorpora.Any(tc => IsIncluded(row, tc.TrainOnTextIds, tc.TrainOnChapters));
    // }

    private static bool IsInPretranslate(Row row, Corpus corpus)
    {
        return IsIncluded(row, corpus.PretranslateTextIds, corpus.PretranslateChapters);
    }

    // private static bool IsInPretranslate(Row row, ParallelCorpus corpus)
    // {
    //     return corpus.SourceCorpora.Any(sc => IsIncluded(row, sc.PretranslateTextIds, sc.PretranslateChapters));
    // }

    private static bool IsIncluded(
        Row? row,
        IReadOnlySet<string>? textIds,
        IReadOnlyDictionary<string, HashSet<int>>? chapters
    )
    {
        if (row is null)
            return false;
        if (chapters is not null)
            return row.Refs.Any(r => IsInChapters(chapters, r));
        if (textIds is not null)
            return textIds.Contains(row.TextId);
        return true;
    }

    private static bool IsInChapters(IReadOnlyDictionary<string, HashSet<int>> bookChapters, object rowRef)
    {
        if (rowRef is not ScriptureRef sr)
            return false;
        return bookChapters.TryGetValue(sr.Book, out HashSet<int>? chapters)
            && (chapters.Contains(sr.ChapterNum) || chapters.Count == 0);
    }

    private static IEnumerable<Row?[]> AlignTrainCorpus(
        Corpus corpus,
        IReadOnlyList<ITextCorpus> srcCorpora,
        ITextCorpus trgCorpus
    )
    {
        IEnumerable<string>? textIds = corpus.TrainOnChapters is not null
            ? corpus.TrainOnChapters.Keys
            : corpus.TrainOnTextIds;
        srcCorpora = srcCorpora.Select(sc => sc.FilterTexts(textIds).Transform(CleanSegment)).ToArray();
        trgCorpus = trgCorpus.FilterTexts(textIds).Transform(CleanSegment);

        if (trgCorpus.IsScripture())
        {
            return srcCorpora
                .Select(sc => AlignScripture(sc, trgCorpus))
                .ZipMany(rows => rows.ToArray())
                // filter out every list that only contains completely empty rows
                .Where(rows => rows.Any(r => r is null || r.SourceSegment.Length > 0 || r.TargetSegment.Length > 0));
        }

        IEnumerable<Row[]> sourceOnlyRows = srcCorpora
            .Select(sc => sc.AlignRows(trgCorpus, allSourceRows: true))
            .ZipMany(rows =>
                rows.Where(r => r.TargetSegment.Count == 0)
                    .Select(r => new Row(r.TextId, r.Refs, r.SourceText, r.TargetText, 1))
                    .ToArray()
            );

        IEnumerable<Row[]> targetRows = srcCorpora
            .Select(sc => sc.AlignRows(trgCorpus, allTargetRows: true))
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

    private static IEnumerable<Row> AlignPretranslateCorpus(Corpus corpus, ITextCorpus srcCorpus, ITextCorpus trgCorpus)
    {
        IEnumerable<string>? textIds = corpus.PretranslateChapters is not null
            ? corpus.PretranslateChapters.Keys
            : corpus.PretranslateTextIds;
        srcCorpus = srcCorpus.FilterTexts(textIds).Transform(CleanSegment);
        trgCorpus = trgCorpus.FilterTexts(textIds).Transform(CleanSegment);
        int rowCount = 0;
        StringBuilder srcSegBuffer = new();
        StringBuilder trgSegBuffer = new();
        List<object> refs = [];
        string textId = "";
        foreach (ParallelTextRow row in srcCorpus.AlignRows(trgCorpus, allSourceRows: true))
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

    private record Row(
        string TextId,
        IReadOnlyList<object> Refs,
        string SourceSegment,
        string TargetSegment,
        int RowCount
    );

    protected virtual bool ResolveLanguageCodeForBaseModel(string languageCode, out string resolvedCode)
    {
        resolvedCode = languageCode;
        return true;
    }

    private static TextRow CleanSegment(TextRow row)
    {
        if (row.Text == "...")
            row.Segment = [];
        return row;
    }
}
