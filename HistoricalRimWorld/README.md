# Historical RimWorld

An independent RimWorld 1.6 mod with an opt-in historical calendar, scenario data, political state, world reveal foundations, and a data-driven historical event and chronicle engine. Biotech is required; Royalty, Ideology, Anomaly, and Odyssey are not required. Historical dates use a 12-month, 5-day-per-month calendar and are derived from RimWorld's game ticks without changing vanilla time or pawn ages. Political entities are separate from vanilla Factions; the current data demonstrates the 770 BCE Eastern Zhou scenario.

## Build

Requirements: .NET SDK with the .NET Framework 4.7.2 targeting pack, and a RimWorld 1.6 installation.

In Windows PowerShell, from this directory:

```powershell
$env:RIMWORLD_DIR = 'C:\Path\To\RimWorld'
$env:HARMONY_DIR = 'C:\Path\To\RimWorld\Mods\Harmony\Current\Assemblies'
dotnet build .\Source\HistoricalRimWorld.csproj -c Release
```

Alternatively, pass the MSBuild property directly:

```powershell
dotnet build .\Source\HistoricalRimWorld.csproj -c Release `
  -p:RimWorldDir='C:\Path\To\RimWorld' -p:HarmonyDir='C:\Path\To\Harmony\Assemblies'
```

The project reads `Assembly-CSharp.dll` and the required Unity modules from `RimWorldWin64_Data\Managed`, plus `0Harmony.dll` from the Harmony 1.6 Assemblies directory. Harmony is required for the centralized world-map access and rendering patches. All game, Unity, and Harmony references are non-copy-local; the build writes `HistoricalRimWorld.dll` to `Assemblies` and does not bundle those dependency DLLs.

## In game

Enable the mod, start a new game, and choose **Eastern Zhou: The Royal Court Moves East** from the vanilla scenario menu. It starts at 770 BCE (astronomical year -769), generates five vanilla colonists, supplies a small configured starter stock, and initializes the existing `HR_Scenario_EasternZhou_770BCE` systems after map generation. The **Eastern Zhou** main tab shows the chapter overview and, after 740 BCE, allows optional completion and HR-010 archive export. The developer-only Historical Calendar scenario picker remains available for other historical scenarios. Chinese and English UI text is included.

Historical politics initializes alongside an explicitly selected scenario and persists in the save. The calendar tab shows regions, polities, domains, communities, and political changes. Developer controls are available only in developer mode.

Scenario initialization also records the 771/770 BCE Eastern Zhou background entries and schedules a disputed year-only 743 BCE Zheng succession entry in the saved **Historical Chronicle**. Use the Historical Chronicle main tab to browse visible records, filter by record type/category/region/polity, and page through results. Scheduled events are ordered by date, then ascending priority (smaller first), then DefName. See [Events and Chronicle](Docs/EventsAndChronicle.md) for execution and failure behavior.

The HR-007 foundation adds the **Historical Genealogy** main tab for saved person/family records, immediate relations, historical vs biological age, and lifespan deadlines. Pawn registration and lifecycle capture run only in an initialized historical scenario. See [Characters and Genealogy](Docs/CharactersAndGenealogy.md) for Scribe storage, public RimWorld 1.6 APIs, natural-death handling, and polling limitations.

HR-008 adds a community-scoped ancient technology tree, scenario starting state, research and diffusion APIs, an in-game technology tab, and an initial production slice for copper/bronze, pottery, and hemp cloth. Tech progress uses Scribe and the vanilla ResearchManager, worktables, skill gates, and Bills. Modern-content filtering is intentionally partial: it covers research, buildability, recipes/Bills, and the standard trader stock maker, but not every quest reward, raid, or world-generation route. See [Architecture](Docs/Architecture.md) for the API and limits.

HR-009 adds person, family, and community cultural state; culture/custom/ritual XML data; a culture tab; and planned, cancelable ancestor, funeral/memorial, and hospitality observances. Ritual completion rechecks the gathered Pawns, site, offering, visitor requirement where applicable, and duration. Observances are menu-driven and do not assign ceremony jobs or guarantee attendance. The relic archive stores permanent records/events separately from physical item links; designation, transfer, and loss can be recorded in its tab, while vanilla trade/caravan/theft ownership changes are not automatically intercepted. The first scenario data uses qualified, broad cultural categories rather than exclusive regional stereotypes. In-game Def loading and ceremony/relic lifecycle have not been verified by compilation alone. See [Architecture](Docs/Architecture.md) for persistence boundaries.

## Build

Build from Windows PowerShell with .NET SDK installed. The project reads the RimWorld installation from `RimWorldDir` (or `RIMWORLD_DIR`) and Harmony from `HarmonyDir` (or `HARMONY_DIR`):

```powershell
$env:RIMWORLD_DIR = 'C:\Path\To\RimWorld'
$env:HARMONY_DIR = 'C:\Path\To\Harmony\1.6\Assemblies'
dotnet build .\Source\HistoricalRimWorld.csproj -c Release
```

The assembly is emitted to `Assemblies\HistoricalRimWorld.dll`; RimWorld and Unity reference DLLs are not copied.

To install manually, place the `HistoricalRimWorld` folder in RimWorld's `Mods` directory and enable it in the mod list. See [Architecture](Docs/Architecture.md) and [Roadmap](Docs/Roadmap.md).

## Historical world prototype

The current 770 BCE world reveal setup uses `PrototypeWorld`. RimWorld's generated tiles are not treated as real-world Chinese coordinates. No verified historical boundary or real Earth tile mapping is shipped. In developer mode, the Historical World panel can associate selected tiles with configured historical regions; those assignments are save-specific. Until a tile is mapped, it is outside this prototype region system and remains accessible. Locked mapped tiles are masked and blocked for player caravan travel and player settlement placement. This prototype does not provide automated historical map coverage or Earth geography.

## Cross-scenario family legacy (HR-010)

The **Family Legacy** tab exports all or a selected family and kin to a versioned JSON archive under the RimWorld Config directory. Archive files stay outside game saves, are validated and size-limited, and use temporary writes with a retained backup. Select an archive and family to run a deterministic preview for the active scenario date. The preview does not change the game. Choose eligible descendants (up to eight) and any available relics, then use **Confirm import** to generate fresh vanilla colonist Pawns and carry selected relics into the player home map. Import is marked per archive and target scenario to prevent duplicate imports. Rates in the default population policy are gameplay parameters, not historical measurements. In-game import, save/reload, and rollback behavior still need runtime validation.

Archives are stored in `Config\HistoricalRimWorld\LegacyArchives` beneath the game's Config directory. Build outputs go to `Assemblies\HistoricalRimWorld.dll`; RimWorld, Unity and Harmony assemblies remain non-copy-local. Build from Windows PowerShell using the existing `RIMWORLD_DIR` and `HARMONY_DIR` example above.

## Eastern Zhou playable chapter (HR-011)

The menu scenario and setup are implemented, but this is an integration slice rather than a fully runtime-verified 30-year scenario. The generated Pawns have no guaranteed ages, skills or kinship. Local demands/incidents, political notifications and historically accurate crop definitions are still missing; content filtering does not cover every raid/reward/mod route. The starting tile is labeled Zheng only in the explicit PrototypeWorld mapping and is not an ancient-China location. See [Eastern Zhou Scenario](Docs/EasternZhouScenario.md) for its initialization order, start stock, sourced event and verification limits.
