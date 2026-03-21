using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.FinSkin;

/// <summary>Plugin configuration persisted to disk.</summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>File name (without extension) of the currently active skin. Empty = no skin.</summary>
    public string ActiveSkin { get; set; } = string.Empty;
}
