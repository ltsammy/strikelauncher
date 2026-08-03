# Strike Launcher - Entwickler-Dokumentation

Technische Doku für alle, die am Launcher selbst weiterbauen. Für Spieler siehe die
[README.md](../README.md) im Repo-Root.

## Projektstruktur

```
StrikeLauncher.sln
src/StrikeLauncher/          C#/.NET 8 WPF App
  Models/                    Datenmodelle (ModEntry, ServerData, AppSettings, ...)
  Services/                  Pfaderkennung, Modlist-Parser, Steamworks, TeamSpeak, Server-Status, ...
  ViewModels/                MainViewModel (MVVM, CommunityToolkit.Mvvm)
  Converters/                XAML-Wertkonverter (Status-Farben, Bool->Visibility, ...)
  Assets/                    logo.png (zentrales Emblem im Header) + app.ico (App-/Taskleisten-Icon)
  config/default-config.json Standard-URLs, die beim ersten Start geladen werden
  steam_appid.txt            107410 (Arma 3) - nötig, damit Steamworks außerhalb von Steam startet
  steam_api64.dll            Steamworks SDK v1.65 Redistributable (lizenzkonform eingecheckt)
data/                        Beispiel modlist.html / serverdata.json + logo.png/logo.ico (Quelle für Assets/)
ts3plugin/                   Ablage für die .ts3_plugin Datei
.github/workflows/build.yml  CI-Build + Velopack-Release bei jedem Push/Tag
```

## 1. Repo & Hosting einrichten

Modlist und Serverdaten müssen nur über eine öffentliche, **unauthentifizierte** URL
per HTTP GET abrufbar sein - das kann `raw.githubusercontent.com` sein oder ein
eigener Webserver. `ModlistService`/`ServerDataService` machen einen simplen
`HttpClient.GetStringAsync(url)`, es gibt keine GitHub-spezifische Logik.

Aktuell konfiguriert (`src/StrikeLauncher/config/default-config.json`):

```
ModlistUrl:    https://ageofclones.de/strikelauncher/modlist.html
ServerDataUrl: https://ageofclones.de/strikelauncher/launcher.json
```

1. Repo: https://github.com/ltsammy/strikelauncher (bereits als `GithubRepoUrl` und
   `Ts3PluginUrl` in `default-config.json` hinterlegt) - wird für den **Auto-Updater**
   (Velopack-Releases) sowie für die `.ts3_plugin`-Datei gebraucht.
2. `data/modlist.example.html` als Vorlage für den echten Export aus dem offiziellen
   Arma 3 Launcher nutzen (Mods-Tab -> Preset -> "Export to file" -> HTML) und unter
   `https://ageofclones.de/strikelauncher/modlist.html` bereitstellen. Der Parser sucht
   zuerst nach dem offiziellen `<tr data-type="ModContainer">`-Format (Name steht in
   `<td data-type="DisplayName">`, nicht im Link selbst) und fällt bei anderen Formaten
   auf eine generische `<a href=".../filedetails/?id=...">`-Suche zurück.
3. `data/serverdata.example.json` als Vorlage für `launcher.json` nutzen. Bestätigtes
   Format (`ServerData`-Modell, `System.Text.Json` case-insensitive):
   ```json
   {
     "arma3": { "ip": "82.24.85.241", "port": 2592, "password": "" },
     "teamSpeak": { "host": "94.199.215.95", "port": 9987, "password": "" },
     "launcherBackgroundUrl": "https://.../background.jpg",
     "launcherDownloadUrl": "https://.../download"
   }
   ```
   `teamSpeak.host` darf auch `"ip:port"` enthalten (z. B. `"94.199.215.95:9987"`) -
   `ServerDataService` erkennt und entfernt den eingebetteten Port automatisch, bevor
   die `ts3server://`-Verbindungs-URI gebaut wird, damit der Port nicht doppelt landet.
   `launcherBackgroundUrl`/`launcherDownloadUrl` sind optional (leerer String = aus).
   `launcherBackgroundUrl` sollte JPG/PNG sein - WebP wird von WPFs `BitmapImage` nicht
   garantiert unterstützt (kein eingebauter Codec auf allen Windows-Installationen).
4. Die `.ts3_plugin`-Datei (Task Force Radio) liegt in `ts3plugin/` und wird über
   GitHub raw ausgeliefert.

