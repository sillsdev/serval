using SIL.ObjectModel;

namespace Serval.Machine.Shared.Services;

public class SmtTransferEngineState(
    ISmtModelFactory smtModelFactory,
    ITransferEngineFactory transferEngineFactory,
    ITruecaserFactory truecaserFactory,
    IOptionsMonitor<SmtTransferEngineOptions> options,
    string engineId
) : DisposableBase
{
    private readonly ISmtModelFactory _smtModelFactory = smtModelFactory;
    private readonly ITransferEngineFactory _transferEngineFactory = transferEngineFactory;
    private readonly ITruecaserFactory _truecaserFactory = truecaserFactory;
    private readonly IOptionsMonitor<SmtTransferEngineOptions> _options = options;
    private readonly AsyncLock _lock = new();

    private IInteractiveTranslationModel? _smtModel;
    private HybridTranslationEngine? _hybridEngine;

    public string EngineId { get; } = engineId;

    public bool IsUpdated { get; set; }
    public int CurrentBuildRevision { get; set; } = -1;
    public DateTime LastUsedTime { get; private set; } = DateTime.UtcNow;
    public bool IsLoaded => _hybridEngine != null;

    private string EngineDir => Path.Combine(_options.CurrentValue.EnginesDir, EngineId);

    public void InitNew()
    {
        _smtModelFactory.InitNew(EngineDir);
        _transferEngineFactory.InitNew(EngineDir);
    }

    public async Task<HybridTranslationEngine> GetHybridEngineAsync(
        int buildRevision,
        CancellationToken cancellationToken = default
    )
    {
        using (await _lock.LockAsync(cancellationToken))
        {
            if (_hybridEngine is not null && CurrentBuildRevision != -1 && buildRevision != CurrentBuildRevision)
            {
                IsUpdated = false;
                Unload();
            }

            if (_hybridEngine is null)
            {
                LatinWordTokenizer tokenizer = new();
                LatinWordDetokenizer detokenizer = new();
                ITruecaser truecaser = _truecaserFactory.Create(EngineDir);
                _smtModel = _smtModelFactory.Create(EngineDir, tokenizer, detokenizer, truecaser);
                ITranslationEngine? transferEngine = _transferEngineFactory.Create(
                    EngineDir,
                    tokenizer,
                    detokenizer,
                    truecaser
                );
                _hybridEngine = new HybridTranslationEngine(_smtModel, transferEngine)
                {
                    TargetDetokenizer = detokenizer
                };
            }
            CurrentBuildRevision = buildRevision;
            return _hybridEngine;
        }
    }

    public void DeleteData()
    {
        Unload();
        _smtModelFactory.Cleanup(EngineDir);
        _transferEngineFactory.Cleanup(EngineDir);
        _truecaserFactory.Cleanup(EngineDir);
    }

    public void Commit(int buildRevision, TimeSpan inactiveTimeout)
    {
        if (_hybridEngine is null)
            return;

        if (CurrentBuildRevision == -1)
            CurrentBuildRevision = buildRevision;
        if (buildRevision != CurrentBuildRevision)
        {
            Unload();
            CurrentBuildRevision = buildRevision;
        }
        else if (DateTime.UtcNow - LastUsedTime > inactiveTimeout)
        {
            Unload();
        }
        else
        {
            SaveModel();
        }
    }

    public void Touch()
    {
        LastUsedTime = DateTime.UtcNow;
    }

    private void SaveModel()
    {
        if (_smtModel is not null && IsUpdated)
        {
            _smtModel.Save();
            IsUpdated = false;
        }
    }

    private void Unload()
    {
        if (_hybridEngine is null)
            return;

        SaveModel();

        _hybridEngine.Dispose();

        _smtModel = null;
        _hybridEngine = null;
        CurrentBuildRevision = -1;
    }

    protected override void DisposeManagedResources()
    {
        Unload();
    }
}
