# Deploy script fuer JellyTubbing - baut das Plugin und kopiert es direkt auf den Jellyfin-Server.
# Voraussetzung: SSH-Zugriff auf den Server (idealerweise per SSH-Key, sonst Passwortabfrage).

param(
    [string]$Version    = "1.0.0.0",
    [string]$ServerUser = "henny",
    [string]$ServerHost = "192.168.1.5",
    [string]$PluginDir  = "/home/henny/docker/cloud/config/data/plugins/JellyTubbing_$Version",
    [switch]$RestartJellyfin
)

$ErrorActionPreference = "Stop"

$ProjectDir = Join-Path $PSScriptRoot "Jellyfin.Plugin.JellyTubbing"
$PublishDir = Join-Path $PSScriptRoot "dist\publish-jellytubbing-deploy"

# --- 1. Build ---
Write-Host "Baue JellyTubbing..." -ForegroundColor Cyan
dotnet publish "$ProjectDir" -c Release -o "$PublishDir" --no-self-contained
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fehlgeschlagen." }

# Jellyfin-eigene DLLs entfernen
@("MediaBrowser.Common.dll","MediaBrowser.Controller.dll","MediaBrowser.Model.dll","Microsoft.Extensions.*","Newtonsoft.Json.dll") | ForEach-Object {
    Get-ChildItem $PublishDir -Filter $_ | Remove-Item -Force
}

# --- 2. Verzeichnis auf Server anlegen ---
Write-Host "Erstelle Plugin-Verzeichnis auf Server..." -ForegroundColor Cyan
ssh "${ServerUser}@${ServerHost}" "mkdir -p `"${PluginDir}`""
if ($LASTEXITCODE -ne 0) { throw "SSH-Verbindung fehlgeschlagen. Ist SSH aktiv und erreichbar?" }

# --- 3. Dateien kopieren ---
Write-Host "Kopiere Dateien nach ${ServerHost}:${PluginDir} ..." -ForegroundColor Cyan
$files = Get-ChildItem $PublishDir -File
foreach ($file in $files) {
    scp $file.FullName "${ServerUser}@${ServerHost}:${PluginDir}/"
    if ($LASTEXITCODE -ne 0) { throw "Kopieren von $($file.Name) fehlgeschlagen." }
}

# --- 4. Jellyfin neu starten (optional) ---
if ($RestartJellyfin) {
    Write-Host "Starte Jellyfin-Container neu..." -ForegroundColor Cyan
    ssh "${ServerUser}@${ServerHost}" "docker restart jellyfin"
    Write-Host "Jellyfin wird neu gestartet." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Fertig! Bitte Jellyfin manuell neu starten:" -ForegroundColor Yellow
    Write-Host "  ssh ${ServerUser}@${ServerHost} 'docker restart jellyfin'" -ForegroundColor Gray
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host " Deployment abgeschlossen!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
