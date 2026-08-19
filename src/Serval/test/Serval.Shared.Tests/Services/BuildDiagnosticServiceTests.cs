namespace Serval.Shared.Services;

[TestFixture]
public class BuildDiagnosticServiceTests
{
    private IBuildDiagnosticService _service;

    [SetUp]
    public void SetUp()
    {
        _service = new BuildDiagnosticService();
    }

    [Test]
    public void CreateDiagnostic_UnknownCode()
    {
        var code = "ASDF-1234";
        var data = new Dictionary<string, object> { { "parentProjectName", "Tes" } };

        ArgumentException? ex = Assert.Throws<ArgumentException>(() => _service.CreateDiagnostic(code, data));
        Assert.That(ex.Message, Is.EqualTo("Unknown diagnostic code ‘ASDF-1234’."));
    }

    [Test]
    public void CreateDiagnostic_Config0001_MissingData()
    {
        var code = "CONFIG-0001";
        var data = new Dictionary<string, object>
        {
            { "parentProjectName", "Tes" },
            { "parentProjectGuid", "parent-guid" },
            { "daughterProjectName", "TesBT" },
            { "daughterProjectGuid", "daughter-guid" },
            { "monolingualCorpusId", "monolingual-corpus-id" },
        };

        ArgumentException? ex = Assert.Throws<ArgumentException>(() => _service.CreateDiagnostic(code, data));
        Assert.That(ex.Message, Is.EqualTo("Missing required data for diagnostic code CONFIG-0001: parallelCorpusId."));
    }

    [Test]
    public void CreateDiagnostic_Config0001_IncorrectDataType()
    {
        var code = "CONFIG-0001";
        var data = new Dictionary<string, object>
        {
            { "parentProjectName", "Tes" },
            { "parentProjectGuid", 1234 },
            { "daughterProjectName", "TesBT" },
            { "daughterProjectGuid", "daughter-guid" },
            { "monolingualCorpusId", "monolingual-corpus-id" },
        };

        ArgumentException? ex = Assert.Throws<ArgumentException>(() => _service.CreateDiagnostic(code, data));
        Assert.That(
            ex.Message,
            Is.EqualTo("Invalid data type for diagnostic code CONFIG-0001: parentProjectGuid must be of type String.")
        );
    }