Alle URLs lassen sich auch nachträglich direkt im Launcher unter **Einstellungen**
ändern, ohne neu zu kompilieren.

## 2. Lokal bauen

Voraussetzung: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows).

```
dotnet restore StrikeLauncher.sln
dotnet build StrikeLauncher.sln -c Release
dotnet run --project src/StrikeLauncher/StrikeLauncher.csproj
```

> **Bekannte Falle:** `<InvariantGlobalization>true</InvariantGlobalization>` im
> `.csproj` bricht WPF-Databinding zur Laufzeit (`Cannot find non-neutral culture
> related to 'en-us'`), sobald das erste Fenster gezeigt wird. WPF-Apps brauchen die
> volle Globalisierung - diesen Property-Eintrag nie setzen.

## 3. Steamworks (automatisches Abonnieren + Profil)

Die native Steam-Client-API wird direkt per P/Invoke über die "flat" C-Exporte von
`steam_api64.dll` angesprochen (`SteamWorkshopService.cs`) - **nicht** über das
Steamworks.NET NuGet-Paket. Grund: Steamworks SDKs ab etwa Mitte 2024 (dieses Projekt
nutzt v1.65) exportieren `SteamAPI_Init()`/`SteamUGC()` & Co. nicht mehr als callbare
Symbole (nur noch C++-Header-Inlines) - Steamworks.NET (auch die neueste Version
2024.8.0) versucht weiterhin `SteamAPI_Init` aufzurufen und crasht mit
`EntryPointNotFoundException`. Verifiziert per `GetProcAddress`-Probe gegen die
tatsächliche DLL. Stattdessen wird `SteamAPI_InitFlat` genutzt (offiziell für genau
diesen Zweck dokumentiert: "exported from the dll" für Sprachen ohne C++-Header).

Verwendete Flat-API-Funktionen (alle gegen die echte SDK v1.65 DLL verifiziert):
`SteamAPI_InitFlat`, `SteamAPI_RunCallbacks`, `SteamAPI_Shutdown`,
`SteamAPI_SteamUGC_v021` + `SteamAPI_ISteamUGC_{SubscribeItem,GetItemState,DownloadItem}`,
`SteamAPI_SteamFriends_v018` + `GetPersonaName`/`GetMediumFriendAvatar`,
`SteamAPI_SteamUser_v023` + `GetSteamID`, `SteamAPI_SteamUtils_v011` +
`GetImageSize`/`GetImageRGBA` (Avatar-Pixel, RGBA->BGRA-Swap nötig für
`PixelFormats.Bgra32`).

`steam_api64.dll` liegt unter `src/StrikeLauncher/steam_api64.dll` (aus dem
Steamworks SDK v1.65, `redistributable_bin/win64/`) und wird im `.csproj` automatisch
mit ins Build-Verzeichnis kopiert. Laut Steamworks-SDK-Lizenz frei weiterverteilbar,
daher im Repo eingecheckt. Bei einem SDK-Update einfach dieselbe Datei ersetzen
(gleicher Dateiname) - und die Exporte gegebenenfalls neu verifizieren
(`GetProcAddress` auf die o.g. Funktionsnamen).

Funktioniert nur, wenn Steam läuft und der Account Arma 3 besitzt. Läuft Steam nicht,
zeigt der Launcher das an und der Spieler muss die fehlenden Mods manuell im Workshop
abonnieren.

## 4. TeamSpeak 3

- **Erkennung**: Registry + Standardpfade; manuell überschreibbar in den Einstellungen.
- **Plugin-Install**: Der Launcher übergibt die `.ts3_plugin`-Datei an `ts3client_win64.exe`,
  was den nativen "Plugin installieren?"-Dialog von TeamSpeak öffnet. Bewusst nicht
  komplett "silent" umgesetzt, da das Format (`package.ini`-gesteuertes ZIP) inoffiziell
  ist und sich zwischen Client-Versionen ändern kann - der native Dialog ist stabil.
