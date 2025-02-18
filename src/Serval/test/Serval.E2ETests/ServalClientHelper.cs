namespace Serval.E2ETests;

#pragma warning disable CS0612 // Type or member is obsolete

public class ServalClientHelper : IAsyncDisposable
{
    public DataFilesClient DataFilesClient { get; }
    public CorporaClient CorporaClient { get; }
    public TranslationEnginesClient TranslationEnginesClient { get; }
    public TranslationEngineTypesClient TranslationEngineTypesClient { get; }

    public TranslationBuildConfig TranslationBuildConfig { get; set; }

    private string _authToken = "";
    private readonly HttpClient _httpClient;
    private readonly string _prefix;
    private readonly string _audience;

    public ServalClientHelper(string audience, string prefix = "SCE_", bool ignoreSSLErrors = false)
    {
        _audience = audience;
        //setup http client
        if (ignoreSSLErrors)
        {
            HttpClientHandler handler = GetHttHandlerToIgnoreSslErrors();
            _httpClient = new HttpClient(handler);
        }
        else
        {
            _httpClient = new HttpClient();
        }
        string? hostUrl = Environment.GetEnvironmentVariable("SERVAL_HOST_URL");
        if (hostUrl is null)
            throw new InvalidOperationException("The environment variable SERVAL_HOST_URL is not set.");
        _httpClient.BaseAddress = new Uri(hostUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        DataFilesClient = new DataFilesClient(_httpClient);
        CorporaClient = new CorporaClient(_httpClient);
        TranslationEnginesClient = new TranslationEnginesClient(_httpClient);
        TranslationEngineTypesClient = new TranslationEngineTypesClient(_httpClient);
        _prefix = prefix;
        TranslationBuildConfig = InitTranslationBuildConfig();
    }

    public async Task InitAsync()
    {
        string? authUrl = Environment.GetEnvironmentVariable("SERVAL_AUTH_URL");
        if (authUrl is null)
            throw new InvalidOperationException("The environment variable SERVAL_AUTH_URL is not set.");
        string? clientId = Environment.GetEnvironmentVariable("SERVAL_CLIENT_ID");
        if (clientId is null)
            throw new InvalidOperationException("The environment variable SERVAL_CLIENT_ID is not set.");
        string? clientSecret = Environment.GetEnvironmentVariable("SERVAL_CLIENT_SECRET");
        if (clientSecret is null)
            throw new InvalidOperationException("The environment variable SERVAL_CLIENT_SECRET is not set.");

        if (string.IsNullOrEmpty(_authToken))
        {
            _authToken = await GetAuth0AuthenticationAsync(authUrl, _audience, clientId, clientSecret);
            _httpClient.DefaultRequestHeaders.Add("authorization", $"Bearer {_authToken}");
        }
        await ClearEnginesAsync();
    }

    public void Setup()
    {
        InitTranslationBuildConfig();
    }

    public TranslationBuildConfig InitTranslationBuildConfig()
    {
        TranslationBuildConfig = new TranslationBuildConfig
        {
            Pretranslate = [],
            TrainOn = null,
            Options = """
                {
                    "max_steps": 10,
                    "parent_model_name": "hf-internal-testing/tiny-random-nllb",
                    "train_params":
                    {
                        "per_device_train_batch_size": 4
                    }
                }
                """
        };
        return TranslationBuildConfig;
    }

    public async Task ClearEnginesAsync()
    {
        IList<TranslationEngine> existingTranslationEngines = await TranslationEnginesClient.GetAllAsync();
        foreach (TranslationEngine translationEngine in existingTranslationEngines)
        {
            if (translationEngine.Name?.Contains(_prefix) ?? false)
                await TranslationEnginesClient.DeleteAsync(translationEngine.Id);
        }
    }

    public async Task<string> CreateNewEngineAsync(
        string engineTypeString,
        string source_language,
        string target_language,
        string name = "",
        bool? isModelPersisted = null
    )
    {
        TranslationEngine engine = await TranslationEnginesClient.CreateAsync(
            new TranslationEngineConfig
            {
                Name = _prefix + name,
                SourceLanguage = source_language,
                TargetLanguage = target_language,
                Type = engineTypeString,
                IsModelPersisted = isModelPersisted
            }
        );
        return engine.Id;
    }

    public async Task<TranslationBuild> StartBuildAsync(string engineId)
    {
        return await TranslationEnginesClient.StartBuildAsync(engineId, TranslationBuildConfig);
    }

    public async Task<string> BuildEngineAsync(string engineId)
    {
        TranslationBuild newJob = await StartBuildAsync(engineId);
        int revision = newJob.Revision;
        await TranslationEnginesClient.GetBuildAsync(engineId, newJob.Id, newJob.Revision);
        while (true)
        {
            try
            {
                TranslationBuild result = await TranslationEnginesClient.GetBuildAsync(
                    engineId,
                    newJob.Id,
                    revision + 1
                );
                if (!(result.State == JobState.Active || result.State == JobState.Pending))
                    // build completed
                    break;
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
        return newJob.Id;
    }

    public async Task CancelBuildAsync(string engineId, string buildId, int timeoutSeconds = 20)
    {
        await TranslationEnginesClient.CancelBuildAsync(engineId);
        int pollIntervalMs = 1000;
        int tries = 1;
        while (true)
        {
            TranslationBuild build = await TranslationEnginesClient.GetBuildAsync(engineId, buildId);
            if (build.State != JobState.Pending && build.State != JobState.Active)
                break;
            if (tries++ > timeoutSeconds)
            {
                throw new TimeoutException($"The job did not fully cancel in {timeoutSeconds}");
            }
            await Task.Delay(pollIntervalMs);
        }
    }

    public async Task<string> AddTextCorpusToEngineAsync(
        string engineId,
        string[] filesToAdd,
        string sourceLanguage,
        string targetLanguage,
        bool pretranslate
    )
    {
        List<DataFile> sourceFiles = await UploadFilesAsync(
            filesToAdd,
            FileFormat.Text,
            sourceLanguage,
            isTarget: false
        );

        var targetFileConfig = new List<TranslationCorpusFileConfig>();
        if (!pretranslate)
        {
            List<DataFile> targetFiles = await UploadFilesAsync(
                filesToAdd,
                FileFormat.Text,
                targetLanguage,
                isTarget: true
            );
            foreach (var item in targetFiles.Select((file, i) => new { i, file }))
            {
                targetFileConfig.Add(
                    new TranslationCorpusFileConfig { FileId = item.file.Id, TextId = filesToAdd[item.i] }
                );
            }
        }

        var sourceFileConfig = new List<TranslationCorpusFileConfig>();

        for (int i = 0; i < sourceFiles.Count; i++)
        {
            sourceFileConfig.Add(
                new TranslationCorpusFileConfig { FileId = sourceFiles[i].Id, TextId = filesToAdd[i] }
            );
        }

        TranslationCorpus response = await TranslationEnginesClient.AddCorpusAsync(
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

    public async Task<TranslationParallelCorpusConfig> MakeParallelTextCorpus(
        string[] filesToAdd,
        string sourceLanguage,
        string targetLanguage,
        bool pretranslate
    )
    {
        List<DataFile> sourceFiles = await UploadFilesAsync(
            filesToAdd,
            FileFormat.Text,
            sourceLanguage,
            isTarget: false
        );

        var targetFileConfig = new List<CorpusFileConfig>();
        if (!pretranslate)
        {
            List<DataFile> targetFiles = await UploadFilesAsync(
                filesToAdd,
                FileFormat.Text,
                targetLanguage,
                isTarget: true
            );
            foreach (var item in targetFiles.Select((file, i) => new { i, file }))
            {
                targetFileConfig.Add(new CorpusFileConfig { FileId = item.file.Id, TextId = filesToAdd[item.i] });
            }
        }

        CorpusConfig targetCorpusConfig =
            new()
            {
                Name = "None",
                Language = targetLanguage,
                Files = targetFileConfig
            };

        var targetCorpus = await CorporaClient.CreateAsync(targetCorpusConfig);

        var sourceFileConfig = new List<CorpusFileConfig>();

        for (int i = 0; i < sourceFiles.Count; i++)
        {
            sourceFileConfig.Add(new CorpusFileConfig { FileId = sourceFiles[i].Id, TextId = filesToAdd[i] });
        }

        CorpusConfig sourceCorpusConfig =
            new()
            {
                Name = "None",
                Language = sourceLanguage,
                Files = sourceFileConfig
            };

        var sourceCorpus = await CorporaClient.CreateAsync(sourceCorpusConfig);

        TranslationParallelCorpusConfig parallelCorpusConfig =
            new() { SourceCorpusIds = { sourceCorpus.Id }, TargetCorpusIds = { targetCorpus.Id } };

        return parallelCorpusConfig;
    }

    public async Task<string> AddParallelTextCorpusToEngineAsync(
        string engineId,
        TranslationParallelCorpusConfig parallelCorpusConfig,
        bool pretranslate
    )
    {
        var parallelCorpus = await TranslationEnginesClient.AddParallelCorpusAsync(engineId, parallelCorpusConfig);

        if (pretranslate)
        {
            TranslationBuildConfig.Pretranslate!.Add(
                new PretranslateCorpusConfig { ParallelCorpusId = parallelCorpus.Id }
            );
        }

        return parallelCorpus.Id;
    }

    public async Task<List<DataFile>> UploadFilesAsync(
        IEnumerable<string> filesToAdd,
        FileFormat fileFormat,
        string language,
        bool isTarget
    )
    {
        string languageFolder = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../..", "data", language)
        );
        if (!Directory.Exists(languageFolder))
            throw new ArgumentException($"The language data directory {languageFolder} does not exist!");
        // Collect files for the corpus
        string[] files = Directory.GetFiles(languageFolder);
        if (files.Length == 0)
            throw new ArgumentException($"The language data directory {languageFolder} contains no files!");
        var fileList = new List<DataFile>();
        IList<DataFile> allFiles = await DataFilesClient.GetAllAsync();
        ILookup<string, string> filenameToId = allFiles
            .Where(f => f.Name is not null)
            .ToLookup(file => file.Name!, file => file.Id);

        foreach (string fileName in filesToAdd)
        {
            string fullName = _prefix + language + "_" + fileName + (isTarget ? "_trg" : "_src");

            //delete files that have the name name
            if (filenameToId.Contains(fullName))
            {
                IEnumerable<string> matchedFiles = filenameToId[fullName];
                foreach (string fileId in matchedFiles)
                {
                    await DataFilesClient.DeleteAsync(fileId);
                }
            }

            //add the new files
            string filePath = Path.GetFullPath(Path.Combine(languageFolder, fileName));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The corpus file {filePath} does not exist!");
            DataFile response = await DataFilesClient.CreateAsync(
                file: new FileParameter(data: File.OpenRead(filePath), fileName: fileName),
                format: fileFormat,
                name: fullName
            );
            fileList.Add(response);
        }
        return fileList;
    }

    private static async Task<string> GetAuth0AuthenticationAsync(
        string authUrl,
        string audience,
        string clientId,
        string clientSecret
    )
    {
        var authHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
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
        HttpResponseMessage response = await authHttpClient.SendAsync(request);
        if (response.Content is null)
            throw new HttpRequestException("Error getting auth0 Authentication.");
        Dictionary<string, object?>? dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(
            await response.Content.ReadAsStringAsync()
        );
        return dict?["access_token"]?.ToString() ?? "";
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

    public async ValueTask TearDown()
    {
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
            await ClearEnginesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
        return new ValueTask(Task.CompletedTask);
    }
}

#pragma warning restore CS0612 // Type or member is obsolete
