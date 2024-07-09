using SIL.ObjectModel;

namespace Serval.Machine.Shared.Utils;

public class AsyncDisposableBase : DisposableBase, IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();

        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual ValueTask DisposeAsyncCore()
    {
        return default;
    }
}
