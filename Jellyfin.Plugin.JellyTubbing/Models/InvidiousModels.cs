using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellyTubbing.Models;

/// <summary>A video item returned by Invidious search or trending endpoints.</summary>
public class InvidiousVideoItem
{
    [JsonPropertyName("videoId")]          public string VideoId { get; set; } = string.Empty;
    [JsonPropertyName("title")]            public string Title { get; set; } = string.Empty;
    [JsonPropertyName("author")]           public string Author { get; set; } = string.Empty;
    [JsonPropertyName("published")]        public long Published { get; set; }
    [JsonPropertyName("lengthSeconds")]    public int LengthSeconds { get; set; }
    [JsonPropertyName("description")]      public string Description { get; set; } = string.Empty;
    [JsonPropertyName("videoThumbnails")] public InvidiousThumbnail[] VideoThumbnails { get; set; } = [];
    [JsonPropertyName("viewCount")]        public long ViewCount { get; set; }
}

/// <summary>A thumbnail entry from Invidious.</summary>
public class InvidiousThumbnail
{
    [JsonPropertyName("quality")] public string Quality { get; set; } = string.Empty;
    [JsonPropertyName("url")]     public string Url { get; set; } = string.Empty;
    [JsonPropertyName("width")]   public int Width { get; set; }
    [JsonPropertyName("height")]  public int Height { get; set; }
}

/// <summary>Detailed video info including progressive stream URLs.</summary>
public class InvidiousVideoDetail : InvidiousVideoItem
{
    [JsonPropertyName("formatStreams")] public InvidiousFormatStream[] FormatStreams { get; set; } = [];
}

/// <summary>A progressive (combined video+audio) stream from Invidious.</summary>
public class InvidiousFormatStream
{
    [JsonPropertyName("url")]          public string Url { get; set; } = string.Empty;
    [JsonPropertyName("qualityLabel")] public string QualityLabel { get; set; } = string.Empty;
    [JsonPropertyName("container")]    public string Container { get; set; } = string.Empty;
    [JsonPropertyName("resolution")]   public string Resolution { get; set; } = string.Empty;
}
