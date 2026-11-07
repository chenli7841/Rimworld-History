using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Patches.Technology;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Technology
{
    [DefOf]
    public static class HistoricalBlueprintDefOf
    {
        public static ThingDef HR_HistoricalTechnologyBlueprint;
        static HistoricalBlueprintDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(HistoricalBlueprintDefOf));
    }

    public sealed class CompProperties_HistoricalBlueprint : CompProperties
    {
        public CompProperties_HistoricalBlueprint() => compClass = typeof(CompHistoricalBlueprint);
    }

    public sealed class CompHistoricalBlueprint : ThingComp
    {
        public string ResearchDefName;
        public string OriginFactionId;
        public string OriginFactionName;
        public bool AcquisitionRecorded;
        public float DecodingProgress;

        public ResearchProjectDef Research => string.IsNullOrWhiteSpace(ResearchDefName)
            ? null : DefDatabase<ResearchProjectDef>.GetNamedSilentFail(ResearchDefName);

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ResearchDefName, "historicalBlueprintResearchDefName");
            Scribe_Values.Look(ref OriginFactionId, "historicalBlueprintOriginFactionId");
            Scribe_Values.Look(ref OriginFactionName, "historicalBlueprintOriginFactionName");
            Scribe_Values.Look(ref AcquisitionRecorded, "historicalBlueprintAcquisitionRecorded", false);
            Scribe_Values.Look(ref DecodingProgress, "historicalBlueprintDecodingProgress", 0f);
        }

        public override string CompInspectStringExtra()
        {
            ResearchProjectDef research = Research;
            return research == null ? "HR_Blueprint_InvalidResearch".Translate() : "HR_Blueprint_ForResearch".Translate(research.LabelCap);
        }
    }

    public sealed class HistoricalResearchAuthorization : IExposable
    {
        public string ResearchDefName;
        public bool IsAuthorized;
        public int AuthorizedYear;
        public int AuthorizedGameTick = -1;
        public HistoricalResearchAuthorizationSource Source;
        public string SourceDetails;

        public void Grant(HistoricalResearchAuthorizationSource source, string details)
        {
            IsAuthorized = true;
            Source = source;
            SourceDetails = details;
            AuthorizedGameTick = Find.TickManager?.TicksGame ?? -1;
            AuthorizedYear = HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date) ? date.AstronomicalYear : 0;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ResearchDefName, "researchDefName");
            Scribe_Values.Look(ref IsAuthorized, "isAuthorized", false);
            Scribe_Values.Look(ref AuthorizedYear, "authorizedYear");
            Scribe_Values.Look(ref AuthorizedGameTick, "authorizedGameTick", -1);
            Scribe_Values.Look(ref Source, "source", HistoricalResearchAuthorizationSource.BlueprintDecoded);
            Scribe_Values.Look(ref SourceDetails, "sourceDetails");
        }
    }

    public static class HistoricalBlueprintService
    {
        public const int DefaultDecodingWork = 600;

        public static HistoricalTechnologyGameComponent Component => Current.Game?.GetComponent<HistoricalTechnologyGameComponent>();

        public static HistoricalResearchMode GetMode(ResearchProjectDef project)
        {
            HistoricalTechnologyDef tech = HistoricalTechnologyManager.Technologies.FirstOrDefault(item => item.researchProject == project);
            HistoricalTechExtension extension = project?.GetModExtension<HistoricalTechExtension>();
            return tech?.researchMode ?? extension?.ResearchMode ?? HistoricalResearchMode.Basic;
        }

        public static HistoricalKnowledgeRequirement GetKnowledgeRequirement(ResearchProjectDef project)
        {
            HistoricalTechnologyDef tech = HistoricalTechnologyManager.Technologies.FirstOrDefault(item => item.researchProject == project);
            if (tech != null)
            {
                if (tech.knowledgeRequirement != HistoricalKnowledgeRequirement.None) return tech.knowledgeRequirement;
                return tech.researchMode == HistoricalResearchMode.Historical
                    ? HistoricalKnowledgeRequirement.Blueprint : HistoricalKnowledgeRequirement.None;
            }
            HistoricalTechExtension extension = project?.GetModExtension<HistoricalTechExtension>();
            if (extension != null && extension.KnowledgeRequirement != HistoricalKnowledgeRequirement.None)
                return extension.KnowledgeRequirement;
            return GetMode(project) == HistoricalResearchMode.Historical
                ? HistoricalKnowledgeRequirement.Blueprint : HistoricalKnowledgeRequirement.None;
        }

        public static HistoricalBlueprintStatus GetStatus(ResearchProjectDef research)
        {
            if (research == null) return HistoricalBlueprintStatus.NotOwned;
            if (research.IsFinished || Component?.FindAuthorization(research.defName) != null)
                return HistoricalBlueprintStatus.Decoded;
            return EnumerateAccessibleBlueprints().Any(item => item?.ResearchDefName == research.defName)
                ? HistoricalBlueprintStatus.OwnedButNotDecoded : HistoricalBlueprintStatus.NotOwned;
        }

        public static IEnumerable<CompHistoricalBlueprint> EnumerateAccessibleBlueprints()
        {
            if (Current.Game?.Maps == null) yield break;
            foreach (Map map in Current.Game.Maps)
            {
                if (map == null || map.IsPlayerHome == false) continue;
                List<Thing> things = map.listerThings?.ThingsOfDef(HistoricalBlueprintDefOf.HR_HistoricalTechnologyBlueprint);
                if (things == null) continue;
                foreach (Thing thing in things)
                {
                    CompHistoricalBlueprint comp = thing?.TryGetComp<CompHistoricalBlueprint>();
                    if (comp != null && comp.Research != null && !thing.IsForbidden(Faction.OfPlayer)) yield return comp;
                }
            }
        }

        public static Thing CreateBlueprint(ResearchProjectDef research, Faction source = null)
        {
            if (!CanCreateBlueprint(research)) return null;
            ThingDef def = HistoricalBlueprintDefOf.HR_HistoricalTechnologyBlueprint;
            if (def == null) return null;
            Thing thing = ThingMaker.MakeThing(def);
            CompHistoricalBlueprint comp = thing.TryGetComp<CompHistoricalBlueprint>();
            if (comp == null) { thing.Destroy(DestroyMode.Vanish); return null; }
            comp.ResearchDefName = research.defName;
            comp.OriginFactionId = source?.GetUniqueLoadID();
            comp.OriginFactionName = source?.Name;
            return thing;
        }

        public static bool CanCreateBlueprint(ResearchProjectDef research)
        {
            if (!HistoricalTechnologyManager.IsActive || research == null || DefDatabase<ResearchProjectDef>.GetNamedSilentFail(research.defName) == null)
                return false;
            if (GetMode(research) != HistoricalResearchMode.Historical
                || GetKnowledgeRequirement(research) != HistoricalKnowledgeRequirement.Blueprint) return false;
            if (research.IsFinished || !HistoricalTechnologyManager.HasTechnologyEmerged(research)) return false;
            return HistoricalTechnologyManager.Technologies.Any(item => item.researchProject == research
                && item.contentPolicy != HistoricalContentPolicy.Forbidden);
        }

        public static bool CanFactionSellBlueprint(Faction faction, ResearchProjectDef research)
        {
            HistoricalTechExtension extension = research?.GetModExtension<HistoricalTechExtension>();
            return faction != null && CanCreateBlueprint(research)
                && extension?.AllowTradeDiffusion != false
                && (!extension.IsRestrictedMilitaryTechnology || extension.AllowTradeDiffusion)
                && HistoricalTechnologyManager.CanFactionSupplyTechnology(faction, research);
        }

        public static bool PrerequisitesComplete(ResearchProjectDef research)
        {
            if (research == null) return false;
            IEnumerable<ResearchProjectDef> prerequisites = (research.prerequisites ?? new List<ResearchProjectDef>())
                .Concat(research.hiddenPrerequisites ?? new List<ResearchProjectDef>());
            return prerequisites.All(item => item != null && item.IsFinished);
        }

        public static bool CanStartHistoricalResearch(ResearchProjectDef research, out string reason)
        {
            reason = null;
            if (research == null) { reason = "Research project is missing."; return false; }
            if (research.IsFinished) return true;
            if (!PrerequisitesComplete(research))
            { reason = "HR_Blueprint_ReasonPrerequisite".Translate().ToString(); return false; }
            if (GetMode(research) != HistoricalResearchMode.Historical) return true;

            HistoricalTechnologyDef technology = HistoricalTechnologyManager.Technologies.FirstOrDefault(item => item.researchProject == research);
            if (technology == null) { reason = "Historical technology definition is missing."; return false; }
            if (HistoricalBlueprintService.GetKnowledgeRequirement(research) == HistoricalKnowledgeRequirement.Special)
            { reason = "HR_Blueprint_ReasonSpecial".Translate().ToString(); return false; }
            if (HistoricalBlueprintService.GetKnowledgeRequirement(research) == HistoricalKnowledgeRequirement.Blueprint)
            {
                if (!HistoricalTechnologyManager.HasTechnologyEmerged(research))
                { reason = "HR_Blueprint_ReasonNotEmerged".Translate().ToString(); return false; }
                if (!HasAuthorization(research))
                { reason = "HR_Blueprint_ReasonNoAuthorization".Translate().ToString(); return false; }
            }
            if (HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date)
                && !HistoricalTechnologyTimelineLayout.IsHistoricallyAvailable(research, date.AstronomicalYear))
            { reason = "HR_Blueprint_ReasonDate".Translate().ToString(); return false; }
            return true;
        }

        public static bool TryAppendTraderBlueprints(Faction faction, List<Thing> stock)
            => TryAppendTradeBlueprint(faction, stock, .18f);

        public static bool TryAppendSettlementBlueprint(Faction faction, List<Thing> stock)
            => TryAppendTradeBlueprint(faction, stock, .45f);

        private static bool TryAppendTradeBlueprint(Faction faction, List<Thing> stock, float chance)
        {
            if (faction == null || stock == null || !HistoricalTechnologyManager.IsActive) return false;
            HashSet<string> alreadyStocked = new HashSet<string>(stock.Select(item => item?.TryGetComp<CompHistoricalBlueprint>()?.ResearchDefName)
                .Where(name => !string.IsNullOrWhiteSpace(name)), StringComparer.Ordinal);
            List<ResearchProjectDef> eligible = HistoricalTechnologyTimelineLayout.HistoricalProjects
                .Where(project => CanFactionSellBlueprint(faction, project)
                    && !HasAuthorization(project) && !alreadyStocked.Contains(project.defName))
                .OrderBy(project => project.defName, StringComparer.Ordinal).ToList();
            if (eligible.Count == 0 || !Rand.Chance(chance)) return false;
            ResearchProjectDef selected = eligible.RandomElement();
            Thing blueprint = CreateBlueprint(selected, faction);
            if (blueprint == null) return false;
            stock.Add(blueprint);
            return true;
        }

        public static bool HasAuthorization(ResearchProjectDef research) => research != null
            && (research.IsFinished || Component?.FindAuthorization(research.defName)?.IsAuthorized == true);

        public static bool TryGrantAuthorization(ResearchProjectDef research, HistoricalResearchAuthorizationSource source, string details, out string error)
        {
            error = null;
            if (research == null || GetMode(research) != HistoricalResearchMode.Historical
                || GetKnowledgeRequirement(research) != HistoricalKnowledgeRequirement.Blueprint)
            { error = "The research does not use blueprint authorization."; return false; }
            if (HasAuthorization(research)) { error = "Research authorization is already present."; return false; }
            if (Component == null || !Component.GrantAuthorization(research.defName, source, details))
            { error = "Could not save the research authorization."; return false; }
            HistoricalTechnologyDef technology = HistoricalTechnologyManager.Technologies.FirstOrDefault(item => item.researchProject == research);
            HistoricalTechnologyAcquisitionSource acquisitionSource = source == HistoricalResearchAuthorizationSource.BlueprintDecoded
                ? HistoricalTechnologyAcquisitionSource.Trade
                : source == HistoricalResearchAuthorizationSource.SpecialEvent
                    ? HistoricalTechnologyAcquisitionSource.HistoricalEvent : HistoricalTechnologyAcquisitionSource.Development;
            if (technology != null && !HistoricalTechnologyManager.TryGrantTechnologyKnowledge(HistoricalTechnologyManager.GetPlayerCommunityId(),
                technology.defName, acquisitionSource, out error))
            {
                Component.RevokeAuthorization(research.defName);
                return false;
            }
            HistoricalResearchTimelineUIState.Invalidate();
            return true;
        }

        public static bool TryCompleteDecoding(Thing blueprint, out string error)
        {
            error = null;
            CompHistoricalBlueprint comp = blueprint?.TryGetComp<CompHistoricalBlueprint>();
            ResearchProjectDef research = comp?.Research;
            if (blueprint == null || blueprint.Destroyed || !blueprint.Spawned || comp == null || research == null)
            { error = "The blueprint or its linked research project is missing."; return false; }
            if (HasAuthorization(research)) { error = "This blueprint has already been decoded."; return false; }
            if (!HistoricalTechnologyManager.HasTechnologyEmerged(research))
            { error = "The technology has not emerged in the historical world yet."; return false; }
            if (!TryGrantAuthorization(research, HistoricalResearchAuthorizationSource.BlueprintDecoded,
                string.IsNullOrWhiteSpace(comp.OriginFactionName) ? "Blueprint" : comp.OriginFactionName, out error)) return false;

            if (!comp.AcquisitionRecorded) RecordAcquisition(comp);
            HistoricalChronicleManager.TryAddLocalExperience("BlueprintDecoded:" + research.defName,
                "HR_Blueprint_DecodedChronicleTitle", "HR_Blueprint_DecodedChronicleText",
                null, null, HistoricalEvidenceLevel.Speculative, "Player research staff decoded a purchased or recovered technical blueprint.", out _);
            Messages.Message("HR_Blueprint_DecodedMessage".Translate(research.LabelCap), MessageTypeDefOf.PositiveEvent, false);
            blueprint.Destroy(DestroyMode.Vanish);
            return true;
        }

        public static void RecordAcquisition(CompHistoricalBlueprint comp)
        {
            if (comp == null || comp.AcquisitionRecorded || comp.Research == null) return;
            string source = string.IsNullOrWhiteSpace(comp.OriginFactionName) ? "an unknown source" : comp.OriginFactionName;
            bool recorded = HistoricalChronicleManager.TryAddLocalExperience("BlueprintAcquired:" + comp.ResearchDefName,
                "HR_Blueprint_AcquiredChronicleTitle", "HR_Blueprint_AcquiredChronicleText",
                null, null, HistoricalEvidenceLevel.Speculative,
                "Research=" + comp.Research.LabelCap + "; source=" + source + ". Recorded when first observed in player storage.", out _);
            comp.AcquisitionRecorded = recorded;
        }

        public static void ScanPlayerBlueprintAcquisitions()
        {
            foreach (CompHistoricalBlueprint blueprint in EnumerateAccessibleBlueprints()) RecordAcquisition(blueprint);
        }
    }
}
