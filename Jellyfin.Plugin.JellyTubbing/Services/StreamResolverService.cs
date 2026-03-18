using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTubbing.Services;

/// <summary>
/// Resolves a YouTube video ID to a direct stream URL via yt-dlp.
/// Used by the JellyTubbing channel for on-demand trending video playback.
/// </summary>
public class StreamResolverService
{
    private readonly ILogger<StreamResolverService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamResolverService"/> class.
    /// </summary>
    public StreamResolverService(ILogger<StreamResolverService> logger)
    {
        _logger = logger;
    }

    /// <summary>Resolves a YouTube video ID to a <see cref="MediaSourceInfo"/> via yt-dlp.</summary>
    public async Task<IEnumerable<MediaSourceInfo>> ResolveAsync(string videoId, CancellationToken ct)
    {
        var streamUrl = await ResolveViaYtDlpAsync(videoId, ct);
        if (!string.IsNullOrEmpty(streamUrl))
        {
            _logger.LogInformation("Resolved stream for {VideoId} via yt-dlp.", videoId);
            return new[] { BuildSource(videoId, streamUrl) };
        }

        _logger.LogWarning("Could not resolve stream for {VideoId}.", videoId);
        return Array.Empty<MediaSourceInfo>();
    }

    /// <summary>Resolves a YouTube video URL to a direct stream URL string via yt-dlp -g.</summary>
    public async Task<string?> ResolveUrlAsync(string videoId, CancellationToken ct)
        => await ResolveViaYtDlpAsync(videoId, ct);

    // -------------------------------------------------------------------------

    private async Task<string?> ResolveViaYtDlpAsync(string videoId, CancellationToken ct)
    {
        var config = Plugin.Instance!.Configuration;
        var binary = string.IsNullOrWhiteSpace(config.YtDlpBinaryPath) ? "yt-dlp" : config.YtDlpBinaryPath;
        var height = ParseHeight(config.PreferredQuality ?? "720p");
        var format = $"best[height<={height}][ext=mp4]/best[height<={height}]/best[ext=mp4]/best";
        var ytUrl  = $"https://www.youtube.com/watch?v={videoId}";

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = binary,
                    Arguments              = $"-g --format \"{format}\" -- {ytUrl}",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };
            proc.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
            await proc.WaitForExitAsync(cts.Token);

            var url = output.Split('\n')[0].Trim();
            return string.IsNullOrEmpty(url) ? null : url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "yt-dlp stream resolution failed for {VideoId}", videoId);
            return null;
        }
    }

    private static MediaSourceInfo BuildSource(string videoId, string url) => new()
    {
        Id                   = videoId,
        Name                 = "yt-dlp",
        Path                 = url,
        Protocol             = MediaProtocol.Http,
        IsRemote             = true,
        Container            = "mp4",
        SupportsDirectPlay   = true,
        SupportsDirectStream = true,
        SupportsTranscoding  = true,
        RequiresOpening      = false,
        RequiresClosing      = false,
        IsInfiniteStream     = false,
        Type                 = MediaSourceType.Default,
    };

    private static int ParseHeight(string q) =>
        int.TryParse(q.TrimEnd('p', 'P'), out var h) ? h : 720;
}
