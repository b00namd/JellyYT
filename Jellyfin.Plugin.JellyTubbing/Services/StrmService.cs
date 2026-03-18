using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTubbing.Services;

/// <summary>
/// Creates .strm, .nfo and thumbnail files for YouTube videos in the configured output folder.
/// </summary>
public class StrmService
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<StrmService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StrmService"/> class.
    /// </summary>
    public StrmService(IHttpClientFactory http, ILogger<StrmService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    /// <summary>
    /// Creates .strm / .nfo / thumbnail for a single video if they do not already exist.
    /// </summary>
    public async Task CreateVideoFilesAsync(
        string channelName,
        string videoId,
        string title,
        string description,
        string publishedAt,
        string thumbnailUrl,
        CancellationToken ct)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.StrmOutputPath))
        {
            _logger.LogWarning("STRM output path not configured – skipping {VideoId}", videoId);
            return;
        }

        var channelDir = Path.Combine(config.StrmOutputPath, SanitizeName(channelName));
        Directory.CreateDirectory(channelDir);

        var year     = DateTime.TryParse(publishedAt, out var dt) ? dt.Year.ToString() : string.Empty;
        var safeName = SanitizeName(title);
        var baseName = string.IsNullOrEmpty(year) ? safeName : $"{safeName} ({year})";

        // .strm – stream endpoint URL
        var strmPath = Path.Combine(channelDir, baseName + ".strm");
        if (!File.Exists(strmPath))
        {
            var serverUrl = (config.JellyfinServerUrl ?? "http://localhost:8096").TrimEnd('/');
            await File.WriteAllTextAsync(strmPath, $"{serverUrl}/api/jellytubbing/stream/{videoId}", Encoding.UTF8, ct);
            _logger.LogDebug("Created {StrmPath}", strmPath);
        }

        // .nfo – Kodi/Jellyfin metadata
        var nfoPath = Path.Combine(channelDir, baseName + ".nfo");
        if (!File.Exists(nfoPath))
        {
            await File.WriteAllTextAsync(nfoPath, BuildNfo(title, description, publishedAt, videoId), Encoding.UTF8, ct);
        }

        // thumbnail
        if (!string.IsNullOrEmpty(thumbnailUrl))
        {
            var thumbPath = Path.Combine(channelDir, baseName + "-thumb.jpg");
            if (!File.Exists(thumbPath))
            {
                try
                {
                    var client = _http.CreateClient("jellytubbing");
                    var bytes  = await client.GetByteArrayAsync(thumbnailUrl, ct);
                    await File.WriteAllBytesAsync(thumbPath, bytes, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Thumbnail download failed for {VideoId}", videoId);
                }
            }
        }
    }

    // -------------------------------------------------------------------------

    private static string BuildNfo(string title, string description, string publishedAt, string videoId)
    {
        var year = DateTime.TryParse(publishedAt, out var dt) ? dt.Year.ToString() : string.Empty;
        return $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<movie>
  <title>{Xml(title)}</title>
  <year>{year}</year>
  <plot>{Xml(description)}</plot>
  <uniqueid type="youtube">{videoId}</uniqueid>
</movie>
""";
    }

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return sb.ToString().Trim().TrimEnd('.');
    }

    private static string Xml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
