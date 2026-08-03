# Strike Launcher (Age of Clones - Strike Platoon)

Eigener Arma 3 Launcher: lädt eine `modlist.html` + `serverdata.json` von diesem
GitHub-Repo, gleicht sie mit den installierten Steam-Workshop-Mods ab, kann fehlende
Mods direkt (ohne Browser) abonnieren, startet TeamSpeak 3 mit Task-Force-Radio-Plugin
stumm geschaltet und verbindet dich anschließend mit Arma 3 auf euren Server.

## Projektstruktur

```
StrikeLauncher.sln
src/StrikeLauncher/          C#/.NET 8 WPF App
  Models/                    Datenmodelle (ModEntry, ServerData, AppSettings, ...)
  Services/                  Pfaderkennung, Modlist-Parser, Steamworks, TeamSpeak, Background-Bild, ...
  ViewModels/                MainViewModel (MVVM, CommunityToolkit.Mvvm)
  Converters/                XAML-Wertkonverter (Status-Farben, Bool->Visibility, ...)
  Assets/                    logo.png (zentrales Emblem im Header) + app.ico (App-/Taskleisten-Icon)
  config/default-config.json Standard-URLs, die beim ersten Start geladen werden
  steam_appid.txt            107410 (Arma 3) - nötig, damit Steamworks außerhalb von Steam startet
data/                        Beispiel modlist.html / serverdata.json + logo.png/logo.ico (Quelle für Assets/)
ts3plugin/                   Ablage für die .ts3_plugin Datei
.github/workflows/build.yml  CI-Build + Velopack-Release bei Tags "vX.Y.Z"
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

> **Achtung:** Beide URLs haben beim Testen (03.08.2026) `HTTP 404` zurückgegeben.
> `https://ageofclones.de/strikelauncher/` (ohne Dateiname) leitet außerdem auf
> `/login?error=auth-required` um - falls das eine Next.js-Middleware ist, die alle
> Pfade unter `/strikelauncher` abfängt, muss `modlist.html`/`launcher.json` als
> **öffentliche** Route/Static-Asset davon ausgenommen werden (z. B. über den
> `public/`-Ordner + einen Ausschluss im Middleware-`matcher`, oder eine eigene
> API-Route ohne Auth-Check). Der Launcher schickt keine Login-Session mit, ein
> simpler `HttpClient` kann sich also nicht hinter `/login` durchklicken. Bitte prüfen,
> ob die Dateien schon deployed sind bzw. der Pfad stimmt, bevor der Launcher getestet
> wird.

Weitere Schritte:

1. Repo auf GitHub anlegen (z. B. `dein-name/strikelauncher`) und diesen Ordner pushen
   - wird weiterhin für den **Auto-Updater** (Velopack-Releases) gebraucht, siehe
   `GithubRepoUrl` in `default-config.json`.
2. `data/modlist.example.html` als Vorlage für den echten Export aus dem offiziellen
   Arma 3 Launcher nutzen (Mods-Tab -> Preset -> "Export to file" -> HTML) und unter
   `https://ageofclones.de/strikelauncher/modlist.html` bereitstellen. Der Parser sucht
   nur nach `<a href=".../filedetails/?id=...">` - der offizielle Export funktioniert
   unverändert.
3. `data/serverdata.example.json` als Vorlage für `launcher.json` nutzen und mit den
   echten Arma3-/TeamSpeak-Werten unter obiger URL bereitstellen. Bestätigtes Format
   (`ServerData`-Modell, `System.Text.Json` case-insensitive):
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
4. Die `.ts3_plugin`-Datei (Task Force Radio) in `ts3plugin/` ablegen, siehe
   `ts3plugin/README.md` (aktuell noch über GitHub raw verlinkt - kann genauso auf
   ageofclones.de umgezogen werden, sobald der Pfad feststeht).

Alle URLs lassen sich auch nachträglich direkt im Launcher unter **Einstellungen**
ändern, ohne neu zu kompilieren.

## 2. Lokal bauen

Voraussetzung: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows).

