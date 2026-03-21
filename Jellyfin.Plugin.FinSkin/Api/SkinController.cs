using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Branding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FinSkin.Api;

/// <summary>REST endpoints for FinSkin skin management.</summary>
[ApiController]
[Route("Plugins/FinSkin")]
public class SkinController : ControllerBase
{
    private readonly IServerConfigurationManager _config;
    private readonly IApplicationPaths _appPaths;
    private readonly ILogger<SkinController> _logger;

    /// <summary>Directory where skin CSS files are stored.</summary>
    private string SkinsDir => Path.Combine(_appPaths.DataPath, "skins");

    /// <summary>Initializes a new instance of <see cref="SkinController"/>.</summary>
    public SkinController(
        IServerConfigurationManager config,
        IApplicationPaths appPaths,
        ILogger<SkinController> logger)
    {
        _config = config;
        _appPaths = appPaths;
        _logger = logger;
    }

    /// <summary>Returns a list of all available skins.</summary>
    [HttpGet("Skins")]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult<IEnumerable<SkinInfo>> GetSkins()
    {
        Directory.CreateDirectory(SkinsDir);

        var skins = Directory
            .GetFiles(SkinsDir, "*.css")
            .Select(BuildSkinInfo)
            .OrderBy(s => s.Name)
            .ToList();

        return Ok(skins);
    }

    /// <summary>Uploads a new skin CSS file.</summary>
    [HttpPost("Upload")]
    [Authorize(Policy = "RequiresElevation")]
    [RequestSizeLimit(5_242_880)] // 5 MB
    public async Task<ActionResult<SkinInfo>> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file provided.");

        if (!file.FileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .css files are allowed.");

        Directory.CreateDirectory(SkinsDir);

        var safeName = Path.GetFileName(file.FileName);
        var dest = Path.Combine(SkinsDir, safeName);

        await using var stream = System.IO.File.Create(dest);
        await file.CopyToAsync(stream);

        _logger.LogInformation("FinSkin: uploaded skin '{Name}'", safeName);
        return Ok(BuildSkinInfo(dest));
    }

    /// <summary>Activates a skin by updating Jellyfin's Custom CSS setting.</summary>
    [HttpPost("Activate/{skinFileName}")]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult Activate(string skinFileName)
    {
        // Sanitize — only allow safe filenames
        var safe = Path.GetFileNameWithoutExtension(Path.GetFileName(skinFileName));
        var cssPath = Path.Combine(SkinsDir, safe + ".css");

        if (!System.IO.File.Exists(cssPath))
            return NotFound($"Skin '{safe}' not found.");

        var importRule = $"@import url('/Plugins/FinSkin/Skin/{safe}.css');";
        var branding = (BrandingOptions)_config.GetConfiguration("branding");
        branding.CustomCss = importRule;
        _config.SaveConfiguration("branding", branding);

        Plugin.Instance!.Configuration.ActiveSkin = safe;
        Plugin.Instance.SaveConfiguration();

        _logger.LogInformation("FinSkin: activated skin '{Name}'", safe);
        return Ok();
    }

    /// <summary>Deactivates the current skin.</summary>
    [HttpPost("Deactivate")]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult Deactivate()
    {
        var branding = (BrandingOptions)_config.GetConfiguration("branding");
        branding.CustomCss = string.Empty;
        _config.SaveConfiguration("branding", branding);

        Plugin.Instance!.Configuration.ActiveSkin = string.Empty;
        Plugin.Instance.SaveConfiguration();

        _logger.LogInformation("FinSkin: deactivated skin");
        return Ok();
    }

    /// <summary>Deletes a skin from disk.</summary>
    [HttpDelete("Skin/{skinFileName}")]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult DeleteSkin(string skinFileName)
    {
        var safe = Path.GetFileNameWithoutExtension(Path.GetFileName(skinFileName));
        var cssPath = Path.Combine(SkinsDir, safe + ".css");

        if (!System.IO.File.Exists(cssPath))
            return NotFound($"Skin '{safe}' not found.");

        System.IO.File.Delete(cssPath);

        var metaPath = Path.Combine(SkinsDir, safe + ".json");
        if (System.IO.File.Exists(metaPath))
            System.IO.File.Delete(metaPath);

        // Deactivate if it was the active skin
        if (Plugin.Instance?.Configuration.ActiveSkin == safe)
        {
            var branding = (BrandingOptions)_config.GetConfiguration("branding");
            branding.CustomCss = string.Empty;
            _config.SaveConfiguration("branding", branding);
            Plugin.Instance.Configuration.ActiveSkin = string.Empty;
            Plugin.Instance.SaveConfiguration();
        }

        _logger.LogInformation("FinSkin: deleted skin '{Name}'", safe);
        return Ok();
    }

    /// <summary>Serves a skin's CSS file — anonymous so browsers can load it.</summary>
    [HttpGet("Skin/{skinFileName}.css")]
    [AllowAnonymous]
    public ActionResult GetSkinCss(string skinFileName)
    {
        var safe = Path.GetFileNameWithoutExtension(Path.GetFileName(skinFileName));
        var cssPath = Path.Combine(SkinsDir, safe + ".css");

        if (!System.IO.File.Exists(cssPath))
            return NotFound();

        return PhysicalFile(cssPath, "text/css");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private SkinInfo BuildSkinInfo(string cssPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(cssPath);
        var metaPath = Path.ChangeExtension(cssPath, ".json");

        SkinMeta? meta = null;
        if (System.IO.File.Exists(metaPath))
        {
            try
            {
                meta = JsonSerializer.Deserialize<SkinMeta>(
                    System.IO.File.ReadAllText(metaPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinSkin: could not parse meta for '{File}'", fileName);
            }
        }

        return new SkinInfo
        {
            Name        = meta?.Name        ?? fileName,
            FileName    = fileName,
            Author      = meta?.Author      ?? string.Empty,
            Description = meta?.Description ?? string.Empty,
            IsActive    = Plugin.Instance?.Configuration.ActiveSkin == fileName,
            CssUrl      = $"/Plugins/FinSkin/Skin/{fileName}.css"
        };
    }
}

/// <summary>Skin metadata returned by the API.</summary>
public sealed class SkinInfo
{
    public string Name        { get; set; } = string.Empty;
    public string FileName    { get; set; } = string.Empty;
    public string Author      { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool   IsActive    { get; set; }
    public string CssUrl      { get; set; } = string.Empty;
}

/// <summary>Optional per-skin metadata from a .json sidecar file.</summary>
public sealed class SkinMeta
{
    public string? Name        { get; set; }
    public string? Author      { get; set; }
    public string? Description { get; set; }
}
