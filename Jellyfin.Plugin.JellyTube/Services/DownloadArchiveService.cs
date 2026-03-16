using System;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTube.Services;

/// <summary>
/// Manages the yt-dlp download archive file.
/// Video IDs written here are skipped by yt-dlp on future scheduled downloads.
/// </summary>
public class DownloadArchiveService
{
    private readonly string _archivePath;
    private readonly HashSet<string> _videoIds;
    private readonly object _lock = new();
    private readonly ILogger<DownloadArchiveService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadArchiveService"/> class.
    /// </summary>
    public DownloadArchiveService(IApplicationPaths appPaths, ILogger<DownloadArchiveService> logger)
    {
        _logger = logger;
        _archivePath = Path.Combine(appPaths.PluginConfigurationsPath, "jellytube-archive.txt");
        _videoIds = Load();
    }

    /// <summary>Gets the path to the archive file passed to yt-dlp via --download-archive.</summary>
    public string ArchivePath => _archivePath;

    /// <summary>Returns true if the given YouTube video ID is already in the archive.</summary>
    public bool Contains(string videoId)
    {
        lock (_lock)
            return _videoIds.Contains(videoId);
    }

    /// <summary>
    /// Adds a YouTube video ID to the archive so yt-dlp will skip it in future downloads.
    /// </summary>
    public void Add(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
            return;

        lock (_lock)
        {
            if (_videoIds.Add(videoId))
            {
                File.AppendAllText(_archivePath, $"youtube {videoId}{Environment.NewLine}");
                _logger.LogInformation("Added {VideoId} to download archive.", videoId);
            }
        }
    }

    /// <summary>
    /// Clears the archive file and the in-memory set so all videos can be re-downloaded.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _videoIds.Clear();
            if (File.Exists(_archivePath))
                File.Delete(_archivePath);
            _logger.LogInformation("Download archive cleared.");
        }
    }

    private HashSet<string> Load()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(_archivePath))
            return ids;

        foreach (var line in File.ReadAllLines(_archivePath))
        {
            var parts = line.Trim().Split(' ');
            if (parts.Length >= 2)
                ids.Add(parts[1]);
        }

        _logger.LogInformation("Loaded {Count} entries from download archive.", ids.Count);
        return ids;
    }
}
