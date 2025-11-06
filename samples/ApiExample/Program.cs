using System.IO.Compression;
using ApiExample;
using Duende.AccessTokenManagement;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Serval.Client;

// Setup and get the services
ServiceProvider services = SetupServices();
IDataFilesClient dataFilesClient = services.GetService<IDataFilesClient>()!;
ICorporaClient corporaClient = services.GetService<ICorporaClient>()!;
ITranslationEnginesClient translationEnginesClient = services.GetService<ITranslationEnginesClient>()!;

// Trap Ctrl+C cancellation
var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    Console.WriteLine("Cancelling...");
    cancellationTokenSource.Cancel();
    eventArgs.Cancel = true;
};

// Create then tear down a pre-translation (NMT) engine
await CreatePreTranslationEngineAsync(cancellationTokenSource.Token);

// Exit
return;

static ServiceProvider SetupServices()
{
    const string HttpClientName = "serval-api";
    const string TokenClientName = "serval-api-token";

    var configurationBuilder = new ConfigurationBuilder();
    IConfiguration configuration = configurationBuilder
        .AddJsonFile("appsettings.json", false, true)
        .AddUserSecrets<Program>()
        .Build();
    ServalOptions servalOptions = configuration.GetSection("Serval").Get<ServalOptions>()!;

    var services = new ServiceCollection();
    services.AddDistributedMemoryCache();
    services
        .AddClientCredentialsTokenManagement()
        .AddClient(
            TokenClientName,
            client =>
            {
                client.TokenEndpoint = new Uri(servalOptions.TokenUrl, UriKind.Absolute);
                client.ClientId = ClientId.Parse(servalOptions.ClientId);
                client.ClientSecret = ClientSecret.Parse(servalOptions.ClientSecret);
                client.Parameters = new Parameters { { "audience", servalOptions.Audience } };
            }
        );
    services.AddClientCredentialsHttpClient(
        HttpClientName,
        ClientCredentialsClientName.Parse(TokenClientName),
        configureClient: client => client.BaseAddress = new Uri(servalOptions.ApiServer)
    );
    services.AddHttpClient(HttpClientName).SetHandlerLifetime(TimeSpan.FromMinutes(5));
    services.AddSingleton<ITranslationEnginesClient, TranslationEnginesClient>(sp =>
    {
        // Instantiate the translation engines client with the named HTTP client
        IHttpClientFactory? factory = sp.GetService<IHttpClientFactory>();
        HttpClient httpClient = factory!.CreateClient(HttpClientName);
        return new TranslationEnginesClient(httpClient);
    });
    services.AddSingleton<IDataFilesClient, DataFilesClient>(sp =>
    {
        // Instantiate the data files client with the named HTTP client
        IHttpClientFactory? factory = sp.GetService<IHttpClientFactory>();
        HttpClient httpClient = factory!.CreateClient(HttpClientName);
        return new DataFilesClient(httpClient);
    });
    services.AddSingleton<ICorporaClient, CorporaClient>(sp =>
    {
        // Instantiate the corpora client with the named HTTP client
        IHttpClientFactory? factory = sp.GetService<IHttpClientFactory>();
        HttpClient httpClient = factory!.CreateClient(HttpClientName);
        return new CorporaClient(httpClient);
    });
    return services.BuildServiceProvider();
}

