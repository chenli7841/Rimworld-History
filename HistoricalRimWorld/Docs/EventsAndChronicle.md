# Historical Events and Chronicle

## Scheduling

Historical event dates keep their source precision in `HistoricalDateReference`. The scheduler maps Year precision to Month 1, Day 1; Month precision to that month's Day 1; Day precision to the exact supplied date. This is a simulation schedule, not a claim about the historical day.

`HistoricalEventGameComponent` checks on throttled game ticks and processes once when the historical absolute day advances. It catches up all pending events with `ScheduledDate <= CurrentDate`. Events sort by scheduled date, ascending priority (smaller values first), and ordinal DefName. Valid false conditions and unfinished prerequisites remain Pending and retry on the next historical day. Missing definitions, invalid actions, and execution exceptions become terminal Failed states. Event state and status changes are saved with Scribe.

InitialBackground events are imported as chronicle entries and marked Skipped so their actions cannot replay. Scheduled events whose date predates engine initialization are also not replayed. DevelopmentOnly events remain pending until due while developer mode is enabled; they are marked Speculative.

## Actions and atomicity

Conditions read public HR-004 and HR-005 APIs. Supported actions currently include polity status, political relations, region controller and nominal owner, region unlock/reveal, and a custom chronicle entry. A Def may include at most one world-state mutation and one custom chronicle append. All actions validate before execution; mutations go through the owning public manager, followed by chronicle insertion and notification.

There is no shared transaction API spanning HR-004 politics, HR-005 reveal state, and the chronicle component. Therefore the engine rejects multiple state mutations in one event and does not claim cross-component rollback. The implemented manager actions are idempotent for their target value, so an interrupted save can safely retry a Pending action that had already reached its target. Future multi-step events require a prepare/commit or undo API before that restriction can be lifted.

## Chronicle visibility

Chronicle records are AttestedHistory, LocalExperience, or InferredLegacy and retain stable string IDs plus source event, date precision, evidence, source notes, region, polity, category, and scope. The current UI pages 20 entries at a time and filters by type, category, region, and polity. It does not display entries dated after the active historical date. RegionKnowledgeRequired records remain hidden until HR-005 marks their region Revealed; visibility is refreshed as the historical day advances.

`HistoricalChronicleManager.TryAddLocalExperience` provides a saved local-experience entry API. HR-006 does not yet create randomized local incidents. InferredLegacy has a data API for future use; family systems remain out of scope.

## Included event data

- The 771 BCE Western Zhou crisis and 770 BCE Zhou eastward migration are InitialBackground chronicle records, not replayed state changes. Their source notes cite Sima Qian's *Shiji*, “Zhou Benji” (卷四《周本纪》), which records King You's death and King Ping's move east to Luoyi. The year precision follows conventional chronology.
- No post-770 BCE political event is included because this stage did not establish a sufficiently sourced date and outcome for one.
- `HR_Event_DebugUnlockHedong` is DevelopmentOnly and Speculative. It exists only to exercise the HR-005 region-unlock API in developer mode.
