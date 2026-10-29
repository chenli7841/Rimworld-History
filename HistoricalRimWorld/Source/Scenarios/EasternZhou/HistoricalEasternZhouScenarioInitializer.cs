using System;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Relics;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.Technology;
using HistoricalRimWorld.World;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Scenarios.EasternZhou
{
    public static class HistoricalEasternZhouScenarioInitializer
    {
        public static bool TryInitialize(HistoricalPlayableScenarioSetupDef setup, out string error)
        {
            error = null;
            if (setup?.scenario == null || Current.Game == null || Current.Game.CurrentMap == null)
                return Fail("A loaded map and valid Eastern Zhou playable setup are required.", out error);
            if (!HistoricalEasternZhouScenarioValidator.TryValidate(setup, out error)) return false;
            if (setup.startingPopulation > 0 && Current.Game.CurrentMap.mapPawns.FreeColonists.Count != setup.startingPopulation)
                return Fail("The selected vanilla scenario did not generate the configured starting population of " + setup.startingPopulation + ".", out error);

            if (!HistoricalScenarioManager.TryInitializeScenario(setup.scenario, out error)) return false;
            if (!HistoricalWorldRevealManager.TryAssignStartingMapToRegion(Current.Game.CurrentMap,
                setup.defaultStartingRegion.defName, out error)) return false;

            string communityId = HistoricalTechnologyManager.GetPlayerCommunityId();
            foreach (string technologyId in setup.startingTechnologyIds ?? Enumerable.Empty<string>())
            {
                HistoricalTechnologyState state = HistoricalTechnologyManager.PlayerTechnologies
                    .FirstOrDefault(item => item.TechnologyId == technologyId);
                if (state == null) return Fail("Configured starting technology is missing from the initialized community: " + technologyId + ".", out error);
                if (state.Adoption == HistoricalTechnologyAdoption.Adopted) continue;
                if (!HistoricalTechnologyManager.TryAdoptTechnology(communityId, technologyId,
                    HistoricalTechnologyAcquisitionSource.Development, out error)) return false;
            }

            HistoricalDate date = setup.scenario.GetStartDate();
            HistoricalGenealogyGameComponent genealogy = Current.Game.GetComponent<HistoricalGenealogyGameComponent>();
            foreach (Pawn pawn in Current.Game.CurrentMap.mapPawns.FreeColonists.Where(item => item != null && item.RaceProps.Humanlike))
            {
                if (genealogy == null || !genealogy.TryRegisterPawn(pawn, date, out _, out error)) return false;
            }

            Thing relic = Current.Game.CurrentMap.listerThings.AllThings
                .FirstOrDefault(item => item?.def?.defName == "HR_Relic_FamilyVessel" && item.TryGetComp<CompHistoricalRelic>()?.IsLinked == false);
            if (relic != null)
            {
                Pawn founder = Current.Game.CurrentMap.mapPawns.FreeColonists.FirstOrDefault(item => item != null && item.RaceProps.Humanlike);
                string founderId = founder != null && HistoricalGenealogyManager.TryGetPersonId(founder, out string id) ? id : null;
                if (!HistoricalRelicManager.TryDesignateAsRelic(relic, "HR_RelicDef_FamilyVessel", founderId, founderId, out _, out error)) return false;
            }
            else if (!HistoricalRelicManager.Records.Any(item => item.RelicDefName == "HR_RelicDef_FamilyVessel"))
            {
                return Fail("The scenario's configured family vessel was not found on the starting map.", out error);
            }

            if (!HistoricalScenarioProgressManager.TryInitialize(setup, out error)) return false;
            HistoricalChronicleManager.TryAddLocalExperience("HR_EasternZhou_ChapterStarted",
                "HR_EZ_ChapterStartedTitle", "HR_EZ_ChapterStartedDescription",
                setup.defaultStartingRegion.defName, setup.defaultStartingPolity.defName,
                HistoricalEvidenceLevel.Speculative, "Local gameplay chronicle entry; not an attested historical event.", out _);
            Log.Message("[Historical RimWorld] Eastern Zhou: The Royal Court Moves East initialized for the starting village.");
            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            Log.Error("[Historical RimWorld] Eastern Zhou scenario startup failed: " + message);
            return false;
        }
    }

    public sealed class HistoricalEasternZhouScenarioStartupGameComponent : GameComponent
    {
        private bool startupAttempted;
        private int nextRetryTick;

        public HistoricalEasternZhouScenarioStartupGameComponent(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            TryStartup();
        }

        public override void GameComponentTick()
        {
            if (!startupAttempted && (Find.TickManager?.TicksGame ?? 0) >= nextRetryTick) TryStartup();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startupAttempted, "easternZhouStartupAttempted", false);
        }

        private void TryStartup()
        {
            Scenario selected = Current.Game?.Scenario;
            if (selected == null) return;
            HistoricalPlayableScenarioSetupDef setup = DefDatabase<HistoricalPlayableScenarioSetupDef>.AllDefsListForReading
                .FirstOrDefault(item => item.vanillaScenarioName == selected.name);
            if (setup == null) return;
            if (HistoricalScenarioProgressManager.IsPlayableScenarioActive)
            {
                startupAttempted = true;
                return;
            }
            if (Current.Game.CurrentMap == null) return;
            startupAttempted = HistoricalEasternZhouScenarioInitializer.TryInitialize(setup, out _);
            if (!startupAttempted) nextRetryTick = (Find.TickManager?.TicksGame ?? 0) + 2500;
        }
    }
}
