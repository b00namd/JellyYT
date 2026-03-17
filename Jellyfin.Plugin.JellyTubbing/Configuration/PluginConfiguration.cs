using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyTubbing.Configuration;

/// <summary>
/// Configuration for JellyTubbing plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the Invidious instance base URL (e.g. https://invidious.example.com).</summary>
    public string InvidiousInstanceUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional path to the yt-dlp binary for fallback stream resolution.</summary>
    public string YtDlpBinaryPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the preferred stream quality (360p, 480p, 720p, 1080p).</summary>
    public string PreferredQuality { get; set; } = "720p";

    /// <summary>Gets or sets the ISO 3166-1 alpha-2 region code for trending videos (e.g. DE, US).</summary>
    public string TrendingRegion { get; set; } = "DE";
}
