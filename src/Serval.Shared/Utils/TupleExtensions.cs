using System.Diagnostics.CodeAnalysis;

namespace Serval.Shared.Utils;

public static class TupleExtensions
{
    public static bool IsSuccess(
        this ValueTuple<bool, ActionResult?> tuple,
        [MaybeNullWhen(true)] out ActionResult errorResult
    )
    {
        bool success;
        (success, errorResult) = tuple;
        return success;
    }
}
