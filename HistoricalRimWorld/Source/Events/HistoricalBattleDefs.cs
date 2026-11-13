using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Characters;
using Verse;

namespace HistoricalRimWorld.Events
{
    public enum HistoricalBattlePhase
    {
        NotStarted,
        Preparing,
        Deployment,
        Engagement,
        RoyalRetreat,
        FinalCollapse,
        DeathScene,
        Aftermath,
        Completed
    }

    /// <summary>Configuration that binds a battle presentation to existing event and army Defs.</summary>
    public sealed class HistoricalBattleDef : Def
    {
        public string battleId;
        public string historicalEventId;
        public string landmarkId;
        public string mapTemplateId;
        public string royalArmyId;
        public string opposingArmyId;
        public string deploymentStageId = "MilitaryCrisis";
        public string retreatStageId = "RoyalDefeat";
        public string deathStageId = "KingYouDeath";
        public string aftermathStageId = "Aftermath";
        public string completionStageId = "Completed";
        public string royalFactionId;
        public string opposingFactionId;
        public string kingCharacterId;
        public string kingFateDefName;
        public string royalRetreatZoneId = "RoyalRetreatCorridor";
        public string deathSceneZoneId = "HistoricalDeathSceneZone";
        public bool mandatoryRoyalDefeat = true;
        public bool progressWithoutPlayer = true;
        public int combatOrderIntervalTicks = 180;
        public int moraleCheckIntervalTicks = 250;
        public int deathSceneBudgetTicks = 1800;
        public int freshRemainsDays = 5;
        public int weatheredRemainsDays = 15;
        public int overgrownRemainsDays = 180;
        public int ruinRemainsDays = 360;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (string.IsNullOrWhiteSpace(battleId)) yield return "battleId is required.";
            if (DefDatabase<HistoricalEventDef>.AllDefsListForReading.All(item => item == null || item.eventId != historicalEventId)) yield return "historicalEventId must resolve to an existing historical event.";
            if (DefDatabase<HistoricalArmyDef>.GetNamedSilentFail(royalArmyId) == null) yield return "royalArmyId must resolve to an existing HistoricalArmyDef.";
            if (DefDatabase<HistoricalArmyDef>.GetNamedSilentFail(opposingArmyId) == null) yield return "opposingArmyId must resolve to an existing HistoricalArmyDef.";
            if (string.IsNullOrWhiteSpace(landmarkId) || string.IsNullOrWhiteSpace(mapTemplateId)) yield return "landmarkId and mapTemplateId are required.";
            if (combatOrderIntervalTicks < 60 || moraleCheckIntervalTicks < 60 || deathSceneBudgetTicks < 60) yield return "Battle tick intervals and death scene budget must be at least 60 ticks.";
            if (freshRemainsDays < 0 || weatheredRemainsDays < freshRemainsDays
                || overgrownRemainsDays < weatheredRemainsDays || ruinRemainsDays < overgrownRemainsDays)
                yield return "Battlefield aging durations must be non-negative and ordered.";
            if (!string.IsNullOrWhiteSpace(kingFateDefName) && DefDatabase<HistoricalFateDef>.GetNamedSilentFail(kingFateDefName) == null) yield return "kingFateDefName must resolve to an existing HistoricalFateDef.";
        }
    }
}
