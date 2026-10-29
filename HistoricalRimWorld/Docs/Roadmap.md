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
10. **Scenario Legacy** — a separate, explicitly versioned cross-scenario archive with migrations; not implemented yet.
11. **Eastern Zhou Playable Scenario** — integrate the systems into a complete playable scenario after the data and runtime validation gates above.

Stages 10 and 11 remain out of scope for HR-009.
