using SIL.ObjectModel;

namespace Serval.Machine.Shared.Services;

public class WordAlignmentEngineState(
    IWordAlignmentModelFactory wordAlignmentModelFactory,
    IOptionsMonitor<WordAlignmentEngineOptions> options,
    string engineId
) : DisposableBase
{
    private readonly IWordAlignmentModelFactory _wordAlignmentModelFactory = wordAlignmentModelFactory;
    private readonly IOptionsMonitor<WordAlignmentEngineOptions> _options = options;
    private readonly AsyncLock _lock = new();

    private IWordAlignmentEngine? _wordAlignmentEngine;

    public string EngineId { get; } = engineId;

    public bool IsUpdated { get; set; }
    public int CurrentBuildRevision { get; set; } = -1;
    public DateTime LastUsedTime { get; private set; } = DateTime.UtcNow;
    public bool IsLoaded => _wordAlignmentEngine != null;

    private string EngineDir => Path.Combine(_options.CurrentValue.EnginesDir, EngineId);

    public void InitNew()
    {
        _wordAlignmentModelFactory.InitNew(EngineDir);
    }

    public async Task<IWordAlignmentEngine> GetEngineAsync(
        int buildRevision,
        CancellationToken cancellationToken = default
    )
    {
        using (await _lock.LockAsync(cancellationToken))
        {
            if (_wordAlignmentEngine is not null && CurrentBuildRevision != -1 && buildRevision != CurrentBuildRevision)
            {
                IsUpdated = false;
                Unload();
            }

            _wordAlignmentEngine ??= _wordAlignmentModelFactory.Create(EngineDir);
            CurrentBuildRevision = buildRevision;
            return _wordAlignmentEngine;
        }
    }

    public void DeleteData()
    {
        Unload();
        _wordAlignmentModelFactory.Cleanup(EngineDir);
    }

    public void Commit(int buildRevision, TimeSpan inactiveTimeout)
    {
        if (_wordAlignmentEngine is null)
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
    }

    public void Touch()
    {
        LastUsedTime = DateTime.UtcNow;
    }

    private void Unload()
    {
        if (_wordAlignmentEngine is null)
            return;

        _wordAlignmentEngine.Dispose();

        _wordAlignmentEngine = null;
        CurrentBuildRevision = -1;
    }

    protected override void DisposeManagedResources()
    {
        Unload();
    }
}
