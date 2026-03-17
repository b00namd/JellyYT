using Jellyfin.Plugin.JellyTubbing.Channel;
using Jellyfin.Plugin.JellyTubbing.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.JellyTubbing;

/// <summary>
/// Registers JellyTubbing services with Jellyfin's DI container.
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient("jellytubbing");
        serviceCollection.AddSingleton<InvidiousService>();
        serviceCollection.AddSingleton<StreamResolverService>();
        serviceCollection.AddSingleton<IChannel, JellyTubbingChannel>();
    }
}
