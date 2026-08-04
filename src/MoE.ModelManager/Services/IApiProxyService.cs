namespace MoE.ModelManager.Services;

internal interface IApiProxyService : IAsyncDisposable
{
    string BaseUrl { get; }

    string NetworkBaseUrl { get; }

    bool IsNetworkAccessEnabled { get; }

    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task SetNetworkAccessAsync(bool enabled, CancellationToken cancellationToken);
}
