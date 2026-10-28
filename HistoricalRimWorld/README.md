# Historical RimWorld

An independent RimWorld 1.6 mod with an opt-in historical calendar, scenario data, and political-state foundation. Biotech is required; Royalty, Ideology, Anomaly, and Odyssey are not required. Historical dates use a 12-month, 5-day-per-month calendar and are derived from RimWorld's game ticks without changing vanilla time or pawn ages. Political entities are separate from vanilla Factions; the current data demonstrates the 770 BCE Eastern Zhou scenario.

## Build

Requirements: .NET SDK with the .NET Framework 4.7.2 targeting pack, and a RimWorld 1.6 installation.

In Windows PowerShell, from this directory:

```powershell
$env:RIMWORLD_DIR = 'C:\Path\To\RimWorld'
dotnet build .\Source\HistoricalRimWorld.csproj -c Release
```

Alternatively, pass the MSBuild property directly:

```powershell
dotnet build .\Source\HistoricalRimWorld.csproj -c Release -p:RimWorldDir='C:\Path\To\RimWorld'
```

The project reads `Assembly-CSharp.dll` and `UnityEngine.CoreModule.dll` from `RimWorldWin64_Data\Managed`. It writes `HistoricalRimWorld.dll` to `Assemblies`; game and Unity references are marked non-copy-local, so vanilla DLLs are not bundled in the mod.

## In game

Enable the mod and open the **Historical Calendar** main tab. In developer mode, choose a loaded scenario to initialize it for the current save; the `Eastern Zhou Begins` Def starts at 770 BCE, Month 1, Day 1. This binds the scenario identity and initializes the HR-002 clock at the current game tick. Ordinary saves remain unchanged until explicitly initialized. The tab displays scenario information, the current historical date, and elapsed days; debug details appear in developer mode or when `EnableDebugLogging` is enabled in Mod Settings. Chinese and English translations are included.

Historical politics initializes alongside an explicitly selected scenario and persists in the save. The calendar tab shows regions, polities, domains, communities, and political changes. Developer controls are available only in developer mode.

To install manually, place the `HistoricalRimWorld` folder in RimWorld's `Mods` directory and enable it in the mod list. See [Architecture](Docs/Architecture.md) and [Roadmap](Docs/Roadmap.md).