- **Sounds stumm**: Nutzt TS3s eigene eingebaute Option "Sounds deactivated" (Optionen
  -> Benachrichtigungen -> Sound Pack) - kein eigener generierter Soundpack mehr nötig.
  Verifiziert gegen ein echtes `settings.db`: Es gibt **keine** einzelne `preferences`-
  Tabelle, sondern mehrere Section-Tabellen (`Notifications`, `Application`, `General`),
  jeweils mit Schema `(timestamp, key, value)`. Der Schlüssel heißt `SoundPack`, der Wert
  für "stumm" ist der String `nosounds` - TS3 selbst schreibt das identisch in alle drei
  Tabellen, wenn man die Option über die UI wählt, daher patcht der Launcher alle drei.
  `settings.db` wird vorher gesichert (`settings.db.strikelauncher.bak`). Fallback, falls
  eine der Tabellen fehlt (z. B. bei einer neueren/älteren Client-Version): TeamSpeak ->
  Optionen -> Benachrichtigungen -> Soundpack "Sounds deactivated" manuell auswählen.
- **Verbindung**: über das `ts3server://` URI-Schema (Host/Port/Passwort aus `launcher.json`).
- **Auto-Close**: `MainViewModel` wartet nach dem Arma-3-Start per
  `Process.WaitForExitAsync()` auf das Spielende und ruft dann
  `TeamSpeakService.CloseAllInstancesAsync()` (erst `CloseMainWindow()`, nach 3s
  Gnadenfrist `Kill()`).

## 5. Arma 3 Start

Direkter Start der `arma3_x64.exe` (nicht über Steam-Startoptionen), mit:

```
-noSplash -skipIntro -noPause -noLogs -hugePages -mod="<installierte Mod-Pfade>" -connect=<ip> -port=<port> -password=<pw> -name="<nickname>"
```

Mod-Pfade zeigen direkt auf `steamapps/workshop/content/107410/<id>` - kein Symlink im
`!Workshop`-Ordner nötig. Solange der Prozess läuft, zeigt der START-Button "LÄUFT" an
(`MainViewModel.IsArma3Running`) und ist deaktiviert.

## 6. Server-Status-Anzeige

`ServerStatusService` prüft alle 60s (best effort, kein Live-Monitoring):

- **Arma 3**: echte Source-Engine A2S_INFO-UDP-Query auf `gamePort + 1` (Standard-
  Konvention der meisten Arma3-Server). Reicht dem Server ein abweichender Query-Port
  konfiguriert sein, zeigt das fälschlich "offline".
- **TeamSpeak**: TCP-Connect-Versuch auf ServerQuery-Port 10011 (Heuristik - manche
  Hoster blocken diesen Port von außen, dann zeigt es "offline" obwohl der Voice-Server
  läuft).

## 7. Auto-Update

