using SIL.Machine.PunctuationAnalysis;
using SIL.Scripture;

namespace Serval.Shared.Services;

public class ParallelCorpusService : IParallelCorpusService
{
    private const int Seed = 1234;

    public IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        string ProjectName,
        string ProjectGuid,
        string VersificationName,
        IReadOnlyList<UsfmVersificationDiagnosticContract> Diagnostics
    )> AnalyzeUsfmVersification(IEnumerable<ParallelCorpusContract> parallelCorpora)
    {
        CorpusBundle corpusBundle = new(parallelCorpora);
        List<(
            string ParallelCorpusId,
            string MonolingualCorpusId,
            string ProjectName,
            string ProjectGuid,
            string VersificationName,
            IReadOnlyList<UsfmVersificationDiagnosticContract> Diagnostics
        )> diagnosticsPerCorpus = [];
        foreach (
            (
                ParallelCorpusContract parallelCorpus,
                MonolingualCorpusContract monolingualCorpus,
                IReadOnlyList<CorpusFileContract> files,
                _
            ) in corpusBundle.TextCorpora
        )
        {
            foreach (CorpusFileContract file in files.Where(f => f.Format == FileFormat.Paratext))
            {
                using ZipArchive zipArchive = ZipFile.OpenRead(file.Location);
                UsfmVersificationAnalysis analysis = new ZipUsfmVersificationAnalyzer(
                    zipArchive,
                    corpusBundle.ParentOf(file.Location)?.Settings
                ).AnalyzeUsfmVersification(bookIdsAndChapters: GetBookIdsAndChapters(monolingualCorpus));
                diagnosticsPerCorpus.Add(
                    (
                        parallelCorpus.Id,
                        monolingualCorpus.Id,
                        analysis.ProjectSettings.Name,
                        analysis.ProjectSettings.Guid,
                        analysis.ProjectSettings.Versification.Name,
                        [
                            .. analysis.Diagnostics.Select(d => new UsfmVersificationDiagnosticContract
                            {
                                Type = Map(d.Type, d.References.FirstOrDefault()),
                                NumAffectedVerses = d.NumAffectedVerses,
                                References = [.. d.References.Select(r => r.ToString())],
                                Filename = d.Filename,
                                LineNumbers = [.. d.LineNumbers],
                            }),
                        ]
                    )
                );
            }
        }
        return diagnosticsPerCorpus;
    }

    private static Contracts.UsfmVersificationDiagnosticType Map(
        SIL.Machine.Corpora.UsfmVersificationDiagnosticType type,
        VerseRef verseRef
    )
    {
        return type switch
        {
            SIL.Machine.Corpora.UsfmVersificationDiagnosticType.Missing => Contracts
                .UsfmVersificationDiagnosticType
                .Missing,
            SIL.Machine.Corpora.UsfmVersificationDiagnosticType.Extra => Contracts
                .UsfmVersificationDiagnosticType
                .Extra,
            SIL.Machine.Corpora.UsfmVersificationDiagnosticType.Invalid when verseRef.ChapterNum == -1 => Contracts
                .UsfmVersificationDiagnosticType
                .InvalidChapter,
            SIL.Machine.Corpora.UsfmVersificationDiagnosticType.Invalid => Contracts
                .UsfmVersificationDiagnosticType
                .InvalidVerse,
            SIL.Machine.Corpora.UsfmVersificationDiagnosticType.IncorrectVerseSegment => Contracts
                .UsfmVersificationDiagnosticType
                .IncorrectVerseSegment,
            SIL.Machine.Corpora.UsfmVersificationDiagnosticType.UnsupportedVerseRange => Contracts
                .UsfmVersificationDiagnosticType
                .UnsupportedVerseRange,
            _ => throw new InvalidOperationException($"Unknown USFM versification error type: {type}"),
        };
    }

    public string AnalyzeTargetQuoteConvention(IEnumerable<ParallelCorpusContract> parallelCorpora)
    {
        CorpusBundle corpusBundle = new(parallelCorpora);
        Dictionary<string, List<QuoteConventionAnalysis>> analyses = [];
        foreach (
            (
                ParallelCorpusContract parallelCorpus,
                MonolingualCorpusContract targetMonolingualCorpus,
                IReadOnlyList<CorpusFileContract> corpusFiles,
                _
            ) in corpusBundle.TargetTextCorpora
        )
        {
            foreach (CorpusFileContract file in corpusFiles.Where(f => f.Format == FileFormat.Paratext))
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

        var analysis = QuoteConventionAnalysis.CombineWithWeightedAverage([
            .. analyses.Select(kvp => QuoteConventionAnalysis.CombineWithWeightedAverage(kvp.Value)),
        ]);
        return analysis?.BestQuoteConvention?.Name ?? string.Empty;
    }

    public IReadOnlyList<(
        string ParallelCorpusId,
        string MonolingualCorpusId,
        MissingParentProjectErrorContract Error
    )> FindMissingParentProjects(IEnumerable<ParallelCorpusContract> parallelCorpora)
    {
        CorpusBundle corpusBundle = new(parallelCorpora);
        List<(string, string, MissingParentProjectErrorContract)> errors = [];
        foreach (
            (
                ParallelCorpusContract parallelCorpus,
                MonolingualCorpusContract monolingualCorpus,
                IReadOnlyList<CorpusFileContract> files,
                _
            ) in corpusBundle.TextCorpora
        )
        {
            foreach (CorpusFileContract file in files)
            {
                ParatextProjectSettings? settings = corpusBundle.GetSettings(file.Location);
                if (settings != null && settings.HasParent && settings.Parent == null)
                {
                    errors.Add(
                        (
                            parallelCorpus.Id,
                            monolingualCorpus.Id,
                            new()
                            {
                                ProjectName = settings.Name,
                                ProjectGuid = settings.Guid,
                                ParentProjectName = settings.ParentName,
                                ParentProjectGuid = settings.ParentGuid,
                            }
                        )
                    );
                }
            }
        }

        return errors;
    }

    public async Task PreprocessAsync(
        IEnumerable<ParallelCorpusContract> parallelCorpora,
        Func<ParallelRowContract, TrainingDataType, Task> train,
        Func<ParallelRowContract, bool, string, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    )
    {
        await PreprocessAsync(new CorpusBundle(parallelCorpora), train, inference, useKeyTerms, ignoreUsfmMarkers);
    }

    private static async Task PreprocessAsync(
        CorpusBundle corpusBundle,
        Func<ParallelRowContract, TrainingDataType, Task> train,
        Func<ParallelRowContract, bool, string, Task> inference,
        bool useKeyTerms = false,
        HashSet<string>? ignoreUsfmMarkers = null
    )
    {
        ignoreUsfmMarkers ??= [];

        bool parallelTrainingDataPresent = false;

        ScrVers referenceVersification =
            corpusBundle
                .TargetTextCorpora.SelectMany(c => c.TextCorpora.Select(tc => tc.Versification))
                .FirstOrDefault()
            ?? ScrVers.English;

        // Iterate over USFM and Text training corpora separately.
        // This is not only because they use different keys, but if we have text corpora
        // with scripture corpora, we don't want to exclude the text corpora from training.
        foreach (bool isScripture in new bool[] { true, false })
        {
            // Create source and target arrays of text corpora filtered for training
            // based on the filters specified in the associated monolingual corpora
            ITextCorpus[] sourceTrainingCorpora =
            [
                .. corpusBundle.SourceTextCorpora.SelectMany(c =>
                    c.TextCorpora.Where(tc => isScripture ? tc.IsScripture() : !tc.IsScripture())
                        .Select(tc => FilterTrainingCorpora(c.MonolingualCorpus, tc, referenceVersification))
                ),
            ];

            ITextCorpus[] targetTrainingCorpora =
            [
                .. corpusBundle.TargetTextCorpora.SelectMany(c =>
                    c.TextCorpora.Where(tc => isScripture ? tc.IsScripture() : !tc.IsScripture())
                        .Select(tc => FilterTrainingCorpora(c.MonolingualCorpus, tc, referenceVersification))
                ),
            ];

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
            IEnumerable<ParallelTextRow> trainingRows = sourceTrainingCorpus.AlignRows(
                targetTrainingCorpus,
                allSourceRows: true,
                allTargetRows: true
            );

            // After merging segments across ranges, run the 'train' preprocessing function
            // on each training row and record whether any parallel training data was present
            foreach (ParallelRowContract row in CollapseRanges(trainingRows))
            {
                await train(row, TrainingDataType.Text);
                if (!parallelTrainingDataPresent && row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                {
                    parallelTrainingDataPresent = true;
                }
            }
        }

        // Get the target corpus from the training corpora.
        // This is across Scripture and non-Scripture corpora
        ITextCorpus[] targetCorpora =
        [
            .. corpusBundle.TargetTextCorpora.SelectMany(c =>
                c.TextCorpora.Select(tc => FilterTrainingCorpora(c.MonolingualCorpus, tc, referenceVersification))
            ),
        ];
        ITextCorpus targetCorpus = targetCorpora.ChooseFirst();
        if (targetCorpus.IsScripture())
        {
            // Filter out all non-scripture; we only train on scripture content
            targetCorpus = targetCorpus.Where(IsScriptureRow);
        }

        // Since we ultimately need to provide inferences for a particular parallel corpus,
        // we need to preprocess the content on which to inference per parallel corpus
        foreach (ParallelCorpusContract parallelCorpus in corpusBundle.ParallelCorpora)
        {
            // Filter the text corpora based on the filters specified in the monolingual corpora
            ITextCorpus sourceInferencingCorpus = corpusBundle
                .SourceTextCorpora.Where(c => c.ParallelCorpus.Id == parallelCorpus.Id)
                .SelectMany(sc =>
                    sc.TextCorpora.Select(textCorpus =>
                        FilterInferencingCorpora(
                            sc.MonolingualCorpus,
                            textCorpus,
                            ignoreUsfmMarkers,
                            referenceVersification
                        )
                    )
                )
                .ChooseFirst();

            ITextCorpus targetInferencingCorpus = corpusBundle
                .TargetTextCorpora.Where(c => c.ParallelCorpus.Id == parallelCorpus.Id)
                .SelectMany(tc =>
                    tc.TextCorpora.Select(textCorpus =>
                        FilterInferencingCorpora(
                            tc.MonolingualCorpus,
                            textCorpus,
                            ignoreUsfmMarkers,
                            referenceVersification
                        )
                    )
                )
                .ChooseFirst();

            // We need to align all three of these corpora because we need both the source and target
            // content for inferencing (the target is only needed in some contexts like word alignment)
            // as well as the target training corpus in order to determine whether a row was already
            // used in training.
            IEnumerable<NParallelTextRow> inferencingCorpus = new ITextCorpus[]
            {
                sourceInferencingCorpus,
                targetInferencingCorpus,
                targetCorpus,
            }.AlignMany([true, false, false]);

            foreach ((ParallelRowContract row, bool isInTrainingData) in CollapseInferencingRanges(inferencingCorpus))
            {
                await inference(row, isInTrainingData, parallelCorpus.Id);
            }
        }

        // Only train on key terms if there were other parallel scripture data.
        // This is necessary to support inference-only jobs since the terms are not
        // filtered by the filters specified in the monolingual corpora.
        if (useKeyTerms && parallelTrainingDataPresent)
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
                await train(
                    new ParallelRowContract(
                        row.TextId,
                        row.SourceRefs,
                        row.TargetRefs,
                        row.SourceText,
                        row.TargetText,
                        1
                    ),
                    TrainingDataType.KeyTerm
                );
            }
        }
    }

    private static ITextCorpus FilterInferencingCorpora(
        MonolingualCorpusContract corpus,
        ITextCorpus textCorpus,
        HashSet<string> ignoreUsfmMarkers,
        ScrVers referenceVersification
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
                .Where(row =>
                    row.Ref is not ScriptureRef sr || IsInChapters(sr, corpus.InferenceChapters, referenceVersification)
                );
        }
        return textCorpus.Where(row => row.Ref is not ScriptureRef sr || !HasIgnorableMarker(sr, ignoreUsfmMarkers));
    }

    private static ITextCorpus FilterTrainingCorpora(
        MonolingualCorpusContract corpus,
        ITextCorpus textCorpus,
        ScrVers referenceVersification
    )
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
                .Where(row =>
                    row.Ref is not ScriptureRef sr || IsInChapters(sr, corpus.TrainOnChapters, referenceVersification)
                );
        }
        return textCorpus;
    }

    private static IEnumerable<ParallelRowContract> CollapseRanges(IEnumerable<ParallelTextRow> rows)
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
                yield return new ParallelRowContract(
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

            yield return new ParallelRowContract(
                textId,
                sourceRefs,
                targetRefs,
                srcSegBuffer.ToString(),
                trgSegBuffer.ToString(),
                1
            );

            srcSegBuffer.Clear();
            trgSegBuffer.Clear();
            sourceRefs.Clear();
            targetRefs.Clear();
        }
        if (hasUnfinishedRange)
        {
            yield return new ParallelRowContract(
                textId,
                sourceRefs,
                targetRefs,
                srcSegBuffer.ToString(),
                trgSegBuffer.ToString(),
                1
            );
        }
    }

    private static IEnumerable<(ParallelRowContract, bool)> CollapseInferencingRanges(
        IEnumerable<NParallelTextRow> rows
    )
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
                    new ParallelRowContract(
                        textId,
                        sourceRefs,
                        targetRefs,
                        srcSegBuffer.ToString(),
                        trgSegBuffer.ToString(),
                        1
                    ),
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
                new ParallelRowContract(
                    textId,
                    sourceRefs,
                    targetRefs,
                    srcSegBuffer.ToString(),
                    trgSegBuffer.ToString(),
                    1
                ),
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
                new ParallelRowContract(
                    textId,
                    sourceRefs,
                    targetRefs,
                    srcSegBuffer.ToString(),
                    trgSegBuffer.ToString(),
                    1
                ),
                isInTrainingData
            );
        }
    }

    private static bool IsScriptureRow(TextRow parallelTextRow)
    {
        return parallelTextRow.Ref is ScriptureRef sr && sr.IsVerse;
    }

    private static bool IsInChapters(
        ScriptureRef sr,
        Dictionary<string, HashSet<int>> selection,
        ScrVers referenceVersification
    )
    {
        return selection.TryGetValue(sr.Book, out HashSet<int>? chapters)
            && chapters != null
            && (chapters.Count == 0 || chapters.Contains(sr.ChangeVersification(referenceVersification).ChapterNum));
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

    internal static Dictionary<string, HashSet<int>>? GetBookIdsAndChapters(MonolingualCorpusContract corpus)
    {
        if (!corpus.IsFiltered)
            return null;

        Dictionary<string, HashSet<int>> bookIdsAndChapters = [];
        if (corpus.TrainOnTextIds is not null)
        {
            foreach (string textId in corpus.TrainOnTextIds)
                bookIdsAndChapters.TryAdd(textId, []);
        }
        else if (corpus.TrainOnChapters != null)
        {
            foreach ((string textId, HashSet<int> chapters) in corpus.TrainOnChapters)
                bookIdsAndChapters.TryAdd(textId, [.. chapters]);
        }

        if (corpus.InferenceTextIds is not null)
        {
            foreach (string textId in corpus.InferenceTextIds)
            {
                if (!bookIdsAndChapters.TryAdd(textId, []))
                    bookIdsAndChapters[textId] = [];
            }
        }
        else if (corpus.InferenceChapters != null)
        {
            foreach ((string textId, HashSet<int> chapters) in corpus.InferenceChapters)
            {
                if (!bookIdsAndChapters.TryAdd(textId, [.. chapters]))
                {
                    if (chapters.Count == 0)
                    {
                        // Clear all chapters, as we are to inference on all chapters
                        bookIdsAndChapters[textId] = [];
                    }
                    else if (bookIdsAndChapters[textId].Count > 0)
                    {
                        // Only specify these chapters if chapters have already been specified
                        foreach (int chapter in chapters)
                            bookIdsAndChapters[textId].Add(chapter);
                    }
                }
            }
        }

        return bookIdsAndChapters;
    }

    public Dictionary<string, List<int>> GetChapters(
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
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
