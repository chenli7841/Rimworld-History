# HR-029 Phase 2: Historical technology emergence and diffusion

## Scope and architecture

Phase 2 extends `HistoricalTechnologyGameComponent`; it does not add another world technology database. Per-faction mastery and knowledge are the source of truth. World status (`NotEmerged`, `Emerged`, `Spreading`, `Widespread`) is derived from those records. Player awareness and notice de-duplication are stored separately. Saved faction records use historical state DefNames when available and preserve records after a faction disappears.

`HistoricalTechnologyDiffusionService` runs from the existing GameComponent at most once per historical day. Emergence attempts are annual and diffusion intervals are independently persisted per research project. Rolls are deterministic from project, faction, year, and interval IDs; drawing the research UI never runs the simulation. Long jumps are handled as one current-date update, avoiding a burst of catch-up letters. The developer-only 10-year simulation explicitly advances synthetic dates in the technology records, not the game calendar, and suppresses notifications.

Faction starting knowledge is configured in `Defs/HistoricalTechnologySetupDefs/EasternZhou_FactionTechnologyProfiles.xml`. Those differences are scenario abstractions for gameplay, not claims that a specific polity invented or monopolized a technique. Initial profile mastery stores no fabricated exact mastery year. The early iron windows are also explicitly marked as scenario simulation windows, with disputed evidence; they do not assert precise invention dates.

Diffusion uses available settlement distance and RimWorld goodwill/hostility plus HR-004 Alliance, Tributary, and Hostility relations as a lightweight contact proxy. The project does not yet model roads or a dedicated trade-route graph. Military secrecy reduces diffusion probability. Unknown technology sources are never displayed to the player; a first source can become a rumor in knowledge-propagation mode, and a distinct second source can confirm it.

## Notifications and UI

The default historical-message mode sends one native `ChoiceLetter` when a technology first emerges. Knowledge-propagation mode waits until the player receives faction-source information. Clicking a letter opens the native Research window, switches to the historical technology tab, selects the project, and centers the timeline on its node when the tab draws. Letters do not start or finish research.

During the active historical scenario, emerged technologies remain hidden in the historical tab until the player has at least rumor-level awareness. The existing future-preview setting can show not-yet-emerged technologies for inspection. Details show world status and player awareness; known sources are listed only when learned. State changes invalidate the cached list.

## Save compatibility and APIs

All runtime records use Scribe through the existing GameComponent. Missing Phase 2 lists in a Phase 1 save are initialized as empty and populated from the active scenario without clearing existing player research. Notification booleans and last-notice date prevent re-sending after load. Missing Defs/faction references are tolerated; saved historical IDs and records remain intact.

Phase 3 can use these read-only/eligibility APIs:

- `HistoricalTechnologyManager.HasFactionMasteredTechnology(Faction, ResearchProjectDef)`
- `HistoricalTechnologyManager.HasTechnologyEmerged(ResearchProjectDef)`
- `HistoricalTechnologyManager.IsTechnologyKnownToPlayer(ResearchProjectDef)`
- `HistoricalTechnologyManager.GetKnownTechnologySources(ResearchProjectDef)`
- `HistoricalTechnologyManager.CanFactionSupplyTechnology(Faction, ResearchProjectDef)`

`CanFactionSupplyTechnology` only reports technical capacity. Phase 3 must still apply secrecy, trader type, diplomatic, and inventory rules.

## Developer actions

In developer mode, the `Historical RimWorld` debug actions include world status, faction status, force emergence, force diffusion, reveal, simulate 10 years, and rebuild missing faction state. Rebuild preserves existing records. The simulated-years action does not change the live calendar and suppresses emergence letters.

## Validation limits

The source compiles against the installed RimWorld assemblies and XML files are statically parsed. A live RimWorld run, new-game emergence, letter click-through, and an actual save/load roundtrip still require in-game verification. The simplified contact model is intentionally a Phase 2 approximation; actual route-aware diffusion remains future work.
