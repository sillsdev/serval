using System.Net;
using NUnit.Framework;

namespace Serval.SpecFlowTests.StepDefinitions;

[Binding]
public sealed class MachineApiStepDefinitions
{
    // QA int server: "https://machine-api.org/"
    // QA ext server: "https://qa.serval-api.org/"
    // localhost: "http://localhost/"
    const string MACHINE_API_TEST_URL = "http://localhost/";
    readonly Dictionary<string, string> EnginePerUser = new();
    readonly Dictionary<string, string> CorporaPerName = new();

    // For additional details on SpecFlow step definitions see https://go.specflow.org/doc-stepdef
    private readonly HttpClient httpClient;
    private readonly DataFilesClient dataFilesClient;
    private readonly TranslationEnginesClient translationEnginesClient;
    private string? bearer = null;

    private TranslationBuildConfig translationBuildConfig = new TranslationBuildConfig
    {
        Pretranslate = new List<PretranslateCorpusConfig>()
    };

    public MachineApiStepDefinitions()
    {
        //ignore ssl errors
        var handler = new HttpClientHandler();
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) =>
        {
            return true;
        };
        httpClient = new HttpClient(handler);
        httpClient.BaseAddress = new Uri(MACHINE_API_TEST_URL);

        dataFilesClient = new DataFilesClient(httpClient);
        translationEnginesClient = new TranslationEnginesClient(httpClient);
    }

    private async void setBearer()
    {
        if (bearer is not null)
        {
            return;
        }
        bearer = await Utilities.GetAccessTokenFromEnvironment(httpClient);
        httpClient.DefaultRequestHeaders.Add("authorization", $"Bearer {bearer}");
    }

    [Given(@"a new (.*) engine for (.*) from (.*) to (.*)")]
    public async Task GivenNewEngine(
        string engineTypeString,
        string user,
        string source_language,
        string target_language
    )
    {
        setBearer();
        var existingTranslationEngines = await translationEnginesClient.GetAllAsync();
        foreach (var translationEngine in existingTranslationEngines)
        {
            if (translationEngine.Name == user)
            {
                await translationEnginesClient.DeleteAsync(translationEngine.Id);
            }
        }
        var engine = await translationEnginesClient.CreateAsync(
            new TranslationEngineConfig
            {
                Name = user,
                SourceLanguage = source_language,
                TargetLanguage = target_language,
                Type = engineTypeString
            }
        );
        EnginePerUser.Add(user, engine.Id);
        ClearPretranslationConfig();
    }

    [When(@"a (.*) corpora containing (.*) are added to (.*)'s engine in (.*) and (.*)")]
    public async Task GivenCorporaForEngineNoTranslate(
        string fileFormatString,
        string filesToAddString,
        string user,
        string sourceLanguage,
        string targetLanguage
    )
    {
        await GivenCorporaForEngine(
            fileFormatString,
            filesToAddString,
            user,
            sourceLanguage,
            targetLanguage,
            pretranslate: false
        );
    }

    [When(@"a (.*) corpora containing (.*) are added to (.*)'s engine in (.*) to translate into (.*)")]
    public async Task GivenCorporaForEngineToTranslate(
        string fileFormatString,
        string filesToAddString,
        string user,
        string sourceLanguage,
        string targetLanguage
    )
    {
        await GivenCorporaForEngine(
            fileFormatString,
            filesToAddString,
            user,
            sourceLanguage,
            targetLanguage,
            pretranslate: true
        );
    }

    public async Task GivenCorporaForEngine(
        string fileFormatString,
        string filesToAddString,
        string user,
        string sourceLanguage,
        string targetLanguage,
        bool pretranslate
    )
    {
        if (!Enum.TryParse(fileFormatString, ignoreCase: true, result: out FileFormat fileFormat))
            throw new ArgumentException(
                "Corpus format type needs to be one of: " + string.Join(", ", EnumToStringList<FileFormat>())
            );
        var filesToAdd = filesToAddString.Split(", ");
        var engineId = await GetEngineFromUser(user);

        var corpusId = await PostCorpusToEngine(
            engineId,
            fileFormat: fileFormat,
            filesToAdd,
            sourceLanguage,
            targetLanguage,
            pretranslate
        );
    }

    [When(@"(.*)'s engine is built")]
    public async Task WhenEngineIsBuild(string user)
    {
        var engineId = await GetEngineFromUser(user);
        var newJob = await translationEnginesClient.StartBuildAsync(engineId, translationBuildConfig);
        int cRevision = newJob.Revision;
        while (true)
        {
            var result = await translationEnginesClient.GetBuildAsync(engineId, newJob.Id);
            if (!(result.State == JobState.Active || result.State == JobState.Pending))
            {
                // build completed
                break;
            }
            Thread.Sleep(500);
        }
    }

    [When(@"a translation for (.*) is added with ""(.*)"" for ""(.*)""")]
    public async Task WhenTranslationAdded(string user, string targetSegment, string sourceSegment)
    {
        var engineId = await GetEngineFromUser(user);
        await translationEnginesClient.TrainSegmentAsync(
            engineId,
            new SegmentPair
            {
                SourceSegment = sourceSegment,
                TargetSegment = targetSegment,
                SentenceStart = true
            }
        );
    }

    [When(@"the translation for (.*) for ""(.*)"" is ""(.*)""")]
    public async Task WhenTheTranslationIs(string user, string sourceSegment, string targetSegment)
    {
        await ThenTheTranslationShouldBe(user, sourceSegment, targetSegment);
    }

    [Then(@"the translation for (.*) for ""(.*)"" should be ""(.*)""")]
    public async Task ThenTheTranslationShouldBe(string user, string sourceSegment, string targetSegment)
    {
        var engineId = await GetEngineFromUser(user);
        var translation = await translationEnginesClient.TranslateAsync(engineId, sourceSegment);
        Assert.AreEqual(targetSegment, translation.Translation);
    }

    [Then(@"the pretranslation for (.*) for (.*) starts with ""(.*)""")]
    public async Task ThenThePretranslationShouldBe(string user, string fileId, string targetSegment)
    {
        var engineId = await GetEngineFromUser(user);
        if (translationBuildConfig.Pretranslate == null | translationBuildConfig.Pretranslate!.Count == 0)
        {
            throw new Exception("Need to have something to pretranslate!");
        }
        // just do the first one.  This is lazy but it should work for the time being.
        var pretranslations = await translationEnginesClient.GetAllPretranslationsAsync(
            engineId,
            translationBuildConfig.Pretranslate[0].CorpusId
        );
        Assert.IsTrue(
            pretranslations[0].Translation.StartsWith(targetSegment),
            string.Concat(
                "The pretranslation should start with ",
                targetSegment,
                " but instead starts with ",
                pretranslations[0].Translation.AsSpan(0, 30)
            )
        );
    }

    public async Task<string> GetEngineFromUser(string user)
    {
        if (EnginePerUser.ContainsKey(user))
            return EnginePerUser[user];
        var engines = await translationEnginesClient.GetAllAsync();
        foreach (var engine in engines)
        {
            if (engine.Name == user)
                return engine.Id;
        }
        throw new ArgumentException($"No engine for user {user} available.");
    }

    public async Task<string> PostCorpusToEngine(
        string engineId,
        FileFormat fileFormat,
        string[] filesToAdd,
        string sourceLanguage,
        string targetLanguage,
        bool pretranslate
    )
    {
        var sourceFiles = await PostFiles(filesToAdd, fileFormat, sourceLanguage);
        var sourceFileConfig = new List<TranslationCorpusFileConfig>();

        foreach (var item in sourceFiles.Select((file, i) => new { i, file }))
        {
            sourceFileConfig.Add(
                new TranslationCorpusFileConfig { FileId = item.file.Id, TextId = filesToAdd[item.i] }
            );
        }

        var targetFileConfig = new List<TranslationCorpusFileConfig>();
        var targetFiles = await PostFiles(filesToAdd, fileFormat, targetLanguage);
        foreach (var item in targetFiles.Select((file, i) => new { i, file }))
        {
            targetFileConfig.Add(
                new TranslationCorpusFileConfig { FileId = item.file.Id, TextId = filesToAdd[item.i] }
            );
        }

        var response = await translationEnginesClient.AddCorpusAsync(
            id: engineId,
            new TranslationCorpusConfig
            {
                Name = "None",
                SourceFiles = sourceFileConfig,
                SourceLanguage = sourceLanguage,
                TargetFiles = targetFileConfig,
                TargetLanguage = targetLanguage
            }
        );

        if (pretranslate)
        {
            translationBuildConfig.Pretranslate!.Add(
                new PretranslateCorpusConfig { CorpusId = response.Id, TextIds = filesToAdd.ToList() }
            );
        }

        return response.Id;
    }

    private void ClearPretranslationConfig()
    {
        translationBuildConfig.Pretranslate = new List<PretranslateCorpusConfig>();
    }

    public async Task<List<DataFile>> PostFiles(IEnumerable<string> filesToAdd, FileFormat fileFormat, string language)
    {
        string languageFolder = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../..", "data", language)
        );
        if (!Directory.Exists(languageFolder))
            throw new ArgumentException($"The langauge data directory {languageFolder} does not exist!");
        // Collect files for the corpus
        var files = Directory.GetFiles(languageFolder);
        if (files.Length == 0)
            throw new ArgumentException($"The langauge data directory {languageFolder} contains no files!");
        var fileList = new List<DataFile>();
        var all_files = await dataFilesClient.GetAllAsync();
        var filename_to_id = (from file in all_files where file.Name is not null select file).ToLookup(
            file => file.Name!,
            file => file.Id
        );

        foreach (var fileName in filesToAdd)
        {
            var full_name = language + ":" + fileName;

            //delete files that have the name name
            if (filename_to_id.Contains(full_name))
            {
                var matched_files = filename_to_id[full_name];
                foreach (var fileId in matched_files)
                {
                    await dataFilesClient.DeleteAsync(fileId);
                }
            }

            //add the new files
            string filePath = Path.GetFullPath(Path.Combine(languageFolder, fileName));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The corpus file {filePath} does not exist!");
            var response = await dataFilesClient.CreateAsync(
                file: new FileParameter(data: File.OpenRead(filePath), fileName: fileName),
                format: fileFormat,
                name: language + ":" + fileName
            );
            fileList.Add(response);
        }
        return fileList;
    }

    public static IEnumerable<string> EnumToStringList<T>()
        where T : Enum
    {
        return ((IEnumerable<T>)Enum.GetValues(typeof(T))).Select(v => v.ToString());
    }
}
