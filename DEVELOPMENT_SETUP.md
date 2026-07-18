# Development Environment Setup (Windows)

Diese Anleitung beschreibt das Aufsetzen der Entwicklungsumgebung fuer `gpxcut` auf Windows.
Sie ist fuer externe Contributor optimiert und nutzt einen winget-first Ansatz.

## 1. Ziel und Scope

### Zielgruppe
- Externe Contributor (primaer)
- Internes Team (sekundaer)

### In Scope
- Lokale Entwicklungsumgebung fuer Build, Test und Skill-Validierung
- Zugriff auf Projektressourcen und Referenzdokumente

### Out of Scope
- Endnutzer-Installer und Release-Distribution
- Nicht-Windows Plattformen (MVP ist Windows-only)

## 2. Pflicht- und Optional-Tools

## Pflichttools
- Git
- .NET SDK 8
- Visual Studio Code
- PowerShell 7+
- Microsoft Edge WebView2 Runtime

## Optional/Abhaengig
- `skills-ref` Validator (fuer Skill-Validierung)
- Node.js (nur falls zusaetzliches Frontend/Asset-Tooling im MapBridge-Workflow benoetigt wird)

## 3. Installation (winget-first)

Fuehre die folgenden Kommandos in PowerShell aus:

```powershell
winget install --id Git.Git -e
winget install --id Microsoft.DotNet.SDK.8 -e
winget install --id Microsoft.VisualStudioCode -e
winget install --id Microsoft.PowerShell -e
winget install --id Microsoft.EdgeWebView2Runtime -e
```

Wenn `winget` nicht verfuegbar ist, installiere die Tools manuell ueber die jeweiligen Herstellerseiten.

## 4. Versionen und Installation pruefen

```powershell
git --version
dotnet --info
dotnet --list-sdks
pwsh --version
```

Optional:

```powershell
skills-ref --version
node --version
npm --version
```

## 5. Repository klonen und lokal starten

```powershell
git clone <REPO_URL>
cd gpxcut
```

## 6. Projektvalidierung

### Standardweg (vorhandenes Skript)

```powershell
pwsh ./skills/gpxcut-track-editing/scripts/validate-solution.ps1
```

Das Skript fuehrt aktuell aus:
- `dotnet build -c Debug`
- `dotnet test -c Debug --no-build`

### Manueller Fallback

```powershell
dotnet build -c Debug
dotnet test -c Debug --no-build
```

## 7. Was gilt als "Setup erfolgreich"

Die Umgebung gilt als korrekt eingerichtet, wenn:
- Alle Pflichttools installiert sind
- `dotnet --list-sdks` ein 8.x SDK zeigt
- Build und Tests ohne Fehler durchlaufen
- Skill-Struktur vorhanden ist und (falls genutzt) `skills-ref validate` erfolgreich laeuft

## 8. Projektressourcen und Lesereihenfolge

Empfohlener Einstieg fuer neue Contributor:

1. [README.md](README.md) - Projektueberblick und Ziele
2. [AGENTS.md](AGENTS.md) - Architekturprinzipien, Scope, Arbeitsmodus
3. [skills/README.md](skills/README.md) - Skill-Struktur und Validator-Hinweise
4. [skills/gpxcut-track-editing/SKILL.md](skills/gpxcut-track-editing/SKILL.md) - Workflow und Quality Gates
5. [skills/gpxcut-track-editing/references/REFERENCE.md](skills/gpxcut-track-editing/references/REFERENCE.md) - Architekturgrenzen und Prioritaeten
6. Feature-Spezifikationen unter [skills/gpxcut-track-editing/references/features](skills/gpxcut-track-editing/references/features)

## 9. Troubleshooting (haeufige Probleme)

### Falsches oder fehlendes .NET SDK
- Symptom: Build faellt mit SDK-Fehlern aus
- Check: `dotnet --list-sdks`
- Fix: .NET SDK 8 installieren und neues Terminal starten

### WebView2 Runtime fehlt
- Symptom: Kartenansicht startet nicht korrekt
- Fix: Microsoft Edge WebView2 Runtime installieren

### PowerShell blockiert Skriptausfuehrung
- Symptom: `validate-solution.ps1` darf nicht ausgefuehrt werden
- Check/Fix (CurrentUser):

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

### Proxy/Firewall blockiert Paketquellen oder OSM-Tiles
- Symptom: Installationen oder Kartenkacheln schlagen fehl
- Fix: Proxy fuer `winget`, Git und Systemnetzwerk korrekt konfigurieren

## 10. Reproduzierbarkeit (optional, Nice-to-have)

Empfohlene naechste Schritte:
- Optionales Bootstrap-Skript fuer Tool-Checks und Preflight erstellen
- Optionales Diagnose-Skript fuer lokale Konsistenzchecks vor Commits
- Dokumentation bei jeder Toolchain-Aenderung mitpflegen

## 11. Pflegeprozess fuer diese Doku

- Bei Upgrade von .NET/Toolchain muss diese Datei aktualisiert werden
- Bei neuen Pflichttools muessen Installation und Verifikation ergaenzt werden
- Nach Aenderungen an Setup-Schritten immer Build+Test lokal gegenpruefen
