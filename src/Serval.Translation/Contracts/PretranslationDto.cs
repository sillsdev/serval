﻿namespace Serval.Translation.Contracts;

public class PretranslationDto
{
    public string TextId { get; set; } = default!;
    public string[] Refs { get; set; } = default!;
    public string Translation { get; set; } = default!;
}