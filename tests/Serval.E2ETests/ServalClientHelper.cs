namespace Serval.E2ETests;

public class ServalClientHelper : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, string> _enginePerUser = [];
    private readonly string _prefix;

    public ServalClientHelper(string audience, string prefix = "SCE_", bool ignoreSSLErrors = false)
    {
        Dictionary<string, string> env = GetEnvironment();
        //setup http client
        if (ignoreSSLErrors)
        {
            var handler = GetHttHandlerToIgnoreSslErrors();
            _httpClient = new HttpClient(handler);
        }
        else
            _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(env["hostUrl"]);
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        DataFilesClient = new DataFilesClient(_httpClient);
        TranslationEnginesClient = new TranslationEnginesClient(_httpClient);
        TranslationEngineTypesClient = new TranslationEngineTypesClient(_httpClient);
        _httpClient.DefaultRequestHeaders.Add(
            "authorization",
            $"Bearer {GetAuth0Authentication(env["authUrl"], audience, env["clientId"], env["clientSecret"]).Result}"
        );
        _prefix = prefix;
        TranslationBuildConfig = new TranslationBuildConfig
        {
            Pretranslate = new List<PretranslateCorpusConfig>(),
            Options = "{\"max_steps\":10}"
        };
    }

    public DataFilesClient DataFilesClient { get; }
    public TranslationEnginesClient TranslationEnginesClient { get; }
    public TranslationEngineTypesClient TranslationEngineTypesClient { get; }

    public TranslationBuildConfig TranslationBuildConfig { get; set; }

    public static Dictionary<string, string> GetEnvironment()
    {
        Dictionary<string, string> env =
            new()
            {
                { "hostUrl", Environment.GetEnvironmentVariable("SERVAL_HOST_URL") ?? "" },
                { "clientId", Environment.GetEnvironmentVariable("SERVAL_CLIENT_ID") ?? "" },
                { "clientSecret", Environment.GetEnvironmentVariable("SERVAL_CLIENT_SECRET") ?? "" },
                { "authUrl", Environment.GetEnvironmentVariable("SERVAL_AUTH_URL") ?? "" }
            };
        if (env["hostUrl"] == null)
        {
            Console.WriteLine(
                "You need a serval host url in the environment variable SERVAL_HOST_URL!  Look at README for instructions on getting one."
            );
        }
        else if (env["clientId"] == null)
        {
            Console.WriteLine(
                "You need an auth0 client_id in the environment variable SERVAL_CLIENT_ID!  Look at README for instructions on getting one."
            );
        }
        else if (env["clientSecret"] == null)
        {
            Console.WriteLine(
                "You need an auth0 client_secret in the environment variable SERVAL_CLIENT_SECRET!  Look at README for instructions on getting one."
            );
        }
        else if (env["authUrl"] == null)
        {
            Console.WriteLine(
                "You need an auth0 authorization url in the environment variable SERVAL_AUTH_URL!  Look at README for instructions on getting one."
            );
        }
        return env;
    }

    public async Task ClearEngines(string name = "")
    {
        IList<TranslationEngine> existingTranslationEngines = await TranslationEnginesClient.GetAllAsync();
        foreach (var translationEngine in existingTranslationEngines)
        {
            if (translationEngine.Name?.Contains(_prefix + name) ?? false)
            {
                await TranslationEnginesClient.DeleteAsync(translationEngine.Id);
            }
        }
        TranslationBuildConfig.Pretranslate = new List<PretranslateCorpusConfig>();
        _enginePerUser.Clear();
    }

    public async Task<string> CreateNewEngine(
        string engineTypeString,
        string source_language,
        string target_language,
        string name = ""
    )
    {
        var engine = await TranslationEnginesClient.CreateAsync(
            new TranslationEngineConfig
            {
                Name = _prefix + name,
                SourceLanguage = source_language,
                TargetLanguage = target_language,
                Type = engineTypeString
            }
        );
        _enginePerUser.Add(name, engine.Id);
        return engine.Id;
    }

    public async Task<TranslationBuild> StartBuildAsync(string engineId)
    {
        return await TranslationEnginesClient.StartBuildAsync(engineId, TranslationBuildConfig);
    }

    public async Task BuildEngine(string engineId)
    {
        var newJob = await StartBuildAsync(engineId);
        int revision = newJob.Revision;
        await TranslationEnginesClient.GetBuildAsync(engineId, newJob.Id, newJob.Revision);
        while (true)
        {
            try
            {
                var result = await TranslationEnginesClient.GetBuildAsync(engineId, newJob.Id, revision + 1);
                if (!(result.State == JobState.Active || result.State == JobState.Pending))
                {
                    // build completed
                    break;
                }
                revision = result.Revision;
            }
            catch (ServalApiException e)
            {
                if (e.StatusCode != 408)
                    throw;

                // Throttle requests
                await Task.Delay(500);
            }
        }
    }

    public async Task CancelBuild(string engineId, string buildId, int timeoutSeconds = 20)
    {
        await TranslationEnginesClient.CancelBuildAsync(engineId);
        int pollIntervalMs = 1000;
        int tries = 1;
        while (true)
        {
            var build = await TranslationEnginesClient.GetBuildAsync(engineId, buildId);
            if (build.State != JobState.Pending && build.State != JobState.Active)
            {
                break;
            }
            if (tries++ > timeoutSeconds)
            {
                throw new TimeoutException($"The job did not fully cancel in {timeoutSeconds}");
            }
            await Task.Delay(pollIntervalMs);
        }
    }

    public async Task<string> AddTextCorpusToEngine(
        string engineId,
        string[] filesToAdd,
        string sourceLanguage,
        string targetLanguage,
        bool pretranslate
    )
    {
        List<DataFile> sourceFiles = await UploadFiles(filesToAdd, FileFormat.Text, sourceLanguage);

        var targetFileConfig = new List<TranslationCorpusFileConfig>();
        if (!pretranslate)
        {
            var targetFiles = await UploadFiles(filesToAdd, FileFormat.Text, targetLanguage);
            foreach (var item in targetFiles.Select((file, i) => new { i, file }))
            {
                targetFileConfig.Add(
                    new TranslationCorpusFileConfig { FileId = item.file.Id, TextId = filesToAdd[item.i] }
                );
            }
        }

        var sourceFileConfig = new List<TranslationCorpusFileConfig>();

        if (sourceLanguage == targetLanguage && !pretranslate)
        {
            // if it's the same langague, and we are not pretranslating, do nothing (echo for suggestions)
            // if pretranslating, we need to upload the source separately
            // if different languages, we are not echoing.
        }
        else
        {
            for (var i = 0; i < sourceFiles.Count; i++)
            {
                sourceFileConfig.Add(
                    new TranslationCorpusFileConfig { FileId = sourceFiles[i].Id, TextId = filesToAdd[i] }
                );
            }
        }

        var response = await TranslationEnginesClient.AddCorpusAsync(
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
            TranslationBuildConfig.Pretranslate!.Add(
                new PretranslateCorpusConfig { CorpusId = response.Id, TextIds = filesToAdd.ToList() }
            );
        }

        return response.Id;
    }

    public async Task<List<DataFile>> UploadFiles(
        IEnumerable<string> filesToAdd,
        FileFormat fileFormat,
        string language
    )
    {
        string languageFolder = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../..", "data", language)
        );
        if (!Directory.Exists(languageFolder))
            throw new ArgumentException($"The language data directory {languageFolder} does not exist!");
        // Collect files for the corpus
        var files = Directory.GetFiles(languageFolder);
        if (files.Length == 0)
            throw new ArgumentException($"The language data directory {languageFolder} contains no files!");
        var fileList = new List<DataFile>();
        var allFiles = await DataFilesClient.GetAllAsync();
        ILookup<string, string> filenameToId = allFiles
            .Where(f => f.Name is not null)
            .ToLookup(file => file.Name!, file => file.Id);

        foreach (var fileName in filesToAdd)
        {
            var fullName = _prefix + language + "_" + fileName;

            //delete files that have the name name
            if (filenameToId.Contains(fullName))
            {
                var matchedFiles = filenameToId[fullName];
                foreach (var fileId in matchedFiles)
                {
                    await DataFilesClient.DeleteAsync(fileId);
                }
            }

            //add the new files
            string filePath = Path.GetFullPath(Path.Combine(languageFolder, fileName));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The corpus file {filePath} does not exist!");
            var response = await DataFilesClient.CreateAsync(
                file: new FileParameter(data: File.OpenRead(filePath), fileName: fileName),
                format: fileFormat,
                name: fullName
            );
            fileList.Add(response);
        }
        return fileList;
    }

    private static async Task<string> GetAuth0Authentication(
        string authUrl,
        string audience,
        string clientId,
        string clientSecret
    )
    {
        var authHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var request = new HttpRequestMessage(HttpMethod.Post, authUrl + "/oauth/token")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "audience", audience },
                }
            )
        };
        var response = authHttpClient.SendAsync(request).Result;
        if (response.Content is null)
            throw new HttpRequestException("Error getting auth0 Authentication.");
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(await response.Content.ReadAsStringAsync());
        return dict?["access_token"] ?? "";
    }

    private static HttpClientHandler GetHttHandlerToIgnoreSslErrors()
    {
        //ignore ssl errors
        HttpClientHandler handler =
            new()
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) =>
                {
                    return true;
                }
            };
        return handler;
    }

    public async ValueTask DisposeAsync()
    {
        await ClearEngines();

        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
