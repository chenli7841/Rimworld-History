using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using HistoricalRimWorld.Technology;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Events
{
    public enum HistoricalLocalEventOutcome { NoMaterialChange, GainResource, SpendResource, ChangeCommunityPosture, AddColonist, RevealRegion, IncreaseCommunityCulturePresence, CreateRelic, AdoptTechnology, RevealHistoricalEvent, CreateMarriage, InheritRelic, TradeResource }

    public sealed class HistoricalLocalEventChoice
    {
        public string choiceId;
        public string label;
        public string description;
        public List<HistoricalEventCondition> conditions = new List<HistoricalEventCondition>();
        public HistoricalLocalEventOutcome outcome;
        public ThingDef resource;
        public int amount;
        public HistoricalCommunityPosture posture;
        public bool terminateStoryline;
        public string regionId;
        public string cultureId;
        public float cultureAmount;
        public string relicDefName;
        public string technologyId;
        public string historicalEventId;
        public ThingDef secondaryResource;
        public int secondaryAmount;
        public int minimumMarriageAge = 18;
        public HistoricalCommunityPosture? communityPosture;
        public bool createChronicleEntry = true;
        public List<HistoricalLocalEventResourceCost> resourceCosts = new List<HistoricalLocalEventResourceCost>();
    }

    public sealed class HistoricalLocalEventResourceCost
    {
        public ThingDef resource;
        public int amount;
    }

    /// <summary>Interactive, scenario-scoped local content driven by the existing HR-006 event scheduler.</summary>
    public sealed class HistoricalLocalEventDef : Def
    {
        public HistoricalScenarioDef scenario;
        public HistoricalEventDef historicalEvent;
        public string storylineId;
        public int storylineStage = -1;
        public int cooldownDays = 120;
        public int maxAppearances = 1;
        public int weight = 1;
        public bool enabled = true;
        public int minimumColonists = 1;
        public bool requiresMedicalCare;
        public bool requiresFamily;
        public ThingDef requiredResource;
        public int requiredResourceCount;
        public string triggerNotes;
        public string requiredHistoricalEventId;
        public string regionId;
        public string polityId;
        public string sourceNotes;
        public List<HistoricalEventCondition> conditions = new List<HistoricalEventCondition>();
        public List<HistoricalLocalEventChoice> choices = new List<HistoricalLocalEventChoice>();

        public bool IsStoryStage => !string.IsNullOrWhiteSpace(storylineId) && storylineStage >= 0;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (scenario == null) yield return "scenario is required.";
            if (historicalEvent == null || historicalEvent.activationMode != HistoricalEventActivationMode.LocalTemplate
                || historicalEvent.scenario?.defName != scenario?.defName)
                yield return "historicalEvent must reference this scenario's LocalTemplate HistoricalEventDef.";
            if (enabled && string.IsNullOrWhiteSpace(triggerNotes)) yield return "Enabled local events require triggerNotes.";
            if (cooldownDays < 1) yield return "cooldownDays must be positive.";
            if (maxAppearances < 1) yield return "maxAppearances must be positive.";
            if (weight < 1) yield return "weight must be positive.";
            if (minimumColonists < 0 || requiredResourceCount < 0) yield return "Trigger thresholds cannot be negative.";
            if (requiredResourceCount > 0 && requiredResource == null) yield return "requiredResourceCount requires requiredResource.";
            if (!string.IsNullOrWhiteSpace(regionId) && DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null)
                yield return "regionId references a missing HistoricalRegionDef.";
            if (!string.IsNullOrWhiteSpace(polityId) && DefDatabase<HistoricalStateDef>.GetNamedSilentFail(polityId) == null)
                yield return "polityId references a missing HistoricalStateDef.";
            foreach (HistoricalEventCondition condition in conditions ?? new List<HistoricalEventCondition>())
            {
                if (condition == null) { yield return "conditions contains a null item."; continue; }
                if (!condition.ValidateData(out string conditionError)) yield return conditionError;
            }
            if (IsStoryStage && storylineStage > 0 && DefDatabase<HistoricalLocalEventDef>.AllDefsListForReading
                .All(item => item.scenario?.defName != scenario?.defName || item.storylineId != storylineId || item.storylineStage == storylineStage - 1))
                yield return "A story stage must have a preceding stage in the same scenario.";
            if (requiredHistoricalEventId != null && DefDatabase<HistoricalEventDef>.GetNamedSilentFail(requiredHistoricalEventId) == null)
                yield return "requiredHistoricalEventId references a missing HistoricalEventDef.";
            if (choices == null || choices.Count < 2) yield return "At least two player choices are required.";
            else foreach (HistoricalLocalEventChoice choice in choices)
            {
                if (choice == null) { yield return "choices contains a null item."; continue; }
                if (string.IsNullOrWhiteSpace(choice.label) || string.IsNullOrWhiteSpace(choice.description))
                    yield return "Each choice requires localized label and description keys.";
                if (string.IsNullOrWhiteSpace(choice.choiceId)) yield return "Each choice requires a stable choiceId.";
                foreach (HistoricalEventCondition condition in choice.conditions ?? new List<HistoricalEventCondition>())
                {
                    if (condition == null) { yield return "A choice contains a null condition."; continue; }
                    if (!condition.ValidateData(out string conditionError)) yield return conditionError;
                }
                foreach (HistoricalLocalEventResourceCost cost in choice.resourceCosts ?? new List<HistoricalLocalEventResourceCost>())
                    if (cost == null || cost.resource == null || cost.amount < 1) yield return "Choice resource costs require a resource Def and positive amount.";
                if (!System.Enum.IsDefined(typeof(HistoricalLocalEventOutcome), choice.outcome)) yield return "A choice has an invalid outcome.";
                if ((choice.outcome == HistoricalLocalEventOutcome.GainResource || choice.outcome == HistoricalLocalEventOutcome.SpendResource
                    || choice.outcome == HistoricalLocalEventOutcome.TradeResource)
                    && (choice.resource == null || choice.amount < 1)) yield return "Resource choices require a resource Def and positive amount.";
                if ((choice.secondaryResource == null) != (choice.secondaryAmount == 0)
                    || choice.secondaryAmount < 0) yield return "A secondary trade resource and amount must be specified together.";
                if (choice.secondaryResource != null && choice.outcome != HistoricalLocalEventOutcome.TradeResource)
                    yield return "A secondary output is only valid for TradeResource choices.";
                if (choice.outcome == HistoricalLocalEventOutcome.ChangeCommunityPosture
                    && !System.Enum.IsDefined(typeof(HistoricalCommunityPosture), choice.posture)) yield return "A posture choice has an invalid posture.";
                if (choice.outcome == HistoricalLocalEventOutcome.RevealRegion
                    && (string.IsNullOrWhiteSpace(choice.regionId) || DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(choice.regionId) == null))
                    yield return "A reveal choice requires a valid historical region Def name.";
                if (choice.outcome == HistoricalLocalEventOutcome.IncreaseCommunityCulturePresence
                    && (DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(choice.cultureId) == null || choice.cultureAmount <= 0f))
                    yield return "A culture-presence choice requires a valid culture Def and positive amount.";
                if (choice.outcome == HistoricalLocalEventOutcome.CreateRelic
                    && DefDatabase<HistoricalRimWorld.Relics.HistoricalRelicDef>.GetNamedSilentFail(choice.relicDefName) == null)
                    yield return "A relic choice requires a valid HistoricalRelicDef.";
                if (choice.outcome == HistoricalLocalEventOutcome.InheritRelic
                    && DefDatabase<HistoricalRimWorld.Relics.HistoricalRelicDef>.GetNamedSilentFail(choice.relicDefName) == null)
                    yield return "An inheritance choice requires a valid HistoricalRelicDef.";
                if (choice.outcome == HistoricalLocalEventOutcome.AdoptTechnology
                    && DefDatabase<HistoricalTechnologyDef>.GetNamedSilentFail(choice.technologyId) == null)
                    yield return "A technology choice requires a valid HistoricalTechnologyDef.";
                if (choice.outcome == HistoricalLocalEventOutcome.RevealHistoricalEvent
                    && DefDatabase<HistoricalEventDef>.GetNamedSilentFail(choice.historicalEventId) == null)
                    yield return "A reveal choice requires a valid historical event Def.";
                if (choice.outcome == HistoricalLocalEventOutcome.CreateMarriage && choice.minimumMarriageAge < 18)
                    yield return "The minimum marriage age cannot be below 18 historical years.";
            }
            if (choices != null && choices.Where(item => item != null).Select(item => item.choiceId).Distinct().Count() != choices.Count)
                yield return "choiceId values must be unique within this event.";
        }
    }
}
