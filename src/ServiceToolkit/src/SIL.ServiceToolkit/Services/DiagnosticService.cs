namespace SIL.ServiceToolkit.Services;

/// <summary>
/// Diagnostic information service.
/// </summary>
public class DiagnosticService(ILogger<DiagnosticService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Diagnostic: {MemoryUsage}", MemoryUsage);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Returns memory usage information in a human-readable format.
    /// </summary>
    private static string MemoryUsage
    {
        get
        {
            GCMemoryInfo info = GC.GetGCMemoryInfo();
            using var proc = Process.GetCurrentProcess();
            return $"Environment Memory Size: {HumanReadableSize(info.TotalAvailableMemoryBytes)} / "
                + $"Private Memory Size: {HumanReadableSize(proc.PrivateMemorySize64)}";
        }
    }

    /// <summary>
    /// Formats a size in bytes as a human-readable string.
    /// </summary>
    /// <param name="size">The size in bytes.</param>
    /// <returns>A human-readable byte string.</returns>
    private static string HumanReadableSize(long size)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
