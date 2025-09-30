namespace Serval.Machine.Shared.Utils;

/// <summary>
/// This exception is thrown when an engine that is not built is requested to perform an action that requires it being
/// built.
/// </summary>
public class EngineNotBuiltException(string message) : Exception(message) { }
