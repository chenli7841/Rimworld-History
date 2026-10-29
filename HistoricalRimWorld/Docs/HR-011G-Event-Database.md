# HR-011G Event Database

This release adds Eastern Zhou historical entries, local event templates, and story-chain data to the existing HR-006 event engine. It does not create another scheduler.

## Historical record: five dated entries

All dates are year precision. The data uses astronomical year numbering: 771 BCE is `-770`, 770 BCE is `-769`, 750 BCE is `-749`, and 745 BCE is `-744`.

| DefName | Date and mode | Evidence / provenance | Sources and runtime effect |
|---|---|---|---|
| `HR_Event_WesternZhouCrisis_771BCE` | 771 BCE; initial background | Probable; attested historical event | *Shiji*, “Zhou Benji”; opening chronicle context, no replayed actions. |
| `HR_Event_ZhouEasternMigration_770BCE` | 770 BCE; initial background | Probable; historical reconstruction; received chronology | *Shiji*, “Zhou Benji”; initializes the scenario's Luoyi and Eastern Zhou framing; it is not replayed after opening. The date is contested in Xinian-based scholarship. |
| `HR_Event_QinXiangRecognition_770BCE` | c. 770 BCE; initial background | Probable; historical reconstruction | *Shiji*, “Qin Benji”; chronicle and Qin context only. It does not grant Qin every western territory. |
| `HR_Event_XieKingDeath_750BCE` | c. 750 BCE; scheduled, year precision | Disputed; historical reconstruction; received-chronology default | *Shiji* traditions compared with Qinghua *Xinian* and modern reconstructions. It changes no polity; it stays hidden until a player learns it. |
| `HR_Event_JinQuwoEnfeoffment_745BCE` | 745 BCE (`-744`); scheduled, year precision | Probable; attested historical event; received chronology | *Zuo zhuan*, Lord Hui 24; Edward L. Shaughnessy, *A Brief History of Ancient China* (2023), pp. 179–180. Chronicle only; no unconfigured Quwo polity is created. |

## Continuing historical contexts: ten entries

These entries describe conditions spanning a period and do not schedule a dated event.

| DefName | Period | Evidence basis |
|---|---|---|
| `HR_Context_ZhengEasternExpansion` | 770–740 BCE | *Shiji*, “Zheng Shijia”; competing Zheng chronologies. |
| `HR_Context_ZhengAndKuai` | 770–740 BCE | *Shiji*, “Zheng Shijia”; Zhang (2023), notes 135–136 and Zheng chronology. |
| `HR_Context_RoyalCourtAtLuoyi` | 770–740 BCE | *Shiji*, “Zhou Benji”; modern King Ping / King Hui chronology studies. |
| `HR_Context_CompetingZhouLegitimacy` | 771–750 BCE | Qinghua *Xinian*, chapter 2; Chen and Pines (2018); Zhang (2023). |
| `HR_Context_JinSupportForPing` | 771–740 BCE | Qinghua *Xinian*, chapter 2; Zhang (2023), pp. 589–614. |
| `HR_Context_QinWesternDevelopment` | 770–740 BCE | *Shiji*, “Qin Benji”; modern Qin chronology studies. |
| `HR_Context_BronzeRitualObjects` | continuing context | Zhou bronze inscription and archaeology studies; no occurrence year asserted. |
| `HR_Context_AncestralRituals` | continuing context | Studies of Zhou ritual assemblages and funerary practice; no village ritual is asserted as fact. |
| `HR_Context_AgricultureAndStorage` | continuing context | Regional archaeology and settlement evidence; no single-year invention asserted. |
| `HR_Context_CraftTransmission` | continuing context | Craft production and exchange evidence; does not assume iron was common in every region. |

