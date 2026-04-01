using NSubstitute.Core.Arguments;

namespace Serval.Shared.Utils;

public static class ArgEx
{
    public static ref T IsEquivalentTo<T>(T value)
    {
        return ref ArgumentMatcher.Enqueue(new DeepEqualArgumentMatcher<T>(value))!;
    }
}