```
dotnet restore StrikeLauncher.sln
dotnet build StrikeLauncher.sln -c Release
dotnet run --project src/StrikeLauncher/StrikeLauncher.csproj
```

> **Hinweis:** Dieses Projekt wurde in einer Sandbox ohne installiertes .NET SDK
> geschrieben - der Code wurde also nicht lokal kompiliert. Die GitHub Action ist der
> erste echte Build-Check. Wahrscheinlichste Stolpersteine beim ersten Build:
> einzelne NuGet-Paketversionen (`CommunityToolkit.Mvvm`, `HtmlAgilityPack`,
> `Microsoft.Data.Sqlite`, `Steamworks.NET`, `Velopack`) leicht anpassen, falls
> `dotnet restore` eine Version nicht findet (`dotnet add package <Name>` zieht dann
> automatisch die aktuell verfügbare).

## 3. Steamworks (automatisches Abonnieren)

Für das echte Auto-Subscribe (ohne dass der Spieler im Browser klicken muss) wird die
native Steam-Client-API angesprochen - das erfordert `steam_api64.dll` neben der
`.exe`. Diese DLL ist Teil des Steamworks SDK und darf aus Lizenzgründen hier nicht
automatisch mitgeliefert werden:

1. Kostenlosen Account auf https://partner.steamgames.com anlegen, SDK-Lizenz
   akzeptieren, SDK herunterladen.
2. `redistributable_bin/win64/steam_api64.dll` nach `src/StrikeLauncher/` kopieren und
   im `.csproj` als `<None Include="steam_api64.dll" CopyToOutputDirectory="PreserveNewest" />`
   eintragen (oder direkt in den Publish-Ordner legen).
3. Funktioniert nur, wenn Steam läuft und der Account Arma 3 besitzt. Läuft Steam
   nicht, zeigt der Launcher das an und der Spieler muss die fehlenden Mods manuell im
   Workshop abonnieren.

## 4. TeamSpeak 3

- **Erkennung**: Registry + Standardpfade; manuell überschreibbar in den Einstellungen.
- **Plugin-Install**: Der Launcher übergibt die `.ts3_plugin`-Datei an `ts3client_win64.exe`,
  was den nativen "Plugin installieren?"-Dialog von TeamSpeak öffnet. Bewusst nicht
  komplett "silent" umgesetzt, da das Format (`package.ini`-gesteuertes ZIP) inoffiziell
  ist und sich zwischen Client-Versionen ändern kann - der native Dialog ist stabil.
- **Sounds stumm**: Der Launcher legt einen stillen Soundpack (`StrikeLauncher-Silent`,
  0-Byte-WAVs) an und versucht, ihn über `settings.db` (SQLite) automatisch zu aktivieren.
  Das DB-Schema von TeamSpeak ist nicht offiziell dokumentiert, daher: `settings.db` wird
  vorher gesichert (`settings.db.strikelauncher.bak`), und wenn nichts Passendes gefunden
  wird, überspringt der Launcher das und loggt einen Hinweis. Fallback (einmalig, 10
  Sekunden): TeamSpeak -> Optionen -> Benachrichtigungen -> Soundpack
  `StrikeLauncher-Silent` auswählen.
- **Verbindung**: über das `ts3server://` URI-Schema (Host/Port/Passwort aus
  `serverdata.json`).

## 5. Arma 3 Start

Direkter Start der `arma3_x64.exe` (nicht über Steam-Startoptionen), mit:

```
-noSplash -skipIntro -noPause -noLogs -hugePages -mod="<installierte Mod-Pfade>" -connect=<ip> -port=<port> -password=<pw> -name="<nickname>"
```

Mod-Pfade zeigen direkt auf `steamapps/workshop/content/107410/<id>` - kein Symlink im
`!Workshop`-Ordner nötig.

## 6. Auto-Update

