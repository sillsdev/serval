namespace Serval.E2ETests;

#pragma warning disable CS0612 // Type or member is obsolete

public enum EngineGroup
{
    Translation,
    WordAlignment
}

public record Build
{
    public string Id { get; set; }
    public int Revision { get; set; }
    public JobState State { get; set; }

    public Build(TranslationBuild translationBuild)
    {
        Id = translationBuild.Id;
        Revision = translationBuild.Revision;
        State = translationBuild.State;
    }

    public Build(WordAlignmentBuild wordAlignmentBuild)
    {
        Id = wordAlignmentBuild.Id;
        Revision = wordAlignmentBuild.Revision;
        State = wordAlignmentBuild.State;
    }
}

public record ParallelCorpus
{
    public string Id { get; set; }
    public string Url { get; set; }
    public ResourceLink Engine { get; set; }
    public IList<ResourceLink> SourceCorpora { get; set; }
    public IList<ResourceLink> TargetCorpora { get; set; }

    public ParallelCorpus(TranslationParallelCorpus translationParallelCorpus)
    {
        Id = translationParallelCorpus.Id;
        Url = translationParallelCorpus.Url;
        Engine = translationParallelCorpus.Engine;
        SourceCorpora = translationParallelCorpus.SourceCorpora;
        TargetCorpora = translationParallelCorpus.TargetCorpora;
    }

    public ParallelCorpus(WordAlignmentParallelCorpus wordAlignmentParallelCorpus)
    {
        Id = wordAlignmentParallelCorpus.Id;
        Url = wordAlignmentParallelCorpus.Url;
        Engine = wordAlignmentParallelCorpus.Engine;
        SourceCorpora = wordAlignmentParallelCorpus.SourceCorpora;
        TargetCorpora = wordAlignmentParallelCorpus.TargetCorpora;
    }
}

public record ParallelCorpusConfig
{
    public string? Name { get; set; }
    public IList<string> SourceCorpusIds { get; set; }
    public IList<string> TargetCorpusIds { get; set; }

    public TranslationParallelCorpusConfig ToTranslationParallelCorpusConfig()
    {
        return new TranslationParallelCorpusConfig
        {
            Name = Name,
            SourceCorpusIds = SourceCorpusIds,
            TargetCorpusIds = TargetCorpusIds
        };
    }

    public WordAlignmentParallelCorpusConfig ToWordAlignmentParallelCorpusConfig()
    {
        return new WordAlignmentParallelCorpusConfig
        {
            Name = Name,
            SourceCorpusIds = SourceCorpusIds,
            TargetCorpusIds = TargetCorpusIds
        };
    }

    public ParallelCorpusConfig(TranslationParallelCorpusConfig translationParallelCorpusConfig)
    {
        Name = translationParallelCorpusConfig.Name;
        SourceCorpusIds = translationParallelCorpusConfig.SourceCorpusIds;
        TargetCorpusIds = translationParallelCorpusConfig.TargetCorpusIds;
    }

    public ParallelCorpusConfig(WordAlignmentParallelCorpusConfig wordAlignmentParallelCorpusConfig)
    {
        Name = wordAlignmentParallelCorpusConfig.Name;
        SourceCorpusIds = wordAlignmentParallelCorpusConfig.SourceCorpusIds;
        TargetCorpusIds = wordAlignmentParallelCorpusConfig.TargetCorpusIds;
    }
}

public class ServalClientHelper : IAsyncDisposable
{
    public DataFilesClient DataFilesClient { get; }
    public CorporaClient CorporaClient { get; }
    public TranslationEnginesClient TranslationEnginesClient { get; }
    public WordAlignmentEnginesClient WordAlignmentEnginesClient { get; }
    public TranslationEngineTypesClient TranslationEngineTypesClient { get; }

    public TranslationBuildConfig TranslationBuildConfig { get; set; }
    public WordAlignmentBuildConfig WordAlignmentBuildConfig { get; set; }