async Task CreatePreTranslationEngineAsync(CancellationToken cancellationToken)
{
    string? sourceDataFileId = null;
    string? targetDataFileId = null;
    string? sourceCorpusId = null;
    string? targetCorpusId = null;
    string? parallelCorpusId = null;
    string? translationEngineId = null;

    try
    {
        // 1a. Create the source data file
        Console.WriteLine("Create a source data file");
        const string SourceDirectory = "TEA";
        const string SourceFileName = $"{SourceDirectory}.zip";
        await using (var sourceFileStream = new MemoryStream())
        {
            ZipFile.CreateFromDirectory(Path.Combine("data", SourceDirectory), sourceFileStream);
            sourceFileStream.Seek(0, SeekOrigin.Begin);
            DataFile sourceDataFile = await dataFilesClient.CreateAsync(
                new FileParameter(sourceFileStream, SourceFileName),
                FileFormat.Paratext,
                SourceFileName,
                cancellationToken
            );
            sourceDataFileId = sourceDataFile.Id;
        }

        // 1b. Create the target data file
        Console.WriteLine("Create a target data file");
        const string TargetDirectory = "TMA";
        const string TargetFileName = $"{TargetDirectory}.zip";
        await using (var targetFileStream = new MemoryStream())
        {
            ZipFile.CreateFromDirectory(Path.Combine("data", TargetDirectory), targetFileStream);
            targetFileStream.Seek(0, SeekOrigin.Begin);
            DataFile targetDataFile = await dataFilesClient.CreateAsync(
                new FileParameter(targetFileStream, TargetFileName),
                FileFormat.Paratext,
                TargetFileName,
                cancellationToken
            );
            targetDataFileId = targetDataFile.Id;
        }

        // 2a. Create the source corpus
        // NOTE: The text id for the source and target corpora must match
        Console.WriteLine("Create the source corpus");
        const string SourceLanguageCode = "en";
        var corpusConfig = new CorpusConfig
        {
            Name = "English Source Corpus",
            Files = [new CorpusFileConfig { FileId = sourceDataFileId, TextId = "TestData" }],
            Language = SourceLanguageCode,
        };
        Corpus translationCorpus = await corporaClient.CreateAsync(corpusConfig, cancellationToken);
        sourceCorpusId = translationCorpus.Id;

        // 2b. Create the target corpus
        Console.WriteLine("Create the target corpus");
        const string TargetLanguageCode = "mi";
        corpusConfig = new CorpusConfig
        {
            Name = "Maori Target Corpus",
            Files = [new CorpusFileConfig { FileId = targetDataFileId, TextId = "TestData" }],
            Language = TargetLanguageCode,
        };
        translationCorpus = await corporaClient.CreateAsync(corpusConfig, cancellationToken);
        targetCorpusId = translationCorpus.Id;

        // 3. Create the translation engine
        Console.WriteLine("Create the translation engine");
        var engineConfig = new TranslationEngineConfig
        {
            Name = "Test Engine",
            SourceLanguage = SourceLanguageCode,
            TargetLanguage = TargetLanguageCode,
            Type = "nmt",
        };
        TranslationEngine translationEngine = await translationEnginesClient.CreateAsync(
            engineConfig,
            cancellationToken
        );
        translationEngineId = translationEngine.Id;

        // 4. Create the parallel corpus
        TranslationParallelCorpus parallelCorpus = await translationEnginesClient.AddParallelCorpusAsync(
            translationEngineId,
            new TranslationParallelCorpusConfig
            {
                Name = "Test Parallel Corpus",
                SourceCorpusIds = [sourceCorpusId],
                TargetCorpusIds = [targetCorpusId],
            },
            cancellationToken
        );
        parallelCorpusId = parallelCorpus.Id;

        // 5. Start a build
        Console.WriteLine("Start a build");

        // NOTE: This build is restricted to 20 steps for speed of build
        // The generated translation will be very, very inaccurate.
        JObject options = [];
        options.Add("max_steps", 20);
        options.Add("tags", "api-example");

        // We will train on one book, and translate two books
        var translationBuildConfig = new TranslationBuildConfig
        {
            Name = "Test Build",
            Options = options,
            Pretranslate =
            [
                new PretranslateCorpusConfig
                {
                    ParallelCorpusId = parallelCorpusId,
                    SourceFilters =
                    [
                        new ParallelCorpusFilterConfig { CorpusId = sourceCorpusId, ScriptureRange = "LAO;MAN" },
                    ],
                },
            ],
            TrainOn =
            [
                new TrainingCorpusConfig
                {
                    ParallelCorpusId = parallelCorpusId,
                    SourceFilters =
                    [
                        new ParallelCorpusFilterConfig { CorpusId = sourceCorpusId, ScriptureRange = "PS2" },
                    ],
                    TargetFilters =
                    [
                        new ParallelCorpusFilterConfig { CorpusId = targetCorpusId, ScriptureRange = "PS2" },
                    ],
                },
            ],
        };
        TranslationBuild translationBuild = await translationEnginesClient.StartBuildAsync(
            translationEngineId,
            translationBuildConfig,
            cancellationToken
        );

        // Wait until the build is finished
        (int _, int cursorTop) = Console.GetCursorPosition();
        DateTime timeOut = DateTime.Now.AddMinutes(30);
        while (DateTime.Now < timeOut)
        {
            translationBuild = await translationEnginesClient.GetBuildAsync(
                translationEngineId,
                translationBuild.Id,
                minRevision: null,
                cancellationToken
            );
            if (translationBuild.DateFinished is not null)
                break;

            Console.SetCursorPosition(0, cursorTop);
            Console.WriteLine($"{translationBuild.State}: {(translationBuild.Progress ?? 0) * 100}% completed...   ");

            // Wait 20 seconds
            cancellationToken.WaitHandle.WaitOne(millisecondsTimeout: 20000);
        }

        // Display the pre-translation USFM
        string usfm = await translationEnginesClient.GetPretranslatedUsfmAsync(
            translationEngineId,
            parallelCorpusId,
            textId: "LAO",
            textOrigin: PretranslationUsfmTextOrigin.OnlyPretranslated,
            template: PretranslationUsfmTemplate.Source,
            cancellationToken: cancellationToken
        );
        Console.WriteLine(usfm);

        Console.WriteLine("Done!");
    }
    catch (TaskCanceledException)
    {
        // The process was cancelled via Ctrl+C
    }
    finally
    {
        // Clean up created entities
        if (!string.IsNullOrWhiteSpace(sourceDataFileId))
        {
            Console.WriteLine("Delete the Source Data File");
            await dataFilesClient.DeleteAsync(sourceDataFileId, CancellationToken.None);
        }

        if (!string.IsNullOrWhiteSpace(targetDataFileId))
        {
            Console.WriteLine("Delete the Target Data File");
            await dataFilesClient.DeleteAsync(targetDataFileId, CancellationToken.None);
        }

        if (!string.IsNullOrWhiteSpace(sourceCorpusId))
        {
            Console.WriteLine("Delete the Source Corpus");
            await corporaClient.DeleteAsync(sourceCorpusId, CancellationToken.None);
        }

        if (!string.IsNullOrWhiteSpace(targetCorpusId))
        {
            Console.WriteLine("Delete the Target Corpus");
            await corporaClient.DeleteAsync(targetCorpusId, CancellationToken.None);
        }

        if (!string.IsNullOrWhiteSpace(translationEngineId))
        {
            if (!string.IsNullOrWhiteSpace(parallelCorpusId))
            {
                Console.WriteLine("Delete the Parallel Corpus");
                await translationEnginesClient.DeleteParallelCorpusAsync(
                    translationEngineId,
                    parallelCorpusId,
                    CancellationToken.None
                );
            }

            Console.WriteLine("Cancel the current build");
            try
            {
                await translationEnginesClient.CancelBuildAsync(translationEngineId, CancellationToken.None);
            }
            catch (ServalApiException e) when (e.StatusCode == 204)
            {
                // This is the expected result if there is no active build job.
            }

            Console.WriteLine("Delete the Translation Engine");
            await translationEnginesClient.DeleteAsync(translationEngineId, CancellationToken.None);
        }
    }
}
