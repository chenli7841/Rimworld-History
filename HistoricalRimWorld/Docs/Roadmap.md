# Roadmap

1. **Project Bootstrap** — independent RimWorld 1.6 / net472 project and settings foundation (implemented).
2. **Historical Calendar** — astronomical dates, calendar UI, and save persistence (implemented; in-game verification remains separate).
3. **Historical Scenario Definitions** — XML scenario, region, culture, settlement and validation layer (implemented as an initial data model).
4. **Historical Politics** — separate historical polities, control, communities, succession and chronicle changes (foundation implemented).
5. **World Revelation** — save-persisted access and prototype tile mapping (foundation implemented; no verified Earth map data).
6. **Historical Events** — event scheduler and chronicle foundation implemented; sourced scenario event coverage can grow.
7. **Genealogy and Lifespan** — saved person/family records, kinship, vanilla pawn links and lifespan policies (foundation implemented; in-game and edge-case validation remains).
8. **Historical Technology** — community technology state, vanilla research/production gates and an initial production slice (foundation implemented; content filtering is partial).
9. **Culture and Relics** — culture state, customs, ritual records/UI, and relic archive/item links implemented as an initial slice; verify Def loading and ceremony/relic lifecycle in-game, then deepen automated transmission and object transfer tracking.
10. **Scenario Legacy** — versioned external JSON archive, deterministic between-scenario family simulation, candidate preview, confirmed Pawn/relic import and Scribe idempotence markers implemented as an initial slice; run in-game save/reload and failure-path verification before broad release.
11. **Eastern Zhou Playable Scenario** — initial menu entry, data-driven Zheng village start, starting technology/items, startup integration, scenario overview/progress, optional chapter completion and HR-010 export hook implemented. Historical local society, age/skill-controlled founders, complete content filtering and in-game runtime verification remain open before calling the chapter complete.
12. **Eastern Zhou Factions and Prototype Geography** — HR-012 initial faction isolation, nine historical faction bindings, relative prototype-region settlement placement, Zheng starting-region restriction, and historically limited PawnKinds/traders implemented. Earth geography, direct special-faction sources, relationship synchronization, and in-game validation remain open.