    [Test]
    public void CreateDiagnostic_Config0001_DataOutOfOrder()
    {
        var code = "CONFIG-0001";
        var data = new Dictionary<string, object>
        {
            { "daughterProjectGuid", "daughter-guid" },
            { "parallelCorpusId", "parallel-corpus-id" },
            { "parentProjectName", "Tes" },
            { "parentProjectGuid", "parent-guid" },
            { "monolingualCorpusId", "monolingual-corpus-id" },
            { "daughterProjectName", "TesBT" },
        };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("CONFIG"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Warn));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "Unable to locate parent project Tes parent-guid of daughter project TesBT daughter-guid (parallel corpus parallel-corpus-id, monolingual corpus monolingual-corpus-id)."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Config0001()
    {
        var code = "CONFIG-0001";
        var data = new Dictionary<string, object>
        {
            { "parentProjectName", "Tes" },
            { "parentProjectGuid", "parent-guid" },
            { "daughterProjectName", "TesBT" },
            { "daughterProjectGuid", "daughter-guid" },
            { "parallelCorpusId", "parallel-corpus-id" },
            { "monolingualCorpusId", "monolingual-corpus-id" },
        };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("CONFIG"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Warn));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "Unable to locate parent project Tes parent-guid of daughter project TesBT daughter-guid (parallel corpus parallel-corpus-id, monolingual corpus monolingual-corpus-id)."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Config0002()
    {
        var code = "CONFIG-0002";
        var data = new Dictionary<string, object>
        {
            {
                "projectVersifications",
                new Dictionary<string, string> { { "project-guid-1", "Original" }, { "project-guid-2", "English" } }
            },
        };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("CONFIG"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Info));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "There are multiple versifications represented among Paratext projects selected for training or inferencing: {project-guid-1: Original, project-guid-2: English}."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Config0003()
    {
        var code = "CONFIG-0003";
        var data = new Dictionary<string, object> { { "trainCount", 10 }, { "minimumTrainCount", 600 } };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("CONFIG"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Warn));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "Only 10 segments were selected for training. Training on fewer than 600 segments is not recommended."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Config0004()
    {
        var code = "CONFIG-0004";
        var data = new Dictionary<string, object>();

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("CONFIG"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Error));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo("There was no data specified for inferencing and the model is not persisted.")
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Model0001()
    {
        var code = "MODEL-0001";
        var data = new Dictionary<string, object> { { "resolvedCode", "eng_Latn" }, { "modelName", "test-model" } };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("MODEL"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Warn));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo("The script for the source language ‘eng_Latn’ is not known to the base model test-model.")
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Model0002()
    {
        var code = "MODEL-0002";
        var data = new Dictionary<string, object> { { "resolvedCode", "eng_Latn" }, { "modelName", "test-model" } };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("MODEL"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Warn));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo("The script for the target language ‘eng_Latn’ is not known to the base model test-model.")
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Model0003()
    {
        var code = "MODEL-0003";
        var data = new Dictionary<string, object>
        {
            { "averagePretranslationConfidence", 0.370111 },
            { "bookId", "MAT" },
            { "modelName", "test-model" },
        };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("MODEL"));
        Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warn));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "The average pretranslation model confidence 0.370 in book MAT is unusually low for the base model test-model."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Model0004()
    {
        var code = "MODEL-0004";
        var data = new Dictionary<string, object>
        {
            { "modelName", "test-model" },
            {
                "unknownLanguageCodes",
                new List<string> { "spa_Latn", "eng_Latn" }
            },
        };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("MODEL"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Error));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "The following language codes are unknown to the base model test-model: spa_Latn, eng_Latn; and no language data was selected for training."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Usfm0001()
    {
        var code = "USFM-0001";
        var data = new Dictionary<string, object>
        {
            { "projectName", "Tes" },
            { "projectGuid", "project-guid" },
            { "usfmFileName", "MAT.USFM" },
            { "lineNumber", 12 },
            { "verseReference", "1" },
            { "parallelCorpusId", "parallel-corpus-id" },
            { "monolingualCorpusId", "monolingual-corpus-id" },
        };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("USFM"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Warn));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "Invalid chapter number in project Tes project-guid at MAT.USFM line 12, verse 1 (parallel corpus parallel-corpus-id, monolingual corpus monolingual-corpus-id)."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Usfm0002()
    {
        var code = "USFM-0002";
        var data = new Dictionary<string, object>
        {
            { "projectName", "Tes" },
            { "projectGuid", "project-guid" },
            { "usfmFileName", "MAT.USFM" },
            { "lineNumber", 12 },
            { "verseReference", "1" },
            { "parallelCorpusId", "parallel-corpus-id" },
            { "monolingualCorpusId", "monolingual-corpus-id" },
        };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("USFM"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Warn));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "Invalid verse number in project Tes project-guid at MAT.USFM line 12, verse 1 (parallel corpus parallel-corpus-id, monolingual corpus monolingual-corpus-id)."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Usfm0003()
    {
        var code = "USFM-0003";
        var data = new Dictionary<string, object>
        {
            { "numberOfVerses", 2 },
            { "projectName", "Tes" },
            { "projectGuid", "project-guid" },
            { "usfmFileName", "MAT.USFM" },
            {
                "lineNumbers",
                new List<int> { 3, 4 }
            },
            {
                "verseReferences",
                new List<string> { "MAT 1:1", "MAT 1:2" }
            },
            { "parallelCorpusId", "parallel-corpus-id" },
            { "monolingualCorpusId", "monolingual-corpus-id" },
        };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("USFM"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Info));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "2 extra verses in project Tes project-guid at MAT.USFM lines 3, 4, verses MAT 1:1, MAT 1:2 (parallel corpus parallel-corpus-id, monolingual corpus monolingual-corpus-id)."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Usfm0004()
    {
        var code = "USFM-0004";
        var data = new Dictionary<string, object>
        {
            { "numberOfVerses", 2 },
            { "projectName", "Tes" },
            { "projectGuid", "project-guid" },
            { "usfmFileName", "MAT.USFM" },
            {
                "lineNumbers",
                new List<int> { 3, 4 }
            },
            {
                "verseReferences",
                new List<string> { "MAT 1:1", "MAT 1:2" }
            },
            { "parallelCorpusId", "parallel-corpus-id" },
            { "monolingualCorpusId", "monolingual-corpus-id" },
        };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("USFM"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Warn));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "Missing 2 verses in project Tes project-guid at MAT.USFM lines 3, 4, verse MAT 1:1, MAT 1:2 (parallel corpus parallel-corpus-id, monolingual corpus monolingual-corpus-id)."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Usfm0005()
    {
        var code = "USFM-0005";
        var data = new Dictionary<string, object>
        {
            { "projectName", "Tes" },
            { "projectGuid", "project-guid" },
            { "usfmFileName", "MAT.USFM" },
            { "lineNumber", 12 },
            { "verseReference", "MAT 1:1a" },
            { "parallelCorpusId", "parallel-corpus-id" },
            { "monolingualCorpusId", "monolingual-corpus-id" },
        };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("USFM"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Info));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "Incorrect verse segment in project Tes project-guid at MAT.USFM line 12, verse MAT 1:1a (parallel corpus parallel-corpus-id, monolingual corpus monolingual-corpus-id)."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }

    [Test]
    public void CreateDiagnostic_Usfm0006()
    {
        var code = "USFM-0006";
        var data = new Dictionary<string, object>
        {
            { "projectName", "Tes" },
            { "projectGuid", "project-guid" },
            { "usfmFileName", "MAT.USFM" },
            { "lineNumber", 12 },
            { "verseReference", "MAT 1:1-12" },
            { "parallelCorpusId", "parallel-corpus-id" },
            { "monolingualCorpusId", "monolingual-corpus-id" },
        };

        DiagnosticContract diagnostic = _service.CreateDiagnostic(code, data);

        Assert.That(diagnostic.Code, Is.EqualTo(code));
        Assert.That(diagnostic.Category, Is.EqualTo("USFM"));
        Assert.That(diagnostic.Severity, Is.EqualTo(Contracts.DiagnosticSeverity.Info));
        Assert.That(
            diagnostic.Message,
            Is.EqualTo(
                "Unsupported verse range in project Tes project-guid at MAT.USFM line 12, verse MAT 1:1-12 (parallel corpus parallel-corpus-id, monolingual corpus monolingual-corpus-id)."
            )
        );
        Assert.That(diagnostic.Data, Is.EqualTo(data));
    }
}
