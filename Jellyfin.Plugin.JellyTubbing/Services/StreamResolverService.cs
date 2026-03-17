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
/// Resolves a YouTube video ID to a direct stream URL.
/// Primary: Invidious progressive stream. Fallback: yt-dlp -g.
/// </summary>
public class StreamResolverService
{
    private readonly InvidiousService _invidious;
    private readonly ILogger<StreamResolverService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamResolverService"/> class.
    /// </summary>
    public StreamResolverService(InvidiousService invidious, ILogger<StreamResolverService> logger)
    {
        _invidious = invidious;
        _logger = logger;
    }

    /// <summary>
    /// Returns one or more <see cref="MediaSourceInfo"/> for the given YouTube video ID.
    /// </summary>
    public async Task<IEnumerable<MediaSourceInfo>> ResolveAsync(string videoId, CancellationToken ct)
    {
        // 1. Try Invidious
        var streamUrl = await _invidious.GetBestStreamUrlAsync(videoId, ct);
        if (!string.IsNullOrEmpty(streamUrl))
        {
            _logger.LogInformation("Resolved stream for {VideoId} via Invidious.", videoId);
            return new[] { BuildSource(videoId, streamUrl, "Invidious") };
        }

        _logger.LogInformation("Invidious failed for {VideoId}, trying yt-dlp.", videoId);

        // 2. yt-dlp fallback
        streamUrl = await ResolveViaYtDlpAsync(videoId, ct);
        if (!string.IsNullOrEmpty(streamUrl))
        {
            _logger.LogInformation("Resolved stream for {VideoId} via yt-dlp.", videoId);
            return new[] { BuildSource(videoId, streamUrl, "yt-dlp") };
        }

        _logger.LogWarning("Could not resolve stream for {VideoId}.", videoId);
        return Array.Empty<MediaSourceInfo>();
    }

    // -------------------------------------------------------------------------

    private async Task<string?> ResolveViaYtDlpAsync(string videoId, CancellationToken ct)
    {
        var config = Plugin.Instance!.Configuration;
        var binary = string.IsNullOrWhiteSpace(config.YtDlpBinaryPath) ? "yt-dlp" : config.YtDlpBinaryPath;
        var height = ParseHeight(config.PreferredQuality ?? "720p");
        var format = $"best[height<={height}][ext=mp4]/best[height<={height}]/best[ext=mp4]/best";
        var ytUrl = $"https://www.youtube.com/watch?v={videoId}";

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
                    CreateNoWindow         = true
                }
            };
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            var url = output.Split('\n')[0].Trim();
            return string.IsNullOrEmpty(url) ? null : url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "yt-dlp stream resolution failed for {VideoId}", videoId);
            return null;
        }
    }

    private static MediaSourceInfo BuildSource(string videoId, string url, string sourceName) =>
        new MediaSourceInfo
        {
            Id                   = $"{videoId}_{sourceName}",
            Name                 = sourceName,
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
