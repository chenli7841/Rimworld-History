using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.World
{
    public sealed class HistoricalWorldRevealSetupDef : Def
    {
        public HistoricalScenarioDef scenario;
        public HistoricalWorldMapMode mapMode = HistoricalWorldMapMode.PrototypeWorld;
        public int mapVersion = 1;
        public List<HistoricalInitialRevealRecord> regions;
        public List<HistoricalRegionUnlockRule> unlockRules = new List<HistoricalRegionUnlockRule>();
        public string sourceNotes;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            foreach (string error in HistoricalWorldAccessValidator.ValidateSetup(this)) yield return error;
        }
    }

    public sealed class HistoricalInitialRevealRecord
    {
        public HistoricalRegionDef region;
        public HistoricalRegionAccess access;
    }

    public sealed class HistoricalRegionUnlockRule
    {
        public HistoricalRegionDef region;
        public HistoricalDateReference earliestDate;
        public string requiredHistoricalEventId;
        public HistoricalRegionAccess targetAccess = HistoricalRegionAccess.Unexplored;
    }
}
