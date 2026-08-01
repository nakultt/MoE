using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class RequestTelemetryServiceTests
{
    [TestMethod]
    public void AddRaisesEventAndKeepsNewestOneHundredMetrics()
    {
        RequestTelemetryService service = new();
        RequestMetric? observed = null;
        service.MetricAdded += metric => observed = metric;

        for (int index = 0; index < 105; index++)
        {
            service.Add(CreateMetric(index));
        }

        Assert.HasCount(100, service.History);
        Assert.IsNotNull(observed);
        Assert.AreEqual("request-104", observed.Endpoint);
        Assert.AreEqual("request-5", service.History.First().Endpoint);
    }

    private static RequestMetric CreateMetric(int index)
    {
        return new RequestMetric
        {
            Timestamp = DateTimeOffset.UtcNow,
            Source = "Test",
            Endpoint = $"request-{index}",
            Model = "test-model",
            StatusCode = 200,
            PromptTokens = 1,
            CompletionTokens = 1,
            TokensPerSecond = 1,
            DurationMilliseconds = 1,
        };
    }
}
