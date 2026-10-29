using System.Linq;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Culture;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Relics;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.Technology;
using HistoricalRimWorld.World;
using Verse;

namespace HistoricalRimWorld.Events.Conditions
{
    public sealed class Condition_HistoricalDateRange : HistoricalEventCondition
    {
        public int? startAstronomicalYear;
        public int? endAstronomicalYear;
        public override bool ValidateData(out string error) => ValidateRange(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => ValidateRange(out error);
        public override bool Evaluate(HistoricalEventContext context) => context != null
            && (!startAstronomicalYear.HasValue || context.CurrentDate.AstronomicalYear >= startAstronomicalYear.Value)
            && (!endAstronomicalYear.HasValue || context.CurrentDate.AstronomicalYear <= endAstronomicalYear.Value);
        private bool ValidateRange(out string error)
        {
            if (!startAstronomicalYear.HasValue && !endAstronomicalYear.HasValue
                || startAstronomicalYear.HasValue && endAstronomicalYear.HasValue && startAstronomicalYear > endAstronomicalYear)
            { error = "A valid astronomical year range is required."; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_CommunityRegion : HistoricalEventCondition
    {
        public string regionId;
        public override bool ValidateData(out string error) => Valid(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Valid(out error);
        public override bool Evaluate(HistoricalEventContext context) => HistoricalLocalConditionContext.Community(context, out HistoricalLocalCommunityState state) && state.RegionId == regionId;
        private bool Valid(out string error)
        {
            if (DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null) { error = "Unknown HistoricalRegionDef: " + regionId; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_CommunityPolity : HistoricalEventCondition
    {
        public string polityId;
        public override bool ValidateData(out string error) => Valid(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Valid(out error);
        public override bool Evaluate(HistoricalEventContext context) => HistoricalLocalConditionContext.Community(context, out HistoricalLocalCommunityState state)
            && (state.AllegiancePolityId == polityId || state.AdministrativePolityId == polityId || state.EffectiveControllerId == polityId);
        private bool Valid(out string error)
        {
            if (DefDatabase<HistoricalStateDef>.GetNamedSilentFail(polityId) == null) { error = "Unknown HistoricalStateDef: " + polityId; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_CommunityPopulation : HistoricalEventCondition
    {
        public int minimum;
        public int maximum = int.MaxValue;
        public override bool ValidateData(out string error) => Valid(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Valid(out error);
        public override bool Evaluate(HistoricalEventContext context) => context?.Map?.mapPawns != null
            && context.Map.mapPawns.FreeColonists.Count >= minimum && context.Map.mapPawns.FreeColonists.Count <= maximum;
        private bool Valid(out string error)
        {
            if (minimum < 0 || maximum < minimum) { error = "Invalid community population range."; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_CommunityResource : HistoricalEventCondition
    {
        public ThingDef resource;
        public int minimumAmount;
        public override bool ValidateData(out string error) => Valid(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Valid(out error);
        public override bool Evaluate(HistoricalEventContext context) => context?.Map?.listerThings != null && resource != null
            && context.Map.listerThings.ThingsOfDef(resource).Where(item => !item.Destroyed).Sum(item => item.stackCount) >= minimumAmount;
        private bool Valid(out string error)
        {
            if (resource == null || minimumAmount < 0) { error = "A valid resource and non-negative minimumAmount are required."; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_CommunityTechnology : HistoricalEventCondition
    {
        public string technologyId;
        public HistoricalTechnologyAdoption minimumAdoption = HistoricalTechnologyAdoption.Known;
        public override bool ValidateData(out string error) => Valid(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Valid(out error);
        public override bool Evaluate(HistoricalEventContext context) => HistoricalTechnologyManager.PlayerTechnologies
            .Any(item => item.TechnologyId == technologyId && (int)item.Adoption >= (int)minimumAdoption);
        private bool Valid(out string error)
        {
            if (DefDatabase<HistoricalTechnologyDef>.GetNamedSilentFail(technologyId) == null
                || !System.Enum.IsDefined(typeof(HistoricalTechnologyAdoption), minimumAdoption))
            { error = "Unknown technology Def or adoption value: " + technologyId; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_FamilyExists : HistoricalEventCondition
    {
        public bool requireLivingMember = true;
        public override bool ValidateData(out string error) { error = null; return true; }
        public override bool Validate(HistoricalEventContext context, out string error) { error = null; return true; }
        public override bool Evaluate(HistoricalEventContext context) => HistoricalGenealogyManager.Families
            .Any(family => !requireLivingMember || family.MemberPersonIds.Any(id => HistoricalGenealogyManager.FindPerson(id)?.IsAlive == true));
    }

    public sealed class Condition_PersonAge : HistoricalEventCondition
    {
        public int minimumAgeYears;
        public int maximumAgeYears = int.MaxValue;
        public string familyId;
        public override bool ValidateData(out string error) => Valid(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Valid(out error);
        public override bool Evaluate(HistoricalEventContext context) => HistoricalGenealogyManager.Persons.Any(person => person.IsAlive
            && (string.IsNullOrWhiteSpace(familyId) || person.FamilyId == familyId)
            && HistoricalCharacterManager.TryGetCurrentHistoricalAge(person.PersonId, out int age)
            && age >= minimumAgeYears && age <= maximumAgeYears);
        private bool Valid(out string error)
        {
            if (minimumAgeYears < 0 || maximumAgeYears < minimumAgeYears) { error = "Invalid historical age range."; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_RelicOwned : HistoricalEventCondition
    {
        public string relicDefName;
        public override bool ValidateData(out string error) => Valid(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Valid(out error);
        public override bool Evaluate(HistoricalEventContext context) => context?.Map?.listerThings != null
            && HistoricalRelicManager.Records.Any(record => record.RelicDefName == relicDefName && !record.IsLost && !record.IsDestroyed
                && HistoricalRelicManager.Find(record.RelicId) != null)
            && DefDatabase<HistoricalRelicDef>.GetNamedSilentFail(relicDefName)?.baseThingDef is ThingDef def
            && context.Map.listerThings.ThingsOfDef(def).Any(thing => thing.TryGetComp<CompHistoricalRelic>()?.IsLinked == true);
        private bool Valid(out string error)
        {
            if (DefDatabase<HistoricalRelicDef>.GetNamedSilentFail(relicDefName) == null) { error = "Unknown HistoricalRelicDef: " + relicDefName; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_HistoricalEventCompleted : HistoricalEventCondition
    {
        public string eventDefName;
        public override bool ValidateData(out string error) => Valid(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Valid(out error);
        public override bool Evaluate(HistoricalEventContext context) => HistoricalEventManager.WasExecuted(eventDefName);
        private bool Valid(out string error)
        {
            if (DefDatabase<HistoricalEventDef>.GetNamedSilentFail(eventDefName) == null) { error = "Unknown HistoricalEventDef: " + eventDefName; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_RegionKnowledge : HistoricalEventCondition
    {
        public string regionId;
        public HistoricalRegionAccess minimumAccess = HistoricalRegionAccess.Revealed;
        public override bool ValidateData(out string error) => Valid(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Valid(out error);
        public override bool Evaluate(HistoricalEventContext context) => HistoricalWorldRevealManager.TryGetRegionAccess(regionId, out HistoricalRegionAccess access)
            && (int)access >= (int)minimumAccess;
        private bool Valid(out string error)
        {
            if (DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null || !System.Enum.IsDefined(typeof(HistoricalRegionAccess), minimumAccess))
            { error = "Unknown region or reveal access value: " + regionId; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_EventCooldown : HistoricalEventCondition
    {
        public string localEventDefName;
        public override bool ValidateData(out string error) => Valid(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Valid(out error);
        public override bool Evaluate(HistoricalEventContext context) => HistoricalEventManager.LocalEventRecords
            .FirstOrDefault(item => item.eventId == localEventDefName) is HistoricalLocalEventRuntimeRecord record
            && record.nextEligibleAbsoluteDay <= context.CurrentDate.AbsoluteDay;
        private bool Valid(out string error)
        {
            if (DefDatabase<HistoricalLocalEventDef>.GetNamedSilentFail(localEventDefName) == null) { error = "Unknown HistoricalLocalEventDef: " + localEventDefName; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_MarriageCandidate : HistoricalEventCondition
    {
        public int minimumAgeYears = 18;
        public override bool ValidateData(out string error) => Valid(out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Valid(out error);
        public override bool Evaluate(HistoricalEventContext context) => HistoricalLocalEventEligibility.HasMarriageCandidate(context?.Map, minimumAgeYears);
        private bool Valid(out string error)
        {
            if (minimumAgeYears < 18) { error = "Marriage eligibility cannot be configured below age 18."; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_FamilyDeath : HistoricalEventCondition
    {
        public override bool ValidateData(out string error) { error = null; return true; }
        public override bool Validate(HistoricalEventContext context, out string error) { error = null; return true; }
        public override bool Evaluate(HistoricalEventContext context) => HistoricalGenealogyManager.Persons.Any(person => !person.IsAlive
            && person.DeathDate.HasValue && (string.IsNullOrWhiteSpace(person.FamilyId)
                || HistoricalGenealogyManager.FindFamily(person.FamilyId) != null));
    }

    internal static class HistoricalLocalConditionContext
    {
        internal static bool Community(HistoricalEventContext context, out HistoricalLocalCommunityState community)
        {
            community = null;
            string id = context?.CommunityId ?? HistoricalTechnologyManager.GetPlayerCommunityId();
            return !string.IsNullOrWhiteSpace(id) && HistoricalPoliticsManager.TryGetCommunity(id, out community);
        }
    }
}
