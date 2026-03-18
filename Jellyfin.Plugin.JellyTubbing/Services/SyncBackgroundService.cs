using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTubbing.Services;

/// <summary>
/// Background service that periodically syncs subscribed YouTube channels to STRM files.
/// </summary>
public class SyncBackgroundService : IHostedService, IDisposable
{
    private readonly YouTubeApiService _youtube;
    private readonly StrmService _strm;
    private readonly ILogger<SyncBackgroundService> _logger;
    private Timer? _timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncBackgroundService"/> class.
    /// </summary>
    public SyncBackgroundService(
        YouTubeApiService youtube,
        StrmService strm,
        ILogger<SyncBackgroundService> logger)
    {
        _youtube = youtube;
        _strm    = strm;
        _logger  = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken ct)
    {
        var intervalHours = Plugin.Instance?.Configuration.SyncIntervalHours ?? 6;
        var interval      = TimeSpan.FromHours(Math.Max(1, intervalHours));

        // Delay first run by 2 minutes to let Jellyfin finish startup
        _timer = new Timer(
            _ => _ = RunSyncAsync(CancellationToken.None),
            null,
            TimeSpan.FromMinutes(2),
            interval);

        _logger.LogInformation("JellyTubbing sync scheduled every {Hours}h.", intervalHours);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken ct)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    /// <summary>Triggers a manual sync immediately.</summary>
    public Task TriggerSyncAsync(CancellationToken ct) => RunSyncAsync(ct);

    // -------------------------------------------------------------------------

    private async Task RunSyncAsync(CancellationToken ct)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || config.SyncedChannelIds.Length == 0)
        {
            _logger.LogDebug("JellyTubbing sync: no channels configured.");
            return;
        }

        _logger.LogInformation("JellyTubbing sync started for {Count} channel(s).", config.SyncedChannelIds.Length);

        // Resolve channel names from subscriptions
        var subs   = await _youtube.GetSubscriptionsAsync(ct);
        var subMap = subs.ToDictionary(
            s => s.Snippet.ResourceId.ChannelId,
            s => s.Snippet.Title);

        foreach (var channelId in config.SyncedChannelIds)
        {
            if (ct.IsCancellationRequested) break;

            var channelName = subMap.TryGetValue(channelId, out var n) ? n : channelId;
            try
            {
                var videos = await _youtube.GetChannelVideosAsync(channelId, config.MaxVideosPerChannel, ct);
                foreach (var (videoId, snippet) in videos)
                {
                    await _strm.CreateVideoFilesAsync(
                        channelName,
                        videoId,
                        snippet.Title,
                        snippet.Description,
                        snippet.PublishedAt,
                        snippet.Thumbnails.BestUrl,
                        ct);
                }

                _logger.LogInformation("Synced {Count} videos for {Name}.", videos.Count, channelName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sync failed for channel {ChannelId}", channelId);
            }
        }

        _logger.LogInformation("JellyTubbing sync finished.");
    }

    /// <inheritdoc />
    public void Dispose() => _timer?.Dispose();
}
