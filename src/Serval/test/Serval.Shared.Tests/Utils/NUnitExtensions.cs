namespace Serval.Shared.Utils;

public static class NUnitExtensions
{
    public static EqualUsingConstraint<string> IgnoreLineEndings(this EqualStringConstraint constraint)
    {
        return constraint.Using(new IgnoreLineEndingsStringComparer());
    }
}
