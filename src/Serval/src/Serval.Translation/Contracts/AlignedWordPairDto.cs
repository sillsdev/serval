﻿namespace Serval.Translation.Contracts;

public record AlignedWordPairDto
{
    public required int SourceIndex { get; init; }
    public required int TargetIndex { get; init; }
}
