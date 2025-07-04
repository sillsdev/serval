﻿namespace Serval.Machine.Shared.Utils;

public static class NUnitExtensions
{
    public static EqualConstraint IgnoreLineEndings(this EqualConstraint constraint)
    {
        return constraint.Using<string>(
            (actual, expected) => actual.ReplaceLineEndings() == expected.ReplaceLineEndings()
        );
    }
}
