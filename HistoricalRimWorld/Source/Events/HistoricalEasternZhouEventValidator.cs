using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Events
{
    /// <summary>Startup database audit for HR-011G. Runtime execution remains owned by HR-006.</summary>
    public static class HistoricalEasternZhouEventValidator
    {
        public static List<string> ValidateAll()
        {
            List<string> errors = new List<string>();
            HistoricalScenarioDef scenario = DefDatabase<HistoricalScenarioDef>.GetNamedSilentFail("HR_Scenario_EasternZhou_770BCE");
            if (scenario == null) return new List<string> { "HR-011G scenario Def is missing." };

            List<HistoricalEventDef> historical = DefDatabase<HistoricalEventDef>.AllDefsListForReading
                .Where(item => item.scenario?.defName == scenario.defName
                    && item.activationMode != HistoricalEventActivationMode.DevelopmentOnly
                    && item.activationMode != HistoricalEventActivationMode.LocalTemplate).ToList();
            List<HistoricalContextDef> contexts = DefDatabase<HistoricalContextDef>.AllDefsListForReading
                .Where(item => item.scenario?.defName == scenario.defName).ToList();
            List<HistoricalLocalEventDef> local = DefDatabase<HistoricalLocalEventDef>.AllDefsListForReading
                .Where(item => item.scenario?.defName == scenario.defName).ToList();
            List<HistoricalStorylineDef> chains = DefDatabase<HistoricalStorylineDef>.AllDefsListForReading
                .Where(item => item.scenario?.defName == scenario.defName).ToList();

            if (historical.Count + contexts.Count != 15)
                errors.Add("HR-011G requires exactly 15 historical and context entries; found " + (historical.Count + contexts.Count) + ".");
            if (local.Count != 20) errors.Add("HR-011G requires 20 local event templates; found " + local.Count + ".");
            if (chains.Count != 5) errors.Add("HR-011G requires five story chains; found " + chains.Count + ".");

            foreach (HistoricalEventDef item in historical)
            {
                errors.AddRange(HistoricalEventValidator.ValidateDef(item).Select(error => item.defName + ": " + error));
                if (string.IsNullOrWhiteSpace(item.sourceNotes)) errors.Add(item.defName + " is missing source notes.");
                if (item.historicalDate == null) continue;
                int year = item.historicalDate.AstronomicalYear;
                if (year < -770 || year > -739) errors.Add(item.defName + " is outside the HR-011G chronology window.");
                if (item.activationMode == HistoricalEventActivationMode.InitialBackground
                    && item.historicalDate.AstronomicalYear > scenario.startAstronomicalYear)
                    errors.Add(item.defName + " is initial background dated after scenario start and could be misplayed.");
            }
            HistoricalEventDef xie = DefDatabase<HistoricalEventDef>.GetNamedSilentFail("HR_Event_XieKingDeath_750BCE");
            if (xie != null && (xie.evidenceLevel != HistoricalEvidenceLevel.Disputed
                || xie.chronologyTradition != HistoricalChronologyTradition.TraditionalReceivedChronology))
                errors.Add("The default King Xie entry must remain disputed and tagged to the traditional default chronology.");
            if (DefDatabase<HistoricalEventDef>.GetNamedSilentFail("HR_Event_JinQuwoEnfeoffment_745BCE") is HistoricalEventDef quwo
                && quwo.historicalDate?.AstronomicalYear != -744)
                errors.Add("The Jin Quwo grant must use astronomical year -744 (traditional 745 BCE) at year precision.");

            foreach (HistoricalContextDef context in contexts)
                errors.AddRange(context.ConfigErrors().Select(error => context.defName + ": " + error));
            foreach (HistoricalLocalEventDef item in local)
            {
                errors.AddRange(item.ConfigErrors().Select(error => item.defName + ": " + error));
                if (item.historicalEvent?.provenance != HistoricalEventProvenance.FictionalLocalEvent)
                    errors.Add(item.defName + " must be marked as a fictional local event in its HR-006 event template.");
                if (item.cooldownDays < 1 || item.maxAppearances < 1) errors.Add(item.defName + " has invalid cooldown or appearance limits.");
                foreach (HistoricalLocalEventChoice choice in item.choices ?? new List<HistoricalLocalEventChoice>())
                {
                    if (choice == null) continue;
                    if (choice.conditions != null)
                        foreach (HistoricalEventCondition condition in choice.conditions)
                            if (condition != null && !condition.ValidateData(out string reason)) errors.Add(item.defName + "/" + choice.choiceId + ": " + reason);
                    if (choice.outcome == HistoricalLocalEventOutcome.RevealHistoricalEvent
                        && DefDatabase<HistoricalEventDef>.GetNamedSilentFail(choice.historicalEventId) == null)
                        errors.Add(item.defName + "/" + choice.choiceId + " references a missing history event.");
                }
            }
            foreach (HistoricalStorylineDef chain in chains)
                errors.AddRange(chain.ConfigErrors().Select(error => chain.defName + ": " + error));

            var duplicateRelicCreates = local.SelectMany(item => (item.choices ?? new List<HistoricalLocalEventChoice>())
                    .Where(choice => choice?.outcome == HistoricalLocalEventOutcome.CreateRelic)
                    .Select(choice => new { item.defName, choice.relicDefName }))
                .GroupBy(item => item.relicDefName, StringComparer.Ordinal).Where(group => group.Key != null && group.Count() > 1);
            foreach (var duplicate in duplicateRelicCreates)
                errors.Add("A unique relic is configured as a creation reward more than once: " + duplicate.Key + ".");

            if (historical.Any(item => item.activationMode == HistoricalEventActivationMode.InitialBackground
                && item.actions != null && item.actions.Count > 0))
                errors.Add("Initial historical background must not mutate politics during scenario initialization.");
            return errors.Distinct(StringComparer.Ordinal).ToList();
        }
    }
}
