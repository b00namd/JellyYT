using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyTubbing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.JellyTubbing.Api;

/// <summary>
/// REST API endpoints for the JellyTubbing plugin.
/// </summary>
[ApiController]
[Route("api/jellytubbing")]
[Authorize(Policy = "RequiresElevation")]
public class JellyTubbingController : ControllerBase
{
    private readonly OAuthService _oauth;
    private readonly YouTubeApiService _youtube;
    private readonly StreamResolverService _resolver;
    private readonly SyncBackgroundService _sync;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyTubbingController"/> class.
    /// </summary>
    public JellyTubbingController(
        OAuthService oauth,
        YouTubeApiService youtube,
        StreamResolverService resolver,
        SyncBackgroundService sync)
    {
        _oauth    = oauth;
        _youtube  = youtube;
        _resolver = resolver;
        _sync     = sync;
    }

    // -----------------------------------------------------------------------
    // Config page UI
    // -----------------------------------------------------------------------

    /// <summary>Serves the embedded configuration page JavaScript.</summary>
    [HttpGet("ui")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetUiScript()
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Jellyfin.Plugin.JellyTubbing.Configuration.configPage.js");
        if (stream is null) return NotFound();
        using var reader = new StreamReader(stream);
        return Content(reader.ReadToEnd(), "application/javascript");
    }

    // -----------------------------------------------------------------------
    // yt-dlp check
    // -----------------------------------------------------------------------

    /// <summary>Checks whether yt-dlp is available on the server.</summary>
    [HttpGet("check-tools")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckTools()
    {
        var bin = Plugin.Instance?.Configuration.YtDlpBinaryPath;
        if (string.IsNullOrWhiteSpace(bin)) bin = "yt-dlp";

        var (available, version, error) = await TryGetVersionAsync(bin, "--version");
        return Ok(new { ytDlpAvailable = available, ytDlpVersion = version, ytDlpError = error });
    }

    // -----------------------------------------------------------------------
    // OAuth2
    // -----------------------------------------------------------------------

    /// <summary>Returns the Google OAuth2 authorization URL for the config page popup.</summary>
    [HttpGet("oauth-url")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetOAuthUrl()
    {
        var redirectUri = BuildRedirectUri();
        var url = _oauth.GetAuthorizationUrl(redirectUri);

        if (string.IsNullOrEmpty(url))
            return Ok(new { success = false, message = "OAuth-Client-ID nicht konfiguriert." });

        return Ok(new { success = true, url });
    }

    /// <summary>
    /// Handles the Google OAuth2 callback. Exchanges the authorization code for tokens
    /// and returns an HTML page that closes the popup and notifies the opener.
    /// </summary>
    [HttpGet("oauth-callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> OAuthCallback([FromQuery] string? code, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            return Content(OAuthPopupHtml("Fehler", $"Google-Fehler: {error ?? "kein Code erhalten"}", false), "text/html");
        }

        var redirectUri = BuildRedirectUri();
        var ok = await _oauth.ExchangeCodeAsync(code, redirectUri, ct);

        return Content(ok
            ? OAuthPopupHtml("Verbunden", "Google-Konto erfolgreich verbunden.", true)
            : OAuthPopupHtml("Fehler", "Token-Austausch fehlgeschlagen. Prüfe Client-ID und Secret.", false),
            "text/html");
    }

    /// <summary>Returns the current OAuth authorization status.</summary>
    [HttpGet("oauth-status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetOAuthStatus()
    {
        return Ok(new { authorized = _oauth.IsAuthorized });
    }

    /// <summary>Revokes the stored OAuth tokens.</summary>
    [HttpPost("oauth-revoke")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult RevokeOAuth()
    {
        _oauth.Revoke();
        return Ok(new { success = true });
    }

    // -----------------------------------------------------------------------
    // Subscriptions
    // -----------------------------------------------------------------------

    /// <summary>Returns the user's YouTube subscriptions (requires OAuth).</summary>
    [HttpGet("subscriptions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriptions(CancellationToken ct)
    {
        if (!_oauth.IsAuthorized)
            return Ok(new { success = false, message = "Nicht mit Google verbunden." });

        var subs = await _youtube.GetSubscriptionsAsync(ct);
        var synced = Plugin.Instance?.Configuration.SyncedChannelIds ?? [];

        var result = subs.Select(s => new
        {
            channelId = s.Snippet.ResourceId.ChannelId,
            title     = s.Snippet.Title,
            thumbnail = s.Snippet.Thumbnails.BestUrl,
            synced    = synced.Contains(s.Snippet.ResourceId.ChannelId),
        });

        return Ok(new { success = true, subscriptions = result });
    }

    // -----------------------------------------------------------------------
    // Sync
    // -----------------------------------------------------------------------

    /// <summary>Triggers an immediate sync of all configured channels.</summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult TriggerSync(CancellationToken ct)
    {
        _ = _sync.TriggerSyncAsync(ct);
        return Ok(new { success = true, message = "Synchronisation gestartet." });
    }

    // -----------------------------------------------------------------------
    // Stream redirect (STRM playback)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves a YouTube video ID to a direct stream URL and returns a 302 redirect.
    /// Used by the .strm files generated for subscribed channels.
    /// </summary>
    [HttpGet("stream/{videoId}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StreamVideo(string videoId, CancellationToken ct)
    {
        var url = await _resolver.ResolveUrlAsync(videoId, ct);
        if (string.IsNullOrEmpty(url))
            return NotFound(new { message = $"Stream-URL fuer {videoId} konnte nicht aufgeloest werden." });

        return Redirect(url);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private string BuildRedirectUri()
    {
        var serverUrl = (Plugin.Instance?.Configuration.JellyfinServerUrl ?? "http://localhost:8096").TrimEnd('/');
        return $"{serverUrl}/api/jellytubbing/oauth-callback";
    }

    private static string OAuthPopupHtml(string title, string message, bool success)
    {
        var icon    = success ? "&#10003;" : "&#10007;";
        var cssClass = success ? "ok" : "err";
        var boolJs  = success ? "true" : "false";
        return "<!DOCTYPE html><html lang=\"de\"><head><meta charset=\"utf-8\"/><title>" + title + "</title>" +
               "<style>" +
               "body{font-family:sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;background:#1a1a1a;color:#fff;}" +
               ".box{text-align:center;padding:2em;border:1px solid #333;border-radius:8px;background:#222;}" +
               ".ok{color:#4caf50;font-size:2em;}.err{color:#f44336;font-size:2em;}" +
               "</style></head><body><div class=\"box\">" +
               "<div class=\"" + cssClass + "\">" + icon + "</div>" +
               "<h2>" + title + "</h2><p>" + message + "</p>" +
               "<p style=\"color:#aaa;font-size:0.85em;\">Dieses Fenster kann geschlossen werden.</p>" +
               "</div><script>" +
               "if(window.opener&&typeof window.opener.jt_oauthComplete==='function'){window.opener.jt_oauthComplete(" + boolJs + ");}" +
               "if(" + boolJs + ")setTimeout(function(){window.close();},1500);" +
               "</script></body></html>";
    }

    private static async Task<(bool Available, string? Version, string? Error)> TryGetVersionAsync(string binary, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(binary, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return (false, null, "Process.Start returned null");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var stdout = await proc.StandardOutput.ReadToEndAsync(cts.Token);
            await proc.WaitForExitAsync(cts.Token);
            return (true, stdout.Split('\n')[0].Trim(), null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}
