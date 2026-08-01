namespace MoE.ModelManager.Services;

internal interface IApiProxyService : IAsyncDisposable
{
    string BaseUrl { get; }

    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
