namespace Serval.Machine.Shared.Services;

public class WarningsTraceListener(List<string> outputList, string prefix = "") : TraceListener
{
    public override void Write(string? message) { }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            outputList.Add(prefix + message);
    }
}
