using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

internal interface IRequestTelemetryService
{
    event Action<RequestMetric>? MetricAdded;

    IReadOnlyCollection<RequestMetric> History { get; }

    void Add(RequestMetric metric);
}
