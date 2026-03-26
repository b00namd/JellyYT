# JellyTube & JellyTubbing – Jellyfin Plugins

Zwei Jellyfin-Plugins rund um YouTube:

| Plugin | Zweck |
|---|---|
| **JellyTube** | YouTube-Videos herunterladen und in die Mediathek speichern |
| **JellyTubbing** | YouTube-Videos direkt in Jellyfin streamen (ohne Download) |

---

## JellyTube

Lädt YouTube-Videos und Playlists direkt in die Mediathek. Nutzt [yt-dlp](https://github.com/yt-dlp/yt-dlp) für den Download und erstellt automatisch NFO-Metadaten sowie Vorschaubilder.

### Features

- Videos und Playlists per URL herunterladen
- Metadaten-Vorschau vor dem Download
- NFO-Dateien und Vorschaubilder automatisch generieren
- Nach Kanal in Unterordner sortieren
- Untertitel herunterladen (mehrere Sprachen)
- Download-Warteschlange mit Fortschrittsanzeige, Laufzeitanzeige und sofortigem Start
- Aktive und wartende Jobs einzeln abbrechen
- Abgeschlossene Jobs per Knopfdruck leeren
- Jellyfin-Bibliothek nach Download automatisch aktualisieren
- Geplante Playlist-Downloads per Scheduled Task
- Pro geplantem Download: eigenes Maximalalter und „Gesehen löschen"-Option
- Archiv für geplante Downloads – bereits heruntergeladene Videos werden übersprungen
- Archiv zurücksetzen direkt im Plugin möglich
- Smarter Kanal-Scan: stoppt beim ersten bereits archivierten oder zu alten Video (`--break-on-existing`, `--break-on-reject`)
- Standard-Audiosprache konfigurierbar (ISO 639-2, z. B. `deu`) – wird als Sprachmetadaten in die Audiodatei eingebettet
- Verwaiste yt-dlp-Prozesse werden beim Neustart automatisch beendet
- yt-dlp und ffmpeg Verfügbarkeitscheck in den Einstellungen
- Vollständig auf Deutsch

### Einstellungen

| Einstellung | Beschreibung |
|---|---|
| Download-Pfad | Basisverzeichnis für heruntergeladene Videos |
| yt-dlp Programmpfad | Optionaler vollständiger Pfad zur yt-dlp-Binary |
| ffmpeg Programmpfad | Optionaler vollständiger Pfad zur ffmpeg-Binary |
| Videoformat | Voreinstellung oder benutzerdefinierter yt-dlp Format-String |
| Bevorzugter Container | MP4, MKV oder WebM |
| Max. gleichzeitige Downloads | 1–10 |
| Nach Kanal sortieren | Erstellt Unterordner pro Kanal |
| Untertitel herunterladen | Inkl. Sprachauswahl |
| NFO-Dateien schreiben | Metadaten für Jellyfin |
| Vorschaubilder herunterladen | Thumbnails speichern |
| Bibliothek aktualisieren | Scan nach Download |
| Standard-Audiosprache | ISO 639-2 Sprachcode (z. B. `deu`), der in die Audiometadaten eingebettet wird |
| Geplante Downloads | Playlists automatisch prüfen, inkl. Maximalalter und „Gesehen löschen" pro Eintrag |
| Max. Videoalter (Playlist) | Globales Limit: nur Videos der letzten N Tage herunterladen |
| Gesehene Videos löschen | Nur für geplante Downloads: Datei nach dem Schauen löschen, kein erneuter Download |

---

## JellyTubbing

Streamt YouTube-Videos direkt in Jellyfin – ohne Download, ohne Invidious.

- **JellyTrending-Kanal:** YouTube-Trending-Videos nach Region und Kategorie direkt in Jellyfin durchsuchen und abspielen
- **Abo-Synchronisation:** Abonnierte Kanäle werden als `.strm`-Dateien in eine Jellyfin-Bibliothek synchronisiert und sind dort durchsuch- und abspielbar

Stream-URLs werden bei jedem Abspielen frisch über [yt-dlp](https://github.com/yt-dlp/yt-dlp) aufgelöst – keine abgelaufenen Links.

### Features

- **JellyTrending-Kanal** mit Trending-Videos nach Region (DE, AT, CH, US, …)
- Kategorien: Trending, Musik, Gaming, Nachrichten, Filme
- Abonnierte YouTube-Kanäle mit Google-Konto verbinden und als Jellyfin-Bibliothek synchronisieren
- **Jellyfin-Bibliothek wird beim ersten Sync automatisch angelegt** – kein manuelles Einrichten nötig
- YouTube Shorts (≤ 120 s) werden standardmäßig nicht synchronisiert (optional aktivierbar)
- Sync-Zeitplan über **Geplante Aufgaben → JellyTubbing → Kanal-Synchronisation** konfigurierbar (Standard: alle 24 h)
- Manueller Sync-Trigger direkt im Plugin (speichert Einstellungen automatisch)
- Google-Verbindung per Device Authorization Grant – kein Redirect-URI, kein öffentlicher Server nötig
- Client-Secret-JSON aus der Google Cloud Console direkt importieren
- yt-dlp-Verfügbarkeitscheck in den Einstellungen
- Stream-Qualität konfigurierbar (360p – 4K)
- Gesehene Videos automatisch löschen (STRM + NFO + Thumbnail)

### Einstellungen

| Einstellung | Beschreibung |
|---|---|
| YouTube Data API Key | Erforderlich für Trending und Kanal-Videos |
| OAuth2 Client-ID / Secret | Erforderlich für Abo-Synchronisation |
| STRM-Ausgabeordner | Zielordner auf dem Server für `.strm`-, `.nfo`- und Thumbnail-Dateien |
| Max. Videos pro Kanal | Wie viele Videos pro Kanal beim Sync geholt werden (Standard: 25) |
| YouTube Shorts einschließen | Shorts (≤ 120 s) beim Sync berücksichtigen (Standard: aus) |
| Gesehene Videos löschen | STRM + NFO + Thumbnail nach dem Schauen automatisch löschen |
| yt-dlp Programmpfad | Optional – leer lassen wenn yt-dlp im PATH liegt |
| Bevorzugte Qualität | Stream-Auflösung (360p, 480p, 720p, 1080p, 2K, 4K) |
| Trending-Region | Land für Trending-Videos im JellyTrending-Kanal (z. B. DE, US, GB) |

---

### Einrichtung Schritt für Schritt

#### 1. YouTube Data API Key erstellen

1. [Google Cloud Console](https://console.cloud.google.com/) öffnen
2. Neues Projekt anlegen (oder bestehendes wählen)
3. **APIs & Dienste → Bibliothek → „YouTube Data API v3"** aktivieren
4. **APIs & Dienste → Anmeldedaten → Anmeldedaten erstellen → API-Schlüssel**
5. Den generierten Key im Plugin unter **YouTube Data API Key** eintragen

Der API-Key ist für den **JellyTrending-Kanal** und die **Abo-Synchronisation** erforderlich.

#### 2. OAuth2-Credentials erstellen (für Abo-Synchronisation)

> Nur nötig, wenn abonnierte Kanäle synchronisiert werden sollen.

1. [Google Cloud Console](https://console.cloud.google.com/) → **APIs & Dienste → OAuth-Zustimmungsbildschirm**
   - Typ: **Extern**
   - App-Name, Support-E-Mail und Entwickler-E-Mail ausfüllen
   - Unter **Scopes**: `youtube.readonly` hinzufügen
   - Unter **Testnutzer**: eigene Google-E-Mail eintragen
2. **Anmeldedaten → Anmeldedaten erstellen → OAuth-Client-ID**
   - Anwendungstyp: **Fernseher und eingeschränkte Eingabegeräte**
   - Beliebigen Namen vergeben
3. JSON-Datei herunterladen (oder Client-ID und Secret notieren)

> **Wichtig:** Der Anwendungstyp muss „Fernseher und eingeschränkte Eingabegeräte" sein, da JellyTubbing den Device Authorization Grant (RFC 8628) nutzt – kein öffentlicher Server oder Redirect-URI nötig.

#### 3. Credentials im Plugin eintragen

Im Jellyfin Dashboard → **Plugins → JellyTubbing → Einstellungen**:

- **JSON importieren:** Heruntergeladene `client_secret_*.json` direkt hochladen – Client-ID und Secret werden automatisch eingetragen
- Oder Client-ID und Client-Secret manuell eintragen und **Einstellungen speichern**

#### 4. Mit Google verbinden

1. **Mit Google verbinden** klicken (speichert die Credentials automatisch)
2. Es erscheint ein kurzer Code und die URL `accounts.google.com/device`
3. URL im Browser öffnen, Code eingeben und mit dem Google-Konto bestätigen
4. Das Plugin erkennt die Bestätigung automatisch – Status wechselt zu **Mit Google verbunden**

#### 5. STRM-Ausgabeordner wählen

- Unter **STRM-Ausgabeordner** einen Pfad auf dem Server eintragen oder per **Durchsuchen** wählen (z. B. `/media/jellytubbing`)
- Die Jellyfin-Bibliothek **JellyTubbing** wird beim ersten Sync automatisch angelegt und ist danach in der Mediathek sichtbar

#### 6. Kanäle auswählen und synchronisieren

1. Nach der Google-Verbindung erscheint die Liste aller abonnierten Kanäle
2. Gewünschte Kanäle anhaken
3. **Jetzt synchronisieren** klicken – speichert Einstellungen und startet den Sync sofort
4. Nach dem Sync erscheinen die Videos in der Jellyfin-Bibliothek **JellyTubbing** (Unterordner pro Kanal)

Den automatischen Sync-Zeitplan konfigurierst du unter:
**Administration → Geplante Aufgaben → JellyTubbing → Kanal-Synchronisation** (Standard: täglich)

---

## Installation

### 1. Repository hinzufügen

In Jellyfin:
**Admin Dashboard → Plugins → Repositories → Hinzufügen**

Repository-URL:
```
https://raw.githubusercontent.com/b00namd/JellyFinPlugins/master/dist/manifest.json
```

### 2. Plugins installieren

**Admin Dashboard → Plugins → Katalog → JellyTube / JellyTubbing → Installieren**

### 3. Jellyfin neu starten

Nach der Installation muss Jellyfin neu gestartet werden.

---


## Voraussetzungen

- Jellyfin 10.9.x oder neuer
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) auf dem Server installiert (für beide Plugins)
- [ffmpeg](https://ffmpeg.org/) für Containerkonvertierung (mp4/mkv) – nur für JellyTube
- YouTube Data API Key – für JellyTubbing (Trending + Kanal-Sync)
- Google OAuth2-Credentials – für JellyTubbing (Abo-Synchronisation)

yt-dlp und ffmpeg können entweder im Systempfad (PATH) liegen oder der vollständige Pfad wird in den Plugin-Einstellungen angegeben.

---

## Selbst bauen

```powershell
# Beide Plugins bauen, ZIPs und Manifest erstellen
.\build.ps1
```

---

## Hinweis

Diese Plugins ermöglichen den Zugriff auf YouTube-Inhalte. Das Herunterladen und Streamen von Videos kann gegen die Nutzungsbedingungen von YouTube verstoßen. Die Nutzung erfolgt auf eigene Verantwortung und sollte ausschließlich für den persönlichen Gebrauch erfolgen.
