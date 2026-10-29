# Characters, Genealogy, and Lifespan (HR-007)

## Ownership boundaries

`Pawn` remains the source of truth for biological age, Biotech reproduction and growth, health, and ordinary death. `HistoricalPersonRecord` is a separate persistent identity with a UUID `PersonId`, historical birth date/source, family ID, and last-life state. No custom component is attached to every Pawn, and no Harmony patch is used.

`HistoricalGenealogyGameComponent` owns person, family, relation, lifespan, and current-save Pawn-reference lists. It persists those lists with Scribe and rebuilds string-ID indexes after load. Pawn references are serialized using `Scribe_References`; if a Pawn reference cannot be restored, the historical person and family records remain. These Pawn references are local to the RimWorld save and are not exported as portable identities.

The component runs only after a Historical RimWorld scenario and calendar have initialized. Every 250 ticks it inspects the verified RimWorld 1.6 `PawnsFinder.AllMapsWorldAndTemporary_Alive` list, registering live humanlike player-faction Pawns that are not already linked. Initial birth dates are estimated from `Pawn.ageTracker.AgeChronologicalTicks`; recently observed babies use the current historical date and are marked `ObservedInGame`. Historical age is derived from the HR-002 calendar, not `AgeBiologicalYears`. The game component also detects lifecycle changes and synchronizes direct parent/child and spouse relations between registered Pawns.

## Genealogy model

Families use separate UUIDs. Unrelated initial Pawns receive separate families. A recorded biological or adoptive child inherits a registered parent's family; spouse records connect two people without forcing a family merge. Parent direction is `PersonIdA = parent`, `PersonIdB = child`; spouse IDs are stored in a canonical order. Validation rejects self-parenting, duplicate relationships, more than two active biological parents, and biological ancestry cycles. Only immediate parents, children, and spouses are shown; the UI does not recursively walk the family tree.

The initial relationship pass reads RimWorld's `Pawn_RelationsTracker.DirectRelations`, `PawnRelationDefOf.Parent`, `ParentBirth`, `Child`, and `Spouse`. Relationship dates first observed by this mod are observation dates, not claims about when a historical marriage or parent relation began. Family and person records preserve stable IDs even when related Pawns or associated region/polity Defs are unavailable.

## Reproduction and lifecycle capture

Biotech remains responsible for gestation, embryo transfer, birth, and child growth. HR-007 observes newly present player-faction humanlike Pawns through the public Pawn finder every 250 ticks; it does not patch a single birth method, so it can see different Biotech birth pathways without replacing them. Pawns first observed as babies within one game day are recorded with the current historical date. This is a polling integration and can miss an entity that is created and removed between scans; the birth entry represents observation time at day precision.

The tracked Pawn reference lets the component retain a person after death and notice resurrection of that same Pawn. A revived Pawn keeps its PersonId, family, and original lifespan deadline. If the revived person is already past that deadline, the next daily lifespan pass invokes natural death again. A Pawn recreated as a different object by another mod cannot be proven to have the old identity automatically.

## Natural lifespan

`HistoricalLifespanPolicyDef` supplies minimum, maximum, mean, and standard deviation values. `HistoricalLifespanGenerator` draws once from RimWorld's seeded `Rand.Gaussian`, rounds and clamps the result, then stores the assigned years and immutable historical deadline in `HistoricalNaturalLifespanRecord`. The shipped policy is a balancing example, not a statement about ancient demographic life expectancy. Loading saves, treatment, genes, and equipment never reroll the deadline.

The lifespan pass runs once per historical day and only considers due lifespan records that still map to an alive Pawn. Natural death uses the RimWorld 1.6 public method verified locally as `Pawn.Kill(Nullable<DamageInfo>, Hediff)` (`pawn.Kill(null, null)`), allowing the original kill/death side effects to run. Ordinary deaths are recorded as `Unknown` unless HR-007 itself initiated them; the current public integration does not reliably infer combat, disease, accident, or starvation cause from all death paths. Death records and Chronicle entries remain; resurrection adds another Chronicle entry and does not extend the natural-life deadline.

## UI and Chronicle

The `Historical Genealogy` main tab provides a family list, member list, person detail, immediate kinship links, historical and current biological ages, stored birth/death dates, allegiance/place IDs, and the natural-death deadline/countdown. It warns when the deadline is within two historical years. This is a dedicated page, not a patch to the vanilla Bio or Health tab. Person/family creation, observed births, detected marriages, deaths, and resurrections append `LocalExperience` Chronicle records through HR-006.

## Current boundaries

- Registration is limited to live humanlike Pawns owned by `Faction.OfPlayer`; non-player relatives without their own registration are left as missing references.
- Parent/spouse synchronization only uses direct relations between registered Pawns. Adoption has a public validated relation API but is not inferred from an unrelated mod's adoption mechanics.
- Polling can delay a recorded birth or death by at most the scan interval, and a transient entity can be missed.
- The genealogy details are not embedded in the vanilla Bio page.
- The game was not runtime-launched as part of the HR-007 build; in-game save/restore and death effects still need play testing.
- No cross-scenario archive or HR-010 import/export is implemented.
