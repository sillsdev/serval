using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serval.Client;

public class ServalClientHelper
{
    public readonly DataFilesClient dataFilesClient;
    public readonly TranslationEnginesClient translationEnginesClient;
    private readonly HttpClient _httpClient;
    readonly Dictionary<string, string> EnginePerUser = new Dictionary<string, string>();
    readonly Dictionary<string, string> CorporaPerName = new Dictionary<string, string>();
    private string _prefix;

    private TranslationBuildConfig translationBuildConfig = new TranslationBuildConfig
    {
        Pretranslate = new List<PretranslateCorpusConfig>()
    };

    public ServalClientHelper(
        string servalUrl,
        string authUrl,
        string audience,
        string clientId,
        string clientSecret,
        string prefix = "SCE_",
        bool ignoreSSLErrors = false
    )
    {
        //setup http client
        if (ignoreSSLErrors)
        {
            var handler = GetHttHandlerToIgnoreSslErrors();
            _httpClient = new HttpClient(handler);
        }
        else
            _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(servalUrl);
        dataFilesClient = new DataFilesClient(_httpClient);
        translationEnginesClient = new TranslationEnginesClient(_httpClient);
        _httpClient.DefaultRequestHeaders.Add(
            "authorization",
            $"Bearer {GetAuth0Authentication(authUrl, audience, clientId, clientSecret).Result}"
        );
        _prefix = prefix;
    }

    public async Task ClearEngines(string name = "")
    {
        IList<TranslationEngine> existingTranslationEngines = await translationEnginesClient.GetAllAsync();
        foreach (var translationEngine in existingTranslationEngines)
        {
            if (translationEngine.Name?.Contains(_prefix + name) ?? false)
            {
                await translationEnginesClient.DeleteAsync(translationEngine.Id);
            }
        }
        translationBuildConfig.Pretranslate = new List<PretranslateCorpusConfig>();
    }

    public async Task<string> CreateNewEngine(
        string engineTypeString,
        string source_language,
        string target_language,
        string name = ""
    )
    {
        var engine = await translationEnginesClient.CreateAsync(
            new TranslationEngineConfig
            {
                Name = _prefix + name,
                SourceLanguage = source_language,
                TargetLanguage = target_language,
                Type = engineTypeString
            }
        );
        EnginePerUser.Add(name, engine.Id);
        return engine.Id;
    }

    public async Task<TranslationBuild> StartBuildAsync(string engineId)
    {
        return await translationEnginesClient.StartBuildAsync(engineId, translationBuildConfig);
    }

    public async Task BuildEngine(string engineId)
    {
        var newJob = await StartBuildAsync(engineId);
        int cRevision = newJob.Revision;
        while (true)
        {
            var result = await translationEnginesClient.GetBuildAsync(engineId, newJob.Id, cRevision);
            if (!(result.State == JobState.Active || result.State == JobState.Pending))
            {
                // build completed
                break;
            }
            // Throttle requests to only 2 x second
            await Task.Delay(500);
            cRevision = result.Revision + 1;
        }
    }

    public async Task<string> PostTextCorpusToEngine(
        string engineId,
        string[] filesToAdd,
        string sourceLanguage,
        string targetLanguage,
        bool pretranslate
    )
    {
        List<DataFile> sourceFiles = await PostFiles(filesToAdd, FileFormat.Text, sourceLanguage);
        var sourceFileConfig = new List<TranslationCorpusFileConfig>();

        for (var i = 0; i < sourceFiles.Count; i++)
        {
            sourceFileConfig.Add(
                new TranslationCorpusFileConfig { FileId = sourceFiles[i].Id, TextId = filesToAdd[i] }
            );
        }

        var targetFileConfig = new List<TranslationCorpusFileConfig>();
        if (!pretranslate)
        {
            var targetFiles = await PostFiles(filesToAdd, FileFormat.Text, targetLanguage);
            foreach (var item in targetFiles.Select((file, i) => new { i, file }))
            {
                targetFileConfig.Add(
                    new TranslationCorpusFileConfig { FileId = item.file.Id, TextId = filesToAdd[item.i] }
                );
            }
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

    public async Task<List<DataFile>> PostFiles(IEnumerable<string> filesToAdd, FileFormat fileFormat, string language)
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
        var allFiles = await dataFilesClient.GetAllAsync();
        ILookup<String, String> filenameToId = allFiles
            .Where(f => f.Name is object) // not null
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
                name: fullName
            );
            fileList.Add(response);
        }
        return fileList;
    }

    private async Task<string> GetAuth0Authentication(
        string authUrl,
        string audience,
        string clientId,
        string clientSecret
    )
    {
        var authHttpClient = new HttpClient();
        authHttpClient.Timeout = TimeSpan.FromSeconds(3);
        var request = new HttpRequestMessage(HttpMethod.Post, authUrl + "/oauth/token");
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "audience", audience },
            }
        );
        var response = authHttpClient.SendAsync(request).Result;
        if (response.Content is null)
            throw new HttpRequestException("Error getting auth0 Authentication.");
        var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(
            await response.Content.ReadAsStringAsync()
        );
        return dict?["access_token"] ?? "";
    }

    private HttpClientHandler GetHttHandlerToIgnoreSslErrors()
    {
        //ignore ssl errors
        HttpClientHandler handler = new HttpClientHandler();
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) =>
        {
            return true;
        };
        return handler;
    }
}
