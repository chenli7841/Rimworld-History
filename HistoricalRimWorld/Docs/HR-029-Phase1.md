# HR-029 Phase 1: Historical Technology Tab and Timeline

## Implemented

- Added `HR_HistoricalTechnologies` as a native `ResearchTabDef`. All 22 existing historical research projects keep their original Def names, costs, prerequisites, and research coordinates, and are assigned to this tab.
- Linked each native `ResearchProjectDef` to its existing `HistoricalTechnologyDef` using `HistoricalTechExtension`.
- Reused `MainTabWindow_Research` and its native project nodes, prerequisite connections, selection/details pane, research progress, and scroll area. The historical tab adds category filters, a date axis, a current-year marker, and zoom controls; it does not create a parallel research-point system.
- Added a configurable piecewise-linear map between astronomical years and timeline X coordinates. The checked-in segments are monotonic and contiguous; the inverse uses the same segments.
- Projects are placed in category bands, with overlapping same-category dates placed in additional lanes. Projects without date metadata use a separate undated position.
- Projects whose configured start/evidence year is later than the current historical year are hidden by default. The `ShowFutureHistoricalTechnologies` setting allows previewing them. The existing historical research manager remains responsible for actual study eligibility; previewing does not grant research access.
- Project descriptions include historical date range, category, evidence level, source notes, and current research/adoption status. Added Simplified Chinese and English strings, including Chinese historical technology labels and descriptions.
- Timeline zoom and future-preview settings use the existing `ModSettings` Scribe persistence.

## Existing data and integration

The timeline reads years and categories from HR-008 `HistoricalTechnologyDef` records and uses HR-002 `HistoricalYearConverter` for BCE/astronomical-year conversion. The project-to-technology reference is stored on a `DefModExtension`. Native research prerequisites continue to supply graph links. No GameComponent state or research completion behavior was changed for this phase.

## Validation performed

- Built `HistoricalRimWorld.csproj` for `net472` against the locally installed RimWorld 1.6 assemblies.
- Parsed the changed Def and language XML as well-formed XML.
- Checked that all 22 existing `HR_Research_*` projects have exactly one historical-tab assignment and one technology reference, without changing the original research cost or coordinates.
- Checked the timeline segment definitions for increasing year and X ranges and contiguous boundaries.

RimWorld itself was not launched for this phase, so in-game UI behavior, Def loading, and save/reload were not runtime-tested in this environment.

## Not included in Phase 1

Technology appearance notifications, faction diffusion, technology blueprints/trade, authorization gates, practice tracking, completion gates, bonuses, and technology chronicle entries remain for Phases 2–6. The date windows remain the existing HR-008 estimates; this phase does not independently revise their historical accuracy.

## Follow-up interfaces

`HistoricalTechExtension` reserves fields for spread windows, origin faction tags, blueprint/practice requirements, and bonus IDs. The timeline layout Def can be extended with later chronological segments. Phase 2 can add world/faction technology state without changing this tab's use of vanilla research projects.
