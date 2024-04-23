namespace Serval.Aqua.Shared.Services;

public interface IAquaAuthService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    Task RefreshAccessTokenAsync(CancellationToken cancellationToken = default);
}
