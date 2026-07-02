namespace Serval.Translation.Models;

/// <summary>
/// Build options for model training and generation.
/// </summary>
public class BuildOptions
{
    /// <summary>
    /// Whether to align pre-translations.
    /// </summary>
    public bool? AlignPretranslations { get; set; }

    /// <summary>
    /// The tags to identify the build.
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// Whether to use key terms or not.
    /// </summary>
    public bool? UseKeyTerms { get; set; }

    /// <summary>
    /// Name of the parent model.
    /// </summary>
    public string? ParentModelName { get; set; }

    /// <summary>
    /// Thot MT configuration.
    /// </summary>
    public ThotMt? ThotMt { get; set; }

    /// <summary>
    /// Training parameters configuration.
    /// </summary>
    public TrainParams? TrainParams { get; set; }

    /// <summary>
    /// Generation parameters configuration.
    /// </summary>
    public GenerateParams? GenerateParams { get; set; }

    /// <summary>
    /// Tokenizer configuration settings.
    /// </summary>
    public TokenizerConfig? Tokenizer { get; set; }

    /// <summary>
    /// Attention implementation type (e.g., "sdpa").
    /// </summary>
    public string? AttnImplementation { get; set; }
}

/// <summary>
/// Training parameters configuration.
/// </summary>
public class TrainParams
{
    /// <summary>
    /// Whether to perform training
    /// </summary>
    public bool? DoTrain { get; set; }

    /// <summary>
    /// Optimizer type (e.g., "adamw_torch").
    /// </summary>
    public string? Optim { get; set; }

    /// <summary>
    /// The number of warmup steps.
    /// </summary>
    public int? WarmupSteps { get; set; }

    /// <summary>
    /// Per device training batch size.
    /// </summary>
    public int? PerDeviceTrainBatchSize { get; set; }

    /// <summary>
    /// Gradient accumulation steps.
    /// </summary>
    public int? GradientAccumulationSteps { get; set; }

    /// <summary>
    /// Label smoothing factor.
    /// </summary>
    public double? LabelSmoothingFactor { get; set; }

    /// <summary>
    /// Whether to group by length.
    /// </summary>
    public bool? GroupByLength { get; set; }

    /// <summary>
    /// Whether to enable gradient checkpointing.
    /// </summary>
    public bool? GradientCheckpointing { get; set; }

    /// <summary>
    /// Learning rate scheduler type (e.g., "cosine").
    /// </summary>
    public string? LrSchedulerType { get; set; }

    /// <summary>
    /// Learning rate value.
    /// </summary>
    public double? LearningRate { get; set; }

    /// <summary>
    /// Whether to enable FP16.
    /// </summary>
    public bool? Fp16 { get; set; }

    /// <summary>
    /// Whether to enable TF32.
    /// </summary>
    public bool? Tf32 { get; set; }

    /// <summary>
    /// Save strategy (e.g., "no").
    /// </summary>
    public string? SaveStrategy { get; set; }

    /// <summary>
    /// Maximum training steps.
    /// </summary>
    public int? MaxSteps { get; set; }
}

/// <summary>
/// Generation parameters configuration.
/// </summary>
public class GenerateParams
{
    /// <summary>
    /// The device.
    /// </summary>
    public int? Device { get; set; }

    /// <summary>
    /// Number of beams for beam search.
    /// </summary>
    public int? NumBeams { get; set; } = 2;

    /// <summary>
    /// Batch size.
    /// </summary>
    public int? BatchSize { get; set; }

    /// <summary>
    /// OOM batch size backoff multiplier.
    /// </summary>
    public double? OomBatchSizeBackoffMult { get; set; }
}

/// <summary>
/// Tokenizer configuration settings.
/// </summary>
public class TokenizerConfig
{
    /// <summary>
    /// Whether to add unknown source tokens.
    /// </summary>
    public bool? AddUnkSrcTokens { get; set; }

    /// <summary>
    /// Whether to add unknown target tokens.
    /// </summary>
    public bool? AddUnkTrgTokens { get; set; }
}

/// <summary>
/// Thot Machine Translation configuration options.
/// </summary>
public class ThotMt
{
    /// <summary>
    /// Word alignment model type (e.g., "hmm").
    /// </summary>
    public string? WordAlignmentModelType { get; set; }

    /// <summary>
    /// Tokenizer configuration settings.
    /// </summary>
    public string? Tokenizer { get; set; }
}
