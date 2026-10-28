# Historical RimWorld

An independent RimWorld 1.6 mod foundation. Biotech is required; Royalty, Ideology, Anomaly, and Odyssey are not required. The current version contains only project bootstrap and a persisted debug-logging setting.

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

To install manually, place the `HistoricalRimWorld` folder in RimWorld's `Mods` directory and enable it in the mod list. See [Architecture](Docs/Architecture.md) and [Roadmap](Docs/Roadmap.md).
