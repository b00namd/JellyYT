using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyTubbing.Services;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTubbing.Channel;

/// <summary>
/// Jellyfin channel that streams YouTube videos via Invidious (with yt-dlp fallback).
/// </summary>
public class JellyTubbingChannel : IChannel
{
    private readonly InvidiousService _invidious;
    private readonly StreamResolverService _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyTubbingChannel"/> class.
    /// </summary>
    public JellyTubbingChannel(InvidiousService invidious, StreamResolverService resolver)
    {
        _invidious = invidious;
        _resolver = resolver;
    }

    /// <inheritdoc />
    public string Name => "JellyTubbing";

    /// <inheritdoc />
    public string Description => "YouTube-Videos direkt in Jellyfin streamen.";

    /// <inheritdoc />
    public string DataVersion => "1";

    /// <inheritdoc />
    public string HomePageUrl => string.Empty;

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    /// <inheritdoc />
    public InternalChannelFeatures GetChannelFeatures() => new()
    {
        ContentTypes = new List<ChannelMediaContentType> { ChannelMediaContentType.Movie },
        MediaTypes   = new List<ChannelMediaType> { ChannelMediaType.Video },
        SupportsContentDownloading = false,
    };

    /// <inheritdoc />
    public bool IsEnabledFor(string userId) => true;

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedChannelImages() => Array.Empty<ImageType>();

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        => Task.FromResult(new DynamicImageResponse { HasImage = false });

    /// <inheritdoc />
    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken ct)
    {
        // Root level → single "Trending" folder
        if (string.IsNullOrEmpty(query.FolderId))
        {
            return new ChannelItemResult
            {
                Items = new[]
                {
                    new ChannelItemInfo
                    {
                        Id   = "trending",
                        Name = "Trending",
                        Type = ChannelItemType.Folder,
                    }
                },
                TotalRecordCount = 1
            };
        }

        if (query.FolderId == "trending")
        {
            var items = await _invidious.GetTrendingAsync(ct);
            return new ChannelItemResult { Items = items, TotalRecordCount = items.Count };
        }

        return new ChannelItemResult { Items = Array.Empty<ChannelItemInfo>(), TotalRecordCount = 0 };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id, CancellationToken ct)
    {
        // Strip "YT:" prefix added when building channel items
        var videoId = id.StartsWith("YT:", StringComparison.Ordinal) ? id[3..] : id;
        return await _resolver.ResolveAsync(videoId, ct);
    }
}
