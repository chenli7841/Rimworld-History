using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Events.Conditions;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Events
{
    public enum HistoricalChainStageTrigger { LocalEvent, ObservedBirth, FamilyDeath, ScenarioEnd }

    public sealed class HistoricalEventChainStageDef
    {
        public string stageId;
        public string eventDefName;
        public List<string> prerequisiteStageIds = new List<string>();
        public bool optional;
        public int optionalAfterYears;
        public HistoricalChainStageTrigger trigger = HistoricalChainStageTrigger.LocalEvent;
        public bool repeatable;
        public List<HistoricalEventCondition> conditions = new List<HistoricalEventCondition>();
    }

    /// <summary>HR-006 story progression definition; local event execution still belongs to HistoricalEventScheduler.</summary>
    public sealed class HistoricalStorylineDef : Def
    {
        public HistoricalScenarioDef scenario;
        public string startConditions;
        public string longTermConsequences;
        public List<HistoricalEventChainStageDef> stages = new List<HistoricalEventChainStageDef>();
        public string sourceNotes;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (scenario == null) yield return "scenario is required.";
            if (string.IsNullOrWhiteSpace(startConditions)) yield return "startConditions is required.";
            if (string.IsNullOrWhiteSpace(longTermConsequences)) yield return "longTermConsequences is required.";
            if (stages == null || stages.Count < 3) { yield return "A storyline requires at least three stages."; yield break; }
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < stages.Count; i++)
            {
                HistoricalEventChainStageDef stage = stages[i];
                if (stage == null) { yield return "A storyline contains a null stage."; continue; }
                if (string.IsNullOrWhiteSpace(stage.stageId) || !ids.Add(stage.stageId)) yield return "Stage IDs must be non-empty and unique.";
                if (!Enum.IsDefined(typeof(HistoricalChainStageTrigger), stage.trigger)) yield return "A stage has an unknown trigger.";
                if (stage.optionalAfterYears < 0) yield return "optionalAfterYears cannot be negative.";
                foreach (HistoricalEventCondition condition in stage.conditions ?? new List<HistoricalEventCondition>())
                {
                    if (condition == null) { yield return "A stage contains a null condition."; continue; }
                    if (!condition.ValidateData(out string conditionError)) yield return conditionError;
                }
                if (stage.trigger == HistoricalChainStageTrigger.LocalEvent)
                {
                    HistoricalLocalEventDef eventDef = DefDatabase<HistoricalLocalEventDef>.GetNamedSilentFail(stage.eventDefName);
                    if (eventDef == null) yield return "Missing local event stage " + stage.eventDefName + ".";
                    else if (eventDef.scenario?.defName != scenario?.defName) yield return "A local event stage belongs to another scenario.";
                }
                else if (!string.IsNullOrWhiteSpace(stage.eventDefName)) yield return "Only LocalEvent stages may name an event Def.";
                if (stage.prerequisiteStageIds != null && stage.prerequisiteStageIds.Any(id => string.IsNullOrWhiteSpace(id) || id == stage.stageId))
                    yield return "Stage prerequisites cannot be empty or self-referential.";
            }
            foreach (HistoricalEventChainStageDef stage in stages.Where(item => item != null))
                foreach (string prerequisite in stage.prerequisiteStageIds ?? new List<string>())
                    if (!ids.Contains(prerequisite)) yield return "Stage " + stage.stageId + " references missing prerequisite " + prerequisite + ".";
            if (HasCycle(stages)) yield return "Storyline stage prerequisites contain a cycle.";
        }

        private static bool HasCycle(IList<HistoricalEventChainStageDef> list)
        {
            Dictionary<string, HistoricalEventChainStageDef> byId = list.Where(item => item != null && !string.IsNullOrWhiteSpace(item.stageId))
                .GroupBy(item => item.stageId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal), visited = new HashSet<string>(StringComparer.Ordinal);
            Func<string, bool> visit = null;
            visit = id =>
            {
                if (visiting.Contains(id)) return true;
                if (visited.Contains(id) || !byId.TryGetValue(id, out HistoricalEventChainStageDef node)) return false;
                visiting.Add(id);
                foreach (string prerequisite in node.prerequisiteStageIds ?? new List<string>()) if (visit(prerequisite)) return true;
                visiting.Remove(id); visited.Add(id); return false;
            };
            return byId.Keys.Any(visit);
        }
    }
}
