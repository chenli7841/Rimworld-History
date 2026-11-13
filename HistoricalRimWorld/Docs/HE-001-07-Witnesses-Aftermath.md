# HE-001-07 — Witnesses, battlefield aftermath, and political transition

## Design boundary

HE-001's existing `HistoricalEventManager`, event Defs, and
`HistoricalChronicleEntry` remain authoritative. This phase adds no second
historical scheduler or chronicle. Event outcomes are consumed idempotently by
the political outcome bridge; player evidence is stored separately from the
canonical event record so observation never changes what happened.

## Witnesses and evidence

`HistoricalEvidenceGameComponent` is versioned save data for per-Pawn witness
records and evidence records. HE-001-06's actual observer check supplies the
Pawn, battle stage, and observation interval. A witness record tracks cumulative
observation, the highest attained grade, stages actually seen, and evidence IDs.
When a qualifying witness returns to a player-home map, the service writes one
localized `HistoricalChronicleEntry` describing that Pawn's testimony. This
archive entry is marked Probable and lists the observation grade and stages; it
is not presented as a verbatim ancient-source quotation.

The royal records bundle is a fictional gameplay document. Securing the real
Thing creates a separate `RecoveredDocument` evidence record with
`ContemporaryDocument` reliability and one chronicle entry. The bundle is not
consumed or replaced. IDs derived from stable Pawn/Thing IDs, plus duplicate
checks, make both paths save-safe and idempotent.

## Battlefield aftermath

`MapComponent_HistoricalBattlefieldAging` reads the already-completed battle
instance and its saved historical end date. It classifies the Lishan scene as
fresh, weathered, overgrown, or ruins using Def-configured historical-day
thresholds. It does not destroy or respawn combatants, corpses, damaged
buildings, or equipment from a battle played on the map; RimWorld's normal
corpse rot and Thing deterioration, together with existing forest regrowth,
remain responsible for their physical changes.

When the event resolved off-map, the player may later see a limited scene
reconstruction: up to three anonymous, damaged wooden clubs in the battle zone.
This is explicitly gameplay reconstruction, not a casualty list. No Pawn,
named person, death, casualty count, or unsupported historical weapon is
invented. Those reconstruction objects are removed when the scene reaches its
ruins stage. No player-owned objects are touched.

## Political transition and HE-002 handoff

Committed HE-001 outcomes are applied in order: scheduled death, collapse,
succession crisis, then eastward-transition unlock. The death outcome waits for
the existing historical-fate executor. Collapse changes the existing
`HR_State_EasternZhou` polity to `Fragmented`; it does not delete the polity,
`HR_Faction_EasternZhou`, or its faction bindings. The save-backed politics
component then records that late Western Zhou authority is disrupted, the ruler
position is vacant, and succession is contested, with Yi Jiu recorded as a
claimant. The migration-completed flag remains false: this phase does not move
the court or execute HE-002.

`HistoricalEventPrerequisiteService.IsEventUnlocked` is the HE-002 integration
point. It reports the eastward relocation as available only after collapse and
succession outcomes are both applied and the transition-unlocked outcome is
committed. HE-002 itself remains owned and scheduled by HR-006.

## Persistence and limitations

Witnesses/evidence use `GameComponent` plus Scribe deep lists and Thing/Pawn
references. Political aftermath uses the existing politics component. The
battle's end historical day is stored on the existing battle instance, and
map-aging state plus reconstructed Thing references are saved on the scene's
`MapComponent`. Repeated ticks and reloads therefore reuse recorded outcomes
and IDs rather than replaying consequences.

Build validation confirms the code compiles against the configured RimWorld
1.6 assemblies. A live game test is still needed to verify witness visibility,
document recovery, save/reload, native corpse aging, and HE-002 integration with
the full active Mod list.
