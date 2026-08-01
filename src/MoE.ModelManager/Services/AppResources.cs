using Microsoft.Windows.ApplicationModel.Resources;

namespace MoE.ModelManager.Services;

internal sealed class AppResources : IAppResources
{
    private readonly ResourceLoader _resourceLoader = new ResourceLoader();

    public string GetString(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        return _resourceLoader.GetString(resourceName);
    }
}
