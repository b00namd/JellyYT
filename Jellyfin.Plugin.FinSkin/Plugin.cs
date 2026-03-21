using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.FinSkin;

/// <summary>
/// FinSkin — community skin manager plugin for Jellyfin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>Singleton instance set during startup.</summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "FinSkin";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("a4df4c37-3ef5-4d16-8b56-2f21cf2b8e3e");

    /// <inheritdoc />
    public override string Description => "Community skin manager for Jellyfin. Load skins from the filesystem or upload your own.";

    /// <summary>Initializes a new instance of <see cref="Plugin"/>.</summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            }
        ];
    }
}
