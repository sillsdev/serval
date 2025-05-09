﻿namespace Serval.Shared.Contracts;

public record WordAlignmentBuildStartedDto
{
    public required ResourceLinkDto Build { get; init; }
    public required ResourceLinkDto Engine { get; init; }
}
