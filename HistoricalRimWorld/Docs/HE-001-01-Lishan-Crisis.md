# HE-001-01: Lishan Crisis Timeline

The Lishan crisis uses the HR-006 `HistoricalEventDef`, `HistoricalEventGameComponent`, `HistoricalEventScheduler`, and chronicle. Its legacy DefName (`HR_Event_WesternZhouCrisis_771BCE`) stays stable for saved references; the public event ID is `HE_001_LishanCrisis`.

The event starts with the Eastern Zhou scenario at astronomical year -771 (772 BCE). The forewarning is scheduled in that year; the seven later stages run during astronomical year -770 (771 BCE). Month/day assignments are game-calendar scheduling values, not claims that surviving sources establish those exact dates.

| Stage | Scheduled game date | Notification | Chronicle |
|---|---:|---|---|
| Forewarning | 772 BCE, year precision | Yes | No |
| PoliticalCrisis | 771 BCE, month 1 | No | Yes |
| MilitaryCrisis | 771 BCE, month 3 | Yes | Yes |
| RoyalDefeat | 771 BCE, month 6 | No | Yes |
| KingYouDeath | 771 BCE, month 7 | No | Yes |
| Aftermath | 771 BCE, month 9 | Yes | Yes |
| Completed | 771 BCE, month 12, day 5 | Yes | Yes |

The scheduler processes only the active scenario's definitions when the historical date advances. It catches up every overdue stage in order, independent of player location or map loading. Scene requirements are metadata only and cannot block this timeline. Catch-up transitions do not replay stale notifications.

Runtime progress is stored in the existing per-game event state through Scribe: timeline status, current stage, sequence, transition history, outcome IDs, notification/chronicle flags, consumer application markers, and the existing last-processed absolute date. Chronicle IDs are stable and checked before insertion.

Outcomes are queryable through `HistoricalEventManager` using either the stable event ID or legacy DefName. `HE001_YouWangDead` records the historical outcome only; this phase creates and kills no Pawn, starts no battle or quest, and does not implement a world-state political mutation. Later consumers can use `TryMarkOutcomeApplied` to persist their own idempotence marker.

The default interpretation is `TraditionalChronology`. Descriptions retain uncertainty around succession and the detailed military narrative; beacon-fire and Bao Si blame are not asserted as established fact.