Über [Velopack](https://velopack.io): `App.xaml.cs` ruft beim Start
`VelopackApp.Build().Run()` auf, `UpdateService` prüft gegen die GitHub Releases des
konfigurierten Repos (`GithubRepoUrl`) und installiert Updates automatisch mit
Neustart. Die GitHub Action verpackt **jeden** Push (main oder Tag) mit `vpk pack` und
erstellt ein vollwertiges (nicht-Prerelease) GitHub Release inkl. `SHA256SUMS.txt` -
das ist alles, was `GithubSource` von Velopack zum Auto-Update braucht. Das Release
enthält bewusst **nicht** den portablen ZIP (`*-Portable.zip`) - der ist für den
Updater irrelevant und würde nur die Assets-Liste aufblähen; er landet aber weiterhin
im CI-Build-Artifact.

Erst-Installation für Spieler: `Setup.exe` vom neuesten GitHub Release herunterladen
und ausführen (installiert lokal, danach läuft alles über Auto-Update).

## 8. Windows Defender / SmartScreen

Bewusst so gebaut, dass möglichst wenig Anlass zu einer False-Positive-Erkennung
besteht:

- **.NET/WPF statt Python/PyInstaller oder Electron**: kein gebündelter Interpreter,
  keine Signaturen, die klassische Packer-Heuristiken triggern.
- Keine Verschleierung, kein Packing, kein Code, der sich selbst modifiziert oder
  Persistenz/Autostart einträgt.
- Netzwerkzugriffe sind auf klar erkennbare Ziele beschränkt: GitHub (raw Content),
  Steam (Workshop), ageofclones.de und den eigenen TS3/Arma-Server.
- Quelloffen über dieses Repo + öffentlichen GitHub-Actions-Build (nachvollziehbare
  Build-Kette).

Was **nicht** automatisch gelöst ist, weil es Geld/Zeit kostet:

- **Ohne Code-Signing-Zertifikat** zeigt Windows SmartScreen bei neuen/unbekannten
  Releases anfangs "Unbekannter Herausgeber". Das ist kein Virus-Fund, baut sich aber
  nur über Zeit + Download-Zahl an Reputation ab. Optionen für später: klassisches
  OV/EV-Zertifikat (~150-400 €/Jahr) oder Azure Trusted Signing (günstiger,
  Microsoft-eigen) - beides ließe sich als Signing-Step in `build.yml` ergänzen,
  sobald ein Zertifikat/Secret vorhanden ist.
- Falls Defender trotzdem mal anschlägt: über
  https://www.microsoft.com/en-us/wdsi/filesubmission eine False-Positive-Meldung
  einreichen (Link zum GitHub-Release/Repo mit angeben).

## 9. Look & Feel

Theme "Cold Iron Protocol": gunmetal/steel Dunkeltöne passend zum 104th-Battalion-
Emblem, ein einzelner gedämpfter Phasen-Blau-Akzent (`#2F97D6`), harte 2px-Ecken
("milled steel plate" statt weichem Glassmorphism-Look), Glow-Effekte ausschließlich
auf interaktiven Elementen (START-Button) - der Titel selbst bleibt bewusst ohne
Leuchten für einen nüchternen, seriösen Auftritt. Entstanden aus drei unabhängigen
Entwürfen (Imperial/Republic/Rimrunner-Ästhetik), die zu diesem einen kohärenten
Ergebnis zusammengeführt wurden.

- **Custom Fenster-Chrome**: natives Windows-Fenster ist komplett ersetzt
  (`WindowStyle="None"` + `WindowChrome`), eigene dunkle Titelleiste mit
  Minimize/Maximize/Close - Resize/Aero-Snap bleiben über `WindowChrome` erhalten.
- **Hintergrund**: `launcherBackgroundUrl` aus `launcher.json` wird beim Start geladen,
  auf Festplatte gecacht (`%AppData%\StrikeLauncher\cache\background.img`, damit das
  Fenster beim nächsten Start sofort ein Bild zeigt statt leer zu sein) und als
  geweichzeichnetes (`BlurEffect`, Radius 24), abgedunkeltes (Gradient-Overlay)
  Vollbild hinter der gesamten UI gerendert - in `MainWindow.xaml` und
  `SettingsWindow.xaml` identisch. Ohne konfiguriertes Bild greift ein dunkler
  Fallback-Verlauf.
- **Logo**: `data/logo.png` (104th-Battalion-Emblem) liegt gespiegelt in
  `src/StrikeLauncher/Assets/` und wird mittig im Header angezeigt (`DecodePixelWidth`
  + `BitmapScalingMode=HighQuality` gegen Pixelbildung beim Herunterskalieren);
  `data/logo.ico` ist als `<ApplicationIcon>` gesetzt.
- **Steam-Profil + Server-Status** oben links: Avatar + Persona-Name (Steamworks Flat
  API), darunter eine eigene "SERVER STATUS"-Box mit Arma/TeamSpeak-Online-Anzeige
  (bewusst als eigener Kasten getrennt vom Profil, nicht integriert).
- **Mods-Liste**: kein natives `GridView` (sieht wie eine Windows-Standardtabelle aus) -
  stattdessen eine `ListBox` mit komplett eigenem Zeilen-Template (farbiger
  Status-Streifen links, Name, farbige Badge-Pille rechts).
- **Scrollbars**: global über einen impliziten `Style TargetType="ScrollBar"` in
  `App.xaml` themed (dünner Thumb, keine Pfeil-Buttons) statt der weißen OS-Standard-
  Scrollbar.
- **Download-Seite-Button**: erscheint im Footer nur, wenn `launcherDownloadUrl`
  gesetzt ist, und öffnet die URL im Standardbrowser.

## Bekannte Grenzen

- Steamworks-Subscribe braucht einen laufenden, eingeloggten Steam-Client mit dem
  Spiel im Besitz des Accounts - kein Server-seitiger Weg möglich (Valve bietet keine
  öffentliche API dafür an).
- TS3-Sound-Stummschaltung und Plugin-Erkennung sind best-effort, siehe oben.
- Server-Status-Checks sind Heuristiken (siehe Abschnitt 6), kein garantiert korrektes
  Live-Monitoring.
