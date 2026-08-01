using System.Collections.Concurrent;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal sealed class RequestTelemetryService : IRequestTelemetryService
{
    private const int MaximumHistory = 100;
    private readonly ConcurrentQueue<RequestMetric> _history = new();

    public event Action<RequestMetric>? MetricAdded;

    public IReadOnlyCollection<RequestMetric> History => _history.ToArray();

    public void Add(RequestMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        _history.Enqueue(metric);
        while (_history.Count > MaximumHistory)
        {
            _history.TryDequeue(out _);
        }

        MetricAdded?.Invoke(metric);
    }
}
