namespace Serval.WordAlignment.Models;

/// <summary>
/// Build options for word alignment model training and generation.
/// </summary>
public class BuildOptions
{
    /// <summary>
    /// The tags to identify the build.
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// Whether to use key terms or not.
    /// </summary>
    public bool? UseKeyTerms { get; set; }

    /// <summary>
    /// Thot alignment configuration.
    /// </summary>
    public ThotAlign? ThotAlign { get; set; }
}

/// <summary>
/// Thot Alignment configuration options.
/// </summary>
public class ThotAlign
{
    /// <summary>
    /// Word alignment heuristic (e.g., "grow-diag-final-and").
    /// </summary>
    public string? WordAlignmentHeuristic { get; set; }

    /// <summary>
    /// Model type (e.g., "hmm").
    /// </summary>
    public string? ModelType { get; set; }

    /// <summary>
    /// Tokenizer configuration settings.
    /// </summary>
    public string? Tokenizer { get; set; }
}
