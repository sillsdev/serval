﻿namespace Serval.Translation.Entities;

public class Corpus : IOwnedEntity
{
    public string Id { get; set; } = default!;
    public int Revision { get; set; } = 1;
    public string Owner { get; set; } = default!;
    public string Name { get; set; } = default!;
    public CorpusType Type { get; set; }
    public FileFormat Format { get; set; }
    public List<CorpusFile> Files { get; set; } = new List<CorpusFile>();
}