The Xinian-based reconstruction is recorded as an alternative scholarly tradition, not a selectable partial timeline. In particular, Yang Bo's 2021 reconstruction places the fall of Zhou You in 771 BCE, Kuai in 769, Eastern Guo in 767, Ping's elevation in 762, and relocation in 759. Chen Minzhen and Yuri Pines (2018) discuss a divided Zhou succession and the significance of King Xie. These are represented as disputed scholarship, not as extra scheduled events. See [Yang Bo (2021)](https://www.xsyk021.com/article/Y2021/I8/187), [Chen and Pines (2018)](https://cris.huji.ac.il/en/publications/where-is-king-ping-the-history-and-historiography-of-the-zhou-dyn/), and [Zhang (2023)](https://www.cambridge.org/core/journals/early-china/article/chronology-of-western-zhou/0D661C3365537C0D15AD896F72F9DF70).

## Local event templates

The shared scheduler gate requires an enabled template, an active home map, enough free colonists, no unresolved local choice, and a due local-event interval. Each template also observes its configured resource threshold, cooldown, appearance cap, and custom conditions. A chosen outcome is checked again before execution; costs are checked against map inventory before anything is consumed. Every resolved choice is recorded in the chronicle as a fictional local event.

| DefName | Additional trigger | Choices / actual outcome types |
|---|---|---|
| `HR_HRE_001` | Refugee household template; capped at two appearances. | Accept: add a generated colonist; aid: spend 15 rice; refuse: chronicle only. |
| `HR_HRE_002` | Requires at least 10 rice. | Accept an agricultural worker: add colonist; aid: spend 10 rice; refuse. |
| `HR_HRE_003` | Requires at least 15 rice. | Host: spend 15 rice; trade / decline: chronicle only. |
| `HR_HRE_004` | Requires at least 20 rice. | Pay: spend 20 rice; defer: chronicle only; refuse: set community posture to Autonomous. |
| `HR_HRE_005` | Boundary dispute. | Negotiate / assert boundary: chronicle only; compensate: spend 10 rice. |
| `HR_HRE_006` | Water-sharing dispute. | Share / refuse: chronicle only; infrastructure contribution: spend 5 rice. |
| `HR_HRE_007` | Grain-store damage. | Repair representation: spend 15 rice; salvage representation: gain 5 rice; defer: chronicle only. |
| `HR_HRE_008` | Requires at least 10 rice. | Accept potter: add colonist; temporary aid: spend 10 rice; refuse. No specific skill is fabricated. |
| `HR_HRE_009` | Only available after Bronze Casting is adopted. | Create one unique `HR_RelicDef_BronzeVessel` on the map for 10 copper ore and 5 tin ore; other choices consume 5 wood or decline. |
| `HR_HRE_010` | Requires at least 5 rice. | Trade 20 rice for 10 copper ore and 5 tin ore; or spend 5 rice to adopt Bronze Casting when its prerequisites permit; or decline. |
| `HR_HRE_011` | Requires at least 10 wood. | Accept a generated craftsperson colonist; spend 10 wood on the order; or decline. No battle-cart system is claimed. |
| `HR_HRE_012` | May occur only under its chain stage or ordinary template scheduling; relic transfers require an existing heirloom. | Create and register one unique family vessel; transfer a registered one to an eligible living descendant; or decline. |
| `HR_HRE_013` | Requires the registered `HR_RelicDef_FamilyVessel` on the home map. | Reveal Luoyi; increase recorded Zhou cultural presence by 0.05; or leave the inscription unknown. |
| `HR_HRE_014` | Foreign-household rite template; configured rice offering. | Spend 8 rice; increase Zhou cultural presence by 0.05; or decline / terminate the active chain. |
| `HR_HRE_015` | Requires two eligible adult colonists and a family record; minimum age 18. | Persist a RimWorld spouse relation and HR-007 kinship record; defer or refuse. Close parent/child/sibling pairs are rejected. |
| `HR_HRE_016` | **Disabled**: the existing genealogy system has no safe operation to bring an absent recorded relative onto the map. | Not offered. |
| `HR_HRE_017` | Requires at least 20 rice. | Ransom: spend 20 rice; refuse: set community posture to Autonomous; or record no material change. No raid is fabricated by this template. |
| `HR_HRE_018` | Requires a colonist with Medicine skill above 0. | Spend 2 herbal medicine; offer 5 rice; or refuse. |
| `HR_HRE_019` | Requires the King Xie entry to have occurred. | Reveal that already-occurred entry, timestamping player knowledge separately; spend 5 rice on the messenger; or ignore. |
| `HR_HRE_020` | **Disabled**: no reliable map / pawn / settlement migration operation exists in the current project. | Not offered; it does not fake relocation by changing a region ID. |

Outcomes currently supported by the shared local-event resolver are map inventory gain/spend/trade, generated colonist, politics posture, regional revelation, culture familiarity, technology adoption, unique relic creation and transfer, history revelation, and marriage. “Chronicle only” means the choice is persisted and recorded, but does not simulate an unimplemented relation, farm, waterworks, or building system.

## Continuous chains

```text
HRC-001 Eastward migration:
Arrival (001) → Settlement request (002) → Vessel (012) → Family rite (014) → integrate or depart (014)

HRC-002 Land and grain in Zheng:
New fields (002) → Boundary (005) → Grain demand (004) → Water sharing (006) → Community stance (017)

HRC-003 Bronze artisan:
Artisan (008) → Metals (010) → Cast vessel (009) → Workshop order (011) → Family keepsake (012, optional)

HRC-004 News from Luoyi:
First report (019) → Conflicting accounts (019) → Royal retinue (003) → Material request (004) → Recorded viewpoints (019)

HRC-005 Three decades of a family:
Family founded (001) → observed birth (optional) → marriage/branch (optional, 015)
→ death and rite (optional, 014) → relic inheritance (012) → chapter end / HR-010 archive export
```

Progress is stored in `HistoricalEventGameComponent` through `HistoricalStorylineRuntimeState` and `HistoricalLocalEventRuntimeRecord` using Scribe. Stage IDs, prerequisite completion, skipped optional stages, cancellation, selected choice IDs, trigger counts, cooldown dates, and unique relic IDs are persisted. Birth and death milestones observe HR-007 records and are never forced. The five chains advance when a choice is actually resolved; optional natural milestones time out into a recorded skipped stage. HRC-005 exports through the existing versioned HR-010 family exporter at the scenario end when a family record exists.

## HR-006 integration and information rules

- `HistoricalEventScheduler` remains the only scheduler and owns both dated history and local choices.
- Local `HistoricalLocalEventDef` templates point at HR-006 `HistoricalEventDef` records marked `LocalTemplate` and `FictionalLocalEvent`.
- `HistoricalStorylineDef` is the existing event-chain definition extension; no parallel timer or event queue was introduced.
- `HistoricalEasternZhouEventValidator.ValidateAll()` audits counts, source notes, chronology range, disputed tags, local provenance, condition validity, duplicate choices / unique relic rewards, and chain graph validity during scenario initialization.
- The chronology uses `HistoricalDateReference` and the HR-002 date resolver. RimWorld XML tags for its public fields are case-sensitive (`AstronomicalYear`, `Precision`).
- Hidden history is still scheduled internally. A player learns the King Xie report only after HRE-019, and the chronicle retains both its occurrence year and the date it became known.
- HR-004 posture changes use `HistoricalPoliticsManager`; culture, relic, technology, region, genealogy and legacy effects call their existing HR-005 / HR-007 / HR-008 / HR-009 / HR-010 managers.
- Initial background items are chronicle-only, so they cannot rewrite the scenario's initialized polity state.

## Remaining prerequisites

HRE-016 (relative arrival) and HRE-020 (actual household migration) remain disabled until HR-007 can safely spawn/restore a recorded absent person and the world/map systems can move a household while updating every dependent module. HRE-005/006/007/011 currently represent disputes, infrastructure contributions, store repair/salvage, and cart orders mainly through inventory and chronicle outcomes; those building and production simulations are not present. In-game Def loading and playthrough QA are still required after installing the updated XML and assembly.
