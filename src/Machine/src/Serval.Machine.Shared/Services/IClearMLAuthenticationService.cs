namespace Serval.Machine.Shared.Services;

public interface IClearMLAuthenticationService : IHostedService
{
    public Task<string> GetAuthTokenAsync(CancellationToken cancellationToken = default);
}
