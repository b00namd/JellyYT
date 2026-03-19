using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTubbing.Services;

/// <summary>
/// Resolves a YouTube video ID to direct stream URL(s) via yt-dlp.
/// For 1080p+, yt-dlp returns separate video and audio URLs (DASH).
/// Resolved URLs are cached for 4 hours so repeated playback starts instantly.
/// </summary>
public class StreamResolverService
{
    // YouTube CDN URLs are valid for ~6 hours; cache for 4 h to be safe
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(4);

    // Value: (videoUrl, audioUrl or null for combined streams, expiry)
    private static readonly ConcurrentDictionary<string, (string Video, string? Audio, DateTime Expiry)> _cache = new();

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
        var (videoUrl, _) = await ResolveUrlsAsync(videoId, ct);
        if (!string.IsNullOrEmpty(videoUrl))
        {
            _logger.LogInformation("Resolved stream for {VideoId}.", videoId);
            return new[] { BuildSource(videoId, videoUrl) };
        }

        _logger.LogWarning("Could not resolve stream for {VideoId}.", videoId);
        return Array.Empty<MediaSourceInfo>();
    }

    /// <summary>
    /// Returns cached or freshly resolved stream URLs.
    /// For combined streams: Audio is null. For DASH (1080p+): both Video and Audio are set.
    /// </summary>
    public async Task<(string? Video, string? Audio)> ResolveUrlsAsync(string videoId, CancellationToken ct)
    {
        if (_cache.TryGetValue(videoId, out var cached) && DateTime.UtcNow < cached.Expiry)
        {
            _logger.LogDebug("Cache hit for {VideoId}.", videoId);
            return (cached.Video, cached.Audio);
        }

        var (video, audio) = await ResolveViaYtDlpAsync(videoId, ct);

        if (!string.IsNullOrEmpty(video))
        {
            _cache[videoId] = (video, audio, DateTime.UtcNow.Add(CacheTtl));
            PruneCache();
        }

        return (video, audio);
    }

    // -------------------------------------------------------------------------

    private async Task<(string? Video, string? Audio)> ResolveViaYtDlpAsync(string videoId, CancellationToken ct)
    {
        var config = Plugin.Instance!.Configuration;
        var binary = string.IsNullOrWhiteSpace(config.YtDlpBinaryPath) ? "yt-dlp" : config.YtDlpBinaryPath;
        var height = ParseHeight(config.PreferredQuality ?? "720p");

        // For ≤720p: use combined (muxed) streams – no ffmpeg needed, seeking works natively.
        // For 1080p+: request DASH (separate video+audio) – yt-dlp prints two URLs,
        //             HlsTranscodeService merges them with ffmpeg for seeking support.
        string format;
        if (height <= 720)
        {
            format = $"best[height<={height}][vcodec!=none][acodec!=none][ext=mp4]" +
                     $"/best[height<={height}][vcodec!=none][acodec!=none]" +
                     $"/best[vcodec!=none][acodec!=none]" +
                     $"/best";
        }
        else
        {
            format = $"bestvideo[height<={height}][ext=mp4]+bestaudio[ext=m4a]" +
                     $"/bestvideo[height<={height}]+bestaudio" +
                     $"/best[height<={height}][vcodec!=none][acodec!=none]" +
                     $"/best";
        }

        var ytUrl = $"https://www.youtube.com/watch?v={videoId}";

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName  = binary,
                    Arguments = $"-g --no-warnings --no-playlist --format \"{format}\" -- {ytUrl}",
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

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0) return (null, null);

            // Two lines = DASH (video URL + audio URL), one line = combined
            return lines.Length >= 2
                ? (lines[0], lines[1])
                : (lines[0], null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "yt-dlp stream resolution failed for {VideoId}", videoId);
            return (null, null);
        }
    }

    private static void PruneCache()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _cache.Keys)
        {
            if (_cache.TryGetValue(key, out var entry) && now >= entry.Expiry)
                _cache.TryRemove(key, out _);
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