    private IDictionary<string, EngineGroup> EngineIdToEngineGroup { get; } = new Dictionary<string, EngineGroup>();
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
        WordAlignmentEnginesClient = new WordAlignmentEnginesClient(_httpClient);
        WordAlignmentBuildConfig = InitWordAlignmentBuildConfig();
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
        await ClearTestDataAsync();
    }

    public void Setup()
    {
        InitTranslationBuildConfig();
        InitWordAlignmentBuildConfig();
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

    public WordAlignmentBuildConfig InitWordAlignmentBuildConfig()
    {
        WordAlignmentBuildConfig = new WordAlignmentBuildConfig
        {
            WordAlignOn = [],
            TrainOn = null,
            Options = null
        };
        return WordAlignmentBuildConfig;
    }

    public async Task ClearTestDataAsync()
    {
        IList<TranslationEngine> existingTranslationEngines = await TranslationEnginesClient.GetAllAsync();
        foreach (TranslationEngine translationEngine in existingTranslationEngines)
        {
            if (translationEngine.Name?.Contains(_prefix) ?? false)
                await TranslationEnginesClient.DeleteAsync(translationEngine.Id);
        }

        IList<WordAlignmentEngine> existingWordAlignmentEngines = await WordAlignmentEnginesClient.GetAllAsync();
        foreach (WordAlignmentEngine wordAlignmentEngine in existingWordAlignmentEngines)
        {
            if (wordAlignmentEngine.Name?.Contains(_prefix) ?? false)
                await WordAlignmentEnginesClient.DeleteAsync(wordAlignmentEngine.Id);
        }

        IList<Corpus> existingCorpora = await CorporaClient.GetAllAsync();
        foreach (Corpus corpus in existingCorpora)
        {
            if (corpus.Name?.Contains(_prefix) ?? false)
                await CorporaClient.DeleteAsync(corpus.Id);
        }

        IList<DataFile> existingDataFiles = await DataFilesClient.GetAllAsync();
        foreach (DataFile dataFile in existingDataFiles)
        {
            if (dataFile.Name?.Contains(_prefix) ?? false)
                await DataFilesClient.DeleteAsync(dataFile.Id);
        }
    }

    public async Task<string> CreateNewEngineAsync(
        string engineType,
        string sourceLanguage,
        string targetLanguage,
        string name = "",
        bool? isModelPersisted = null
    )
    {
        EngineGroup engineGroup = GetEngineGroup(engineType);
        if (engineGroup == EngineGroup.Translation)
        {
            TranslationEngine engine = await TranslationEnginesClient.CreateAsync(
                new TranslationEngineConfig
                {
                    Name = _prefix + name,
                    SourceLanguage = sourceLanguage,
                    TargetLanguage = targetLanguage,
                    Type = engineType,
                    IsModelPersisted = isModelPersisted
                }
            );
            EngineIdToEngineGroup[engine.Id] = engineGroup;
            return engine.Id;
        }
        else
        {
            WordAlignmentEngine engine = await WordAlignmentEnginesClient.CreateAsync(
                new WordAlignmentEngineConfig
                {
                    Name = _prefix + name,
                    SourceLanguage = sourceLanguage,
                    TargetLanguage = targetLanguage,
                    Type = engineType,
                }
            );
            EngineIdToEngineGroup[engine.Id] = engineGroup;
            return engine.Id;
        }
    }

    public async Task<TranslationBuild> StartTranslationBuildAsync(string engineId)
    {
        return await TranslationEnginesClient.StartBuildAsync(engineId, TranslationBuildConfig);
    }

    public async Task<string> BuildEngineAsync(string engineId)
    {
        EngineGroup engineGroup = EngineIdToEngineGroup[engineId];
        Build newJob;
        int revision;
        if (engineGroup == EngineGroup.Translation)
        {
            newJob = new Build(await StartTranslationBuildAsync(engineId));
            revision = newJob.Revision;
            await TranslationEnginesClient.GetBuildAsync(engineId, newJob.Id, newJob.Revision);
        }
        else
        {
            newJob = new Build(await WordAlignmentEnginesClient.StartBuildAsync(engineId, WordAlignmentBuildConfig));
            revision = newJob.Revision;
            await WordAlignmentEnginesClient.GetBuildAsync(engineId, newJob.Id, newJob.Revision);
        }
        while (true)
        {
            try
            {
                Build result;
                if (engineGroup == EngineGroup.Translation)
                {
                    result = new Build(await TranslationEnginesClient.GetBuildAsync(engineId, newJob.Id, revision + 1));
                }
                else
                {
                    result = new Build(
                        await WordAlignmentEnginesClient.GetBuildAsync(engineId, newJob.Id, revision + 1)
                    );
                }
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
        EngineGroup engineGroup = EngineIdToEngineGroup[engineId];
        if (engineGroup == EngineGroup.Translation)
        {
            await TranslationEnginesClient.CancelBuildAsync(engineId);
        }
        else
        {
            await WordAlignmentEnginesClient.CancelBuildAsync(engineId);
        }
        int pollIntervalMs = 1000;
        int tries = 1;
        while (true)
        {
            Build build;
            if (engineGroup == EngineGroup.Translation)
            {
                build = new Build(await TranslationEnginesClient.GetBuildAsync(engineId, buildId));
            }
            else
            {
                build = new Build(await WordAlignmentEnginesClient.GetBuildAsync(engineId, buildId));
            }
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
        bool inference
    )
    {
        EngineGroup engineGroup = EngineIdToEngineGroup[engineId];
        if (engineGroup == EngineGroup.WordAlignment)
            throw new ArgumentException("Word alignment engines do not support non-parallel corpora.");

        List<DataFile> sourceFiles = await UploadFilesAsync(
            filesToAdd,
            FileFormat.Text,
            sourceLanguage,
            isTarget: false
        );

        var targetFileConfig = new List<TranslationCorpusFileConfig>();
        if (!inference)
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

        if (sourceLanguage == targetLanguage && !inference)
        {
            // if it's the same language, and we are not pretranslating, do nothing (echo for suggestions)
            // if pretranslating, we need to upload the source separately
            // if different languages, we are not echoing.
        }
        else
        {
            for (int i = 0; i < sourceFiles.Count; i++)
            {
                sourceFileConfig.Add(
                    new TranslationCorpusFileConfig { FileId = sourceFiles[i].Id, TextId = filesToAdd[i] }
                );
            }
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

        if (inference)
        {
            TranslationBuildConfig.Pretranslate!.Add(
                new PretranslateCorpusConfig { CorpusId = response.Id, TextIds = filesToAdd.ToList() }
            );
        }

        return response.Id;
    }

    public async Task<ParallelCorpusConfig> MakeParallelTextCorpus(
        string[] filesToAdd,
        string sourceLanguage,
        string targetLanguage,
        bool inference
    )
    {
        List<DataFile> sourceFiles = await UploadFilesAsync(
            filesToAdd,
            FileFormat.Text,
            sourceLanguage,
            isTarget: false
        );

        var targetFileConfig = new List<CorpusFileConfig>();
        if (!inference)
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
                Name = _prefix + "Target",
                Language = targetLanguage,
                Files = targetFileConfig
            };

        Corpus? targetCorpus =
            targetCorpusConfig.Files.Count > 0
                ? targetCorpus = await CorporaClient.CreateAsync(targetCorpusConfig)
                : null;

        var sourceFileConfig = new List<CorpusFileConfig>();

        if (sourceLanguage == targetLanguage && !inference)
        {
            // if it's the same language, and we are not pretranslating, do nothing (echo for suggestions)
            // if pretranslating, we need to upload the source separately
            // if different languages, we are not echoing.
        }
        else
        {
            for (int i = 0; i < sourceFiles.Count; i++)
            {
                sourceFileConfig.Add(new CorpusFileConfig { FileId = sourceFiles[i].Id, TextId = filesToAdd[i] });
            }
        }

        CorpusConfig sourceCorpusConfig =
            new()
            {
                Name = _prefix + "Source",
                Language = sourceLanguage,
                Files = sourceFileConfig
            };

        var sourceCorpus = await CorporaClient.CreateAsync(sourceCorpusConfig);

        TranslationParallelCorpusConfig parallelCorpusConfig = new() { SourceCorpusIds = { sourceCorpus.Id } };
        if (targetCorpus is not null)
            parallelCorpusConfig.TargetCorpusIds.Add(targetCorpus.Id);

        return new ParallelCorpusConfig(parallelCorpusConfig);
    }

    public async Task<string> AddParallelTextCorpusToEngineAsync(
        string engineId,
        ParallelCorpusConfig parallelCorpusConfig,
        bool inference
    )
    {
        EngineGroup engineGroup = EngineIdToEngineGroup[engineId];
        ParallelCorpus parallelCorpus;
        if (engineGroup == EngineGroup.Translation)
        {
            parallelCorpus = new ParallelCorpus(
                await TranslationEnginesClient.AddParallelCorpusAsync(
                    engineId,
                    parallelCorpusConfig.ToTranslationParallelCorpusConfig()
                )
            );
            if (inference)
            {
                TranslationBuildConfig.Pretranslate!.Add(
                    new PretranslateCorpusConfig { ParallelCorpusId = parallelCorpus.Id }
                );
            }
        }
        else
        {
            parallelCorpus = new ParallelCorpus(
                await WordAlignmentEnginesClient.AddParallelCorpusAsync(
                    engineId,
                    parallelCorpusConfig.ToWordAlignmentParallelCorpusConfig()
                )
            );
            if (inference)
            {
                WordAlignmentBuildConfig.WordAlignOn!.Add(
                    new WordAlignmentCorpusConfig { ParallelCorpusId = parallelCorpus.Id }
                );
            }
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

    public static EngineGroup GetEngineGroup(string engineType)
    {
        return engineType switch
        {
            "SmtTransfer" => EngineGroup.Translation,
            "Nmt" => EngineGroup.Translation,
            "Echo" => EngineGroup.Translation,
            "Statistical" => EngineGroup.WordAlignment,
            "EchoWordAlignment" => EngineGroup.WordAlignment,
            _ => throw new ArgumentOutOfRangeException(engineType, "Unknown engine type")
        };
    }

    public async ValueTask TearDown()
    {
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
            await ClearTestDataAsync();
    }

    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
        return new ValueTask(Task.CompletedTask);
    }
}

#pragma warning restore CS0612 // Type or member is obsolete
