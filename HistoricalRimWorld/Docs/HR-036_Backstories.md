# HR-036: Pre-Qin life stories

The Eastern Zhou playable scenario uses 180 native `BackstoryDef` definitions: 60 childhood stories (`HR_C_`) and 120 adult stories (`HR_A_`). Chinese Simplified and English DefInjected files provide each title, short title, and description. Skill gains use vanilla `BackstoryDef.skillGains`, with no custom skill or post-generation skill adjustment; work restrictions are left open.

`HistoricalBackstoryDefExtension` stores a latest BCE year cutoff and identity weights. The selector chooses from an index built once after Def loading. It estimates the childhood period at age eight and the adult period at age eighteen from the pawn's biological age and the active historical calendar; during initial pawn setup it falls back to the selected scenario's start date. Dates are converted from astronomical years through the existing historical scenario date model.

In the Eastern Zhou scenario, the postfix on `PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo` replaces the generated childhood and, for adults, adult backstory with an HR-036 whitelist entry. Other scenarios and non-humanlike pawns retain vanilla behavior. A child receives a historical adult backstory when a biological birthday or life-stage change reaches adulthood, and an existing adult backstory is preserved. RimWorld persists the chosen definitions through the pawn's normal story tracker.

The configured later-emerging categories use approximate gameplay cutoffs: private-school/itinerant-scholar roles at 600 BCE, ironworking at 600 BCE, reform and commandery clerks at 450 BCE, and cavalry/diplomatic strategists at 400 BCE. These dates are selector availability thresholds, not claims that an occupation began everywhere in that exact year. Ironworking remains regionally variable and disputed in the technology system.

The generation patch runs at the common vanilla biography-and-name entry point. A third-party mod that bypasses this entry point or replaces a pawn's story after it returns can still provide its own backstory. Full third-party historical backstory registration is not implemented in this phase.
