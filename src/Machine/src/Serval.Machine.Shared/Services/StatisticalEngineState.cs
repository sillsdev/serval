using SIL.ObjectModel;

namespace Serval.Machine.Shared.Services;

public class StatisticalEngineState(
    IWordAlignmentModelFactory wordAlignmentModelFactory,
    IOptionsMonitor<StatisticalEngineOptions> options,
    string engineId
) : DisposableBase
{
    private readonly IWordAlignmentModelFactory _wordAlignmentModelFactory = wordAlignmentModelFactory;
    private readonly IOptionsMonitor<StatisticalEngineOptions> _options = options;
    private readonly AsyncLock _lock = new();

    private IWordAlignmentModel? _wordAlignmentModel;

    public string EngineId { get; } = engineId;

    public bool IsUpdated { get; set; }
    public bool IsMarkedForDeletion { get; set; }
    public int CurrentBuildRevision { get; set; } = -1;
    public DateTime LastUsedTime { get; private set; } = DateTime.UtcNow;
    public bool IsLoaded => _wordAlignmentModel != null;

    private string EngineDir => Path.Combine(_options.CurrentValue.EnginesDir, EngineId);

    public void InitNew()
    {
        _wordAlignmentModelFactory.InitNew(EngineDir);
    }

    public async Task<IWordAlignmentModel> GetEngineAsync(
        int buildRevision,
        CancellationToken cancellationToken = default
    )
    {
        if (IsMarkedForDeletion)
            throw new InvalidOperationException("Engine is marked for deletion");

        using (await _lock.LockAsync(cancellationToken))
        {
            if (_wordAlignmentModel is not null && CurrentBuildRevision != -1 && buildRevision != CurrentBuildRevision)
            {
                IsUpdated = false;
                Unload();
            }

            if (OperatingSystem.IsWindows())
            {
                string newEngineDir = EngineDir + "-new";
                if (Directory.Exists(newEngineDir))
                {
                    Directory.Delete(EngineDir, true);
                    Directory.Move(newEngineDir, EngineDir);
                }
            }
            _wordAlignmentModel ??= _wordAlignmentModelFactory.Create(EngineDir);
            CurrentBuildRevision = buildRevision;
            return _wordAlignmentModel;
        }
    }

    public void DeleteData()
    {
        Unload();
        _wordAlignmentModelFactory.Cleanup(EngineDir);
        if (OperatingSystem.IsWindows())
            _wordAlignmentModelFactory.Cleanup(EngineDir + "-new");
    }

    public void Commit(int buildRevision, TimeSpan inactiveTimeout)
    {
        if (_wordAlignmentModel is null)
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
        if (_wordAlignmentModel is null)
            return;

        _wordAlignmentModel.Dispose();

        _wordAlignmentModel = null;
        CurrentBuildRevision = -1;
    }

    protected override void DisposeManagedResources()
    {
        Unload();
    }
}
