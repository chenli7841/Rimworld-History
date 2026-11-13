using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Characters
{
    public sealed class HistoricalCharacterDef : Def
    {
        public string historicalCharacterId;
        public string historicalName;
        public string historicalFactionId;
        public string historicalRole;
        public string historicalScenarioId;
        public string relatedEventId;
        public string pawnKindDefName;
        public int minimumEstimatedAge;
        public int maximumEstimatedAge;
        public bool generateOnBattlefield;
        public bool preserveIdentityAcrossEvents = true;
        public Gender gender = Gender.Male;
        public List<string> aliases = new List<string>();
        public string sourceNotes;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (string.IsNullOrWhiteSpace(historicalCharacterId)) yield return "historicalCharacterId is required.";
            if (string.IsNullOrWhiteSpace(historicalName)) yield return "historicalName is required.";
            if (string.IsNullOrWhiteSpace(historicalScenarioId)) yield return "historicalScenarioId is required.";
            if (minimumEstimatedAge < 0 || maximumEstimatedAge < minimumEstimatedAge) yield return "Estimated age range is invalid.";
            if (generateOnBattlefield && string.IsNullOrWhiteSpace(pawnKindDefName)) yield return "A pawnKindDefName is required for generated characters.";
            if (!string.IsNullOrWhiteSpace(historicalCharacterId)
                && DefDatabase<HistoricalCharacterDef>.AllDefsListForReading.Count(item => item != null
                    && item.historicalCharacterId == historicalCharacterId) > 1)
                yield return "historicalCharacterId must be unique.";
        }
    }

    public sealed class HistoricalArmyDef : Def
    {
        public string armyId;
        public string historicalEventId;
        public string historicalScenarioId;
        public string factionId;
        public string armyRole;
        public int targetPawnCount;
        public string deploymentZoneId;
        public bool persistentAcrossMapUnload = true;
        public List<HistoricalUnitGroupDef> unitGroups = new List<HistoricalUnitGroupDef>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (string.IsNullOrWhiteSpace(armyId)) yield return "armyId is required.";
            if (string.IsNullOrWhiteSpace(historicalEventId)) yield return "historicalEventId is required.";
            if (string.IsNullOrWhiteSpace(historicalScenarioId)) yield return "historicalScenarioId is required.";
            if (string.IsNullOrWhiteSpace(factionId)) yield return "factionId is required.";
            if (string.IsNullOrWhiteSpace(deploymentZoneId)) yield return "deploymentZoneId is required.";
            if (targetPawnCount < 1) yield return "targetPawnCount must be positive.";
            if (unitGroups == null || unitGroups.Count == 0) yield return "At least one unit group is required.";
            else
            {
                if (unitGroups.Where(item => item != null).Sum(item => item.count) != targetPawnCount)
                    yield return "targetPawnCount must equal the configured unit-group count total.";
                foreach (HistoricalUnitGroupDef group in unitGroups.Where(item => item != null))
                {
                    if (string.IsNullOrWhiteSpace(group.groupId)) yield return "Every unit group needs a groupId.";
                    if (string.IsNullOrWhiteSpace(group.pawnKindDefName)) yield return "Every unit group needs a pawnKindDefName.";
                    if (group.count < 1) yield return "Unit group count must be positive.";
                    if (group.maximumAge < group.minimumAge || group.minimumAge < 0) yield return "Unit group age range is invalid.";
                    if (group.deploymentDepth < 0f || group.deploymentDepth > 1f) yield return "Unit group deploymentDepth must be between 0 and 1.";
                }
            }
            if (!string.IsNullOrWhiteSpace(armyId)
                && DefDatabase<HistoricalArmyDef>.AllDefsListForReading.Count(item => item != null && item.armyId == armyId) > 1)
                yield return "armyId must be unique.";
        }
    }

    public sealed class HistoricalUnitGroupDef
    {
        public string groupId;
        public string unitRole;
        public string pawnKindDefName;
        public int count;
        public int minimumAge = 18;
        public int maximumAge = 45;
        public float deploymentDepth = 0.5f;
    }
}
