using System.Globalization;

namespace Serval.Shared.Services;

public class BuildDiagnosticService : IBuildDiagnosticService
{
    private record DiagnosticInfo
    {
        public required string MessageFormat { get; init; }
        public Dictionary<string, Func<object, string?>> DataFormatters { get; init; } = [];
        public required string Category { get; init; }
        public required Contracts.DiagnosticSeverity Severity { get; init; }
        public required Dictionary<string, Type> DataTypes { get; init; }

        public string FormatMessage(Dictionary<string, object> data)
        {
            string?[] formattedParameters = DataTypes
                .Select(kvp => DataFormatters.GetValueOrDefault(kvp.Key, obj => obj.ToString())(data[kvp.Key]))
                .ToArray();

            return string.Format(CultureInfo.InvariantCulture, MessageFormat, formattedParameters);
        }
    }

    private static readonly Dictionary<string, DiagnosticInfo> Diagnostics = new()
    {
        ["MODEL-0001"] = new DiagnosticInfo
        {
            MessageFormat = "The script for the source language ‘{0}’ is not known to the base model {1}.",
            Category = "MODEL",
            Severity = Contracts.DiagnosticSeverity.Warn,
            DataTypes = new Dictionary<string, Type>
            {
                ["resolvedCode"] = typeof(string),
                ["modelName"] = typeof(string),
            },
        },
        ["MODEL-0002"] = new DiagnosticInfo
        {
            MessageFormat = "The script for the target language ‘{0}’ is not known to the base model {1}.",
            Category = "MODEL",
            Severity = Contracts.DiagnosticSeverity.Warn,
            DataTypes = new Dictionary<string, Type>
            {
                ["resolvedCode"] = typeof(string),
                ["modelName"] = typeof(string),
            },
        },
        ["MODEL-0003"] = new DiagnosticInfo
        {
            MessageFormat =
                "The average pretranslation model confidence {0} in book {1} is unusually low for the base model {2}.",
            Category = "MODEL",
            Severity = Contracts.DiagnosticSeverity.Warn,
            DataTypes = new Dictionary<string, Type>
            {
                ["averagePretranslationConfidence"] = typeof(double),
                ["bookId"] = typeof(string),
                ["modelName"] = typeof(string),
            },
            DataFormatters = new Dictionary<string, Func<object, string?>>
            {
                ["averagePretranslationConfidence"] = obj => ((double)obj).ToString("F3", CultureInfo.InvariantCulture),
            },
        },
        ["MODEL-0004"] = new DiagnosticInfo
        {
            MessageFormat =
                "The following language codes are unknown to the base model {0}: {1}; and no language data was selected for training.",
            Category = "MODEL",
            Severity = Contracts.DiagnosticSeverity.Error,
            DataTypes = new Dictionary<string, Type>
            {
                ["modelName"] = typeof(string),
                ["unknownLanguageCodes"] = typeof(List<string>),
            },
            DataFormatters = new Dictionary<string, Func<object, string?>>
            {
                ["unknownLanguageCodes"] = obj => string.Join(", ", (List<string>)obj),
            },
        },
        ["CONFIG-0001"] = new DiagnosticInfo
        {
            MessageFormat =
                "Unable to locate parent project {0} {1} of daughter project {2} {3} (parallel corpus {4}, monolingual corpus {5}).",
            Category = "CONFIG",
            Severity = Contracts.DiagnosticSeverity.Warn,
            DataTypes = new Dictionary<string, Type>
            {
                ["parentProjectName"] = typeof(string),
                ["parentProjectGuid"] = typeof(string),
                ["daughterProjectName"] = typeof(string),
                ["daughterProjectGuid"] = typeof(string),
                ["parallelCorpusId"] = typeof(string),
                ["monolingualCorpusId"] = typeof(string),
            },
        },
        ["CONFIG-0002"] = new DiagnosticInfo
        {
            MessageFormat =
                "There are multiple versifications represented among Paratext projects selected for training or inferencing: {0}.",
            Category = "CONFIG",
            Severity = Contracts.DiagnosticSeverity.Info,
            DataTypes = new Dictionary<string, Type> { ["projectVersifications"] = typeof(Dictionary<string, string>) },
            DataFormatters = new Dictionary<string, Func<object, string?>>
            {
                ["projectVersifications"] = obj =>
                {
                    var projectVersifications = (Dictionary<string, string>)obj;
                    return $"{{{string.Join(
                        ", ",
                        projectVersifications.Select(kvp => $"{kvp.Key}: {kvp.Value}")
                    )}}}";
                },
            },
        },
        ["CONFIG-0003"] = new DiagnosticInfo
        {
            MessageFormat =
                "Only {0} segments were selected for training. Training on fewer than {1} segments is not recommended.",
            Category = "CONFIG",
            Severity = Contracts.DiagnosticSeverity.Warn,
            DataTypes = new Dictionary<string, Type>
            {
                ["trainCount"] = typeof(int),
                ["minimumTrainCount"] = typeof(int),
            },
        },
        ["CONFIG-0004"] = new DiagnosticInfo
        {
            MessageFormat = "There was no data specified for inferencing and the model is not persisted.",
            Category = "CONFIG",
            Severity = Contracts.DiagnosticSeverity.Error,
            DataTypes = [],
        },
        ["USFM-0001"] = new DiagnosticInfo
        {
            MessageFormat =
                "Invalid chapter number in project {0} {1} at {2} line {3}, verse {4} (parallel corpus {5}, monolingual corpus {6}).",
            Category = "USFM",
            Severity = Contracts.DiagnosticSeverity.Warn,
            DataTypes = new Dictionary<string, Type>
            {
                ["projectName"] = typeof(string),
                ["projectGuid"] = typeof(string),
                ["usfmFileName"] = typeof(string),
                ["lineNumber"] = typeof(int),
                ["verseReference"] = typeof(string),
                ["parallelCorpusId"] = typeof(string),
                ["monolingualCorpusId"] = typeof(string),
            },
        },
        ["USFM-0002"] = new DiagnosticInfo
        {
            MessageFormat =
                "Invalid verse number in project {0} {1} at {2} line {3}, verse {4} (parallel corpus {5}, monolingual corpus {6}).",
            Category = "USFM",
            Severity = Contracts.DiagnosticSeverity.Warn,
            DataTypes = new Dictionary<string, Type>
            {
                ["projectName"] = typeof(string),
                ["projectGuid"] = typeof(string),
                ["usfmFileName"] = typeof(string),
                ["lineNumber"] = typeof(int),
                ["verseReference"] = typeof(string),
                ["parallelCorpusId"] = typeof(string),
                ["monolingualCorpusId"] = typeof(string),
            },
        },
        ["USFM-0003"] = new DiagnosticInfo
        {
            MessageFormat =
                "{0} extra verses in project {1} {2} at {3} lines {4}, verses {5} (parallel corpus {6}, monolingual corpus {7}).",
            Category = "USFM",
            Severity = Contracts.DiagnosticSeverity.Info,
            DataTypes = new Dictionary<string, Type>
            {
                ["numberOfVerses"] = typeof(int),
                ["projectName"] = typeof(string),
                ["projectGuid"] = typeof(string),
                ["usfmFileName"] = typeof(string),
                ["lineNumbers"] = typeof(List<int>),
                ["verseReferences"] = typeof(List<string>),
                ["parallelCorpusId"] = typeof(string),
                ["monolingualCorpusId"] = typeof(string),
            },
            DataFormatters = new Dictionary<string, Func<object, string?>>
            {
                ["lineNumbers"] = obj => string.Join(", ", (List<int>)obj),
                ["verseReferences"] = obj => string.Join(", ", (List<string>)obj),
            },
        },
        ["USFM-0004"] = new DiagnosticInfo
        {
            MessageFormat =
                "Missing {0} verses in project {1} {2} at {3} lines {4}, verse {5} (parallel corpus {6}, monolingual corpus {7}).",
            Category = "USFM",
            Severity = Contracts.DiagnosticSeverity.Warn,
            DataTypes = new Dictionary<string, Type>
            {
                ["numberOfVerses"] = typeof(int),
                ["projectName"] = typeof(string),
                ["projectGuid"] = typeof(string),
                ["usfmFileName"] = typeof(string),
                ["lineNumbers"] = typeof(List<int>),
                ["verseReferences"] = typeof(List<string>),
                ["parallelCorpusId"] = typeof(string),
                ["monolingualCorpusId"] = typeof(string),
            },
            DataFormatters = new Dictionary<string, Func<object, string?>>
            {
                ["lineNumbers"] = obj => string.Join(", ", (List<int>)obj),
                ["verseReferences"] = obj => string.Join(", ", (List<string>)obj),
            },
        },
        ["USFM-0005"] = new DiagnosticInfo
        {
            MessageFormat =
                "Incorrect verse segment in project {0} {1} at {2} line {3}, verse {4} (parallel corpus {5}, monolingual corpus {6}).",
            Category = "USFM",
            Severity = Contracts.DiagnosticSeverity.Info,
            DataTypes = new Dictionary<string, Type>
            {
                ["projectName"] = typeof(string),
                ["projectGuid"] = typeof(string),
                ["usfmFileName"] = typeof(string),
                ["lineNumber"] = typeof(int),
                ["verseReference"] = typeof(string),
                ["parallelCorpusId"] = typeof(string),
                ["monolingualCorpusId"] = typeof(string),
            },
        },
        ["USFM-0006"] = new DiagnosticInfo
        {
            MessageFormat =
                "Unsupported verse range in project {0} {1} at {2} line {3}, verse {4} (parallel corpus {5}, monolingual corpus {6}).",
            Category = "USFM",
            Severity = Contracts.DiagnosticSeverity.Info,
            DataTypes = new Dictionary<string, Type>
            {
                ["projectName"] = typeof(string),
                ["projectGuid"] = typeof(string),
                ["usfmFileName"] = typeof(string),
                ["lineNumber"] = typeof(int),
                ["verseReference"] = typeof(string),
                ["parallelCorpusId"] = typeof(string),
                ["monolingualCorpusId"] = typeof(string),
            },
        },
    };

    public DiagnosticContract CreateDiagnostic(string code, Dictionary<string, object> data)
    {
        DiagnosticInfo diagnosticInfo = GetDiagnosticInfo(code, data);
        return new DiagnosticContract
        {
            Code = code,
            Message = diagnosticInfo.FormatMessage(data),
            Severity = diagnosticInfo.Severity,
            Category = diagnosticInfo.Category,
            Data = data,
        };
    }

    private static DiagnosticInfo GetDiagnosticInfo(string code, Dictionary<string, object> data)
    {
        if (!Diagnostics.TryGetValue(code, out DiagnosticInfo? diagnosticInfo))
        {
            throw new ArgumentException($"Unknown diagnostic code ‘{code}’.");
        }

        foreach (var (key, type) in diagnosticInfo.DataTypes)
        {
            if (!data.TryGetValue(key, out object? value))
            {
                throw new ArgumentException($"Missing required data for diagnostic code {code}: {key}.");
            }

            if (value is null || type != value.GetType())
            {
                throw new ArgumentException(
                    $"Invalid data type for diagnostic code {code}: {key} must be of type {type.Name}."
                );
            }
        }

        return diagnosticInfo;
    }
}