Über [Velopack](https://velopack.io): `App.xaml.cs` ruft beim Start
`VelopackApp.Build().Run()` auf, `UpdateService` prüft gegen die GitHub Releases des
konfigurierten Repos (`GithubRepoUrl`) und installiert Updates automatisch mit
Neustart. Die GitHub Action verpackt jeden Tag-Push (`vX.Y.Z`) mit `vpk pack` und lädt
das Ergebnis als Release-Assets hoch - das ist alles, was `GithubSource` von Velopack
zum Auto-Update braucht.

Erst-Installation für Spieler: `Setup.exe` vom neuesten GitHub Release herunterladen
und ausführen (installiert lokal, danach läuft alles über Auto-Update).

## 7. Windows Defender / SmartScreen

Es wurde bewusst so gebaut, dass möglichst wenig Anlass zu einer False-Positive-Erkennung
besteht:

- **.NET/WPF statt Python/PyInstaller oder Electron**: kein gebündelter Interpreter,
  keine Signaturen, die klassische Packer-Heuristiken triggern.
- Keine Verschleierung, kein Packing, kein Code, der sich selbst modifiziert oder
  Persistenz/Autostart einträgt.
- Netzwerkzugriffe sind auf drei klar erkennbare Ziele beschränkt: GitHub (raw
  Content), Steam (Workshop) und den eigenen TS3/Arma-Server.
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

## 8. Look & Feel

Theme "Cold Iron Protocol": gunmetal/steel Dunkeltöne passend zum 104th-Battalion-
Emblem, ein einzelner gedämpfter Phasen-Blau-Akzent (`#2F97D6`), harte 2px-Ecken
("milled steel plate" statt weichem Glassmorphism-Look), Glow-Effekte ausschließlich
auf interaktiven/lebendigen Elementen (START-Button, Status-Punkt im Header) - der
Titel selbst bleibt bewusst ohne Leuchten für einen nüchternen, seriösen Auftritt.
Entstanden aus drei unabhängigen Entwürfen (Imperial/Republic/Rimrunner-Ästhetik),
die zu diesem einen kohärenten Ergebnis zusammengeführt wurden.

- **Hintergrund**: `launcherBackgroundUrl` aus `launcher.json` wird beim Start geladen,
  auf Festplatte gecacht (`%AppData%\StrikeLauncher\cache\background.img`, damit das
  Fenster beim nächsten Start sofort ein Bild zeigt statt leer zu sein) und als
  geweichzeichnetes (`BlurEffect`, Radius 24), abgedunkeltes (Gradient-Overlay)
  Vollbild hinter der gesamten UI gerendert - in `MainWindow.xaml` und
  `SettingsWindow.xaml` identisch. Ohne konfiguriertes Bild greift ein dunkler
  Fallback-Verlauf, das Fenster ist also nie leer/unfertig.
- **Logo**: `data/logo.png` (104th-Battalion-Emblem) liegt gespiegelt in
  `src/StrikeLauncher/Assets/` und wird mittig im Header angezeigt;
  `data/logo.ico` ist als `<ApplicationIcon>` gesetzt (Taskleiste, .exe-Icon,
  Fenster-Icon). Wird das Emblem später ausgetauscht, einfach beide Dateien in
  `data/` und `src/StrikeLauncher/Assets/` ersetzen (gleicher Dateiname).
- **Status-Punkt**: der kleine pulsierende Punkt neben "104TH BATTALION · STRIKE
  PLATOON LAUNCHER" ist kein Dekor, sondern zeigt echten Status - grün/glühend wenn
  sowohl Arma 3- als auch TeamSpeak-Pfad erkannt wurden, sonst amber.
- **Download-Seite-Button**: erscheint im Footer nur, wenn `launcherDownloadUrl`
  gesetzt ist, und öffnet die URL im Standardbrowser.

## Bekannte Grenzen

- Steamworks-Subscribe braucht einen laufenden, eingeloggten Steam-Client mit dem
  Spiel im Besitz des Accounts - kein Server-seitiger Weg möglich (Valve bietet keine
  öffentliche API dafür an).
- TS3-Sound-Stummschaltung und Plugin-Erkennung sind best-effort, siehe oben.
- `steam_api64.dll` muss aus Lizenzgründen manuell besorgt werden (siehe Abschnitt 3).
