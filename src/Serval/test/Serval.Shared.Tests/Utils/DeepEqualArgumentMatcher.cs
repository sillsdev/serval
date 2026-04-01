using DeepEqual.Syntax;
using NSubstitute.Core;
using NSubstitute.Core.Arguments;

namespace Serval.Shared.Utils;

public class DeepEqualArgumentMatcher<T>(T value) : IArgumentMatcher<T>, IDescribeNonMatches
{
    public bool IsSatisfiedBy(T? argument) => argument.IsDeepEqual(value);

    public string DescribeFor(object? argument)
    {
        try
        {
            argument.ShouldDeepEqual(value);
            return string.Empty;
        }
        catch (DeepEqualException ex)
        {
            return ex.Message;
        }
    }
}
