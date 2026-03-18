using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyTubbing.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTubbing.Services;

/// <summary>
/// Handles Google OAuth2 authorization code flow for YouTube read-only access.
/// </summary>
public class OAuthService
{
    private const string AuthEndpoint  = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string Scope         = "https://www.googleapis.com/auth/youtube.readonly";

    private readonly IHttpClientFactory _http;
    private readonly ILogger<OAuthService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthService"/> class.
    /// </summary>
    public OAuthService(IHttpClientFactory http, ILogger<OAuthService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    /// <summary>Returns true when a (possibly refreshable) token is stored.</summary>
    public bool IsAuthorized =>
        !string.IsNullOrEmpty(Plugin.Instance?.Configuration.OAuthAccessToken) ||
        !string.IsNullOrEmpty(Plugin.Instance?.Configuration.OAuthRefreshToken);

    /// <summary>Builds the Google authorization URL to redirect the user to.</summary>
    public string GetAuthorizationUrl(string redirectUri)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.OAuthClientId))
            return string.Empty;

        return $"{AuthEndpoint}" +
               $"?client_id={Uri.EscapeDataString(config.OAuthClientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&response_type=code" +
               $"&scope={Uri.EscapeDataString(Scope)}" +
               $"&access_type=offline" +
               $"&prompt=consent";
    }

    /// <summary>Exchanges an authorization code for access + refresh tokens and stores them.</summary>
    public async Task<bool> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null) return false;

        try
        {
            var client = _http.CreateClient("jellytubbing");
            var resp = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["code"]          = code,
                    ["client_id"]     = config.OAuthClientId,
                    ["client_secret"] = config.OAuthClientSecret,
                    ["redirect_uri"]  = redirectUri,
                    ["grant_type"]    = "authorization_code",
                }), ct);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("OAuth code exchange failed ({Status}): {Body}", resp.StatusCode, body);
                return false;
            }

            var token = await resp.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: ct);
            if (token is null) return false;

            StoreTokens(config, token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OAuth code exchange exception");
            return false;
        }
    }

    /// <summary>Returns a valid access token, refreshing if necessary. Returns null if not authorized.</summary>
    public async Task<string?> GetValidAccessTokenAsync(CancellationToken ct)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrEmpty(config.OAuthRefreshToken))
            return null;

        // Still valid?
        if (!string.IsNullOrEmpty(config.OAuthAccessToken) &&
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() < config.OAuthTokenExpiryUnix)
            return config.OAuthAccessToken;

        return await RefreshAsync(config, ct);
    }

    /// <summary>Clears all stored OAuth tokens.</summary>
    public void Revoke()
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null) return;
        config.OAuthAccessToken    = string.Empty;
        config.OAuthRefreshToken   = string.Empty;
        config.OAuthTokenExpiryUnix = 0;
        Plugin.Instance!.SaveConfiguration();
    }

    // -------------------------------------------------------------------------

    private async Task<string?> RefreshAsync(Configuration.PluginConfiguration config, CancellationToken ct)
    {
        try
        {
            var client = _http.CreateClient("jellytubbing");
            var resp = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["refresh_token"] = config.OAuthRefreshToken,
                    ["client_id"]     = config.OAuthClientId,
                    ["client_secret"] = config.OAuthClientSecret,
                    ["grant_type"]    = "refresh_token",
                }), ct);

            if (!resp.IsSuccessStatusCode) return null;

            var token = await resp.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: ct);
            if (token is null) return null;

            StoreTokens(config, token);
            return token.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OAuth token refresh failed");
            return null;
        }
    }

    private static void StoreTokens(Configuration.PluginConfiguration config, OAuthTokenResponse token)
    {
        config.OAuthAccessToken     = token.AccessToken;
        if (!string.IsNullOrEmpty(token.RefreshToken))
            config.OAuthRefreshToken = token.RefreshToken;
        config.OAuthTokenExpiryUnix = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - 60).ToUnixTimeSeconds();
        Plugin.Instance!.SaveConfiguration();
    }
}
