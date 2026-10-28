using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.UI
{
    public static class HistoricalPoliticsUI
    {
        public static int EstimatedLines
        {
            get
            {
                if (!HistoricalPoliticsManager.IsActive) return 3;
                return 8 + HistoricalPoliticsManager.Polities.Count() * 5
                    + HistoricalPoliticsManager.Regions.Count() * 5
                    + HistoricalPoliticsManager.Domains.Count() * 3
                    + HistoricalPoliticsManager.Communities.Count() * 8
                    + HistoricalPoliticsManager.Changes.Count() * 2
                    + (Prefs.DevMode ? 4 : 0);
            }
        }

        public static void Draw(Listing_Standard listing)
        {
            listing.Label(Translator.Translate("HR_Politics_Title"));
            if (!HistoricalPoliticsManager.IsActive)
            {
                listing.Label(Translator.Translate("HR_Politics_Inactive"));
                return;
            }

            HistoricalScenarioDef scenario = HistoricalScenarioManager.CurrentScenario;
            if (scenario != null)
            {
                listing.Label(string.Format(Translator.Translate("HR_Politics_Scenario"), scenario.LabelCap));
            }

            DrawPolities(listing);
            DrawRegions(listing);
            DrawDomains(listing);
            DrawCommunities(listing);
            DrawChanges(listing);
        }

        private static void DrawPolities(Listing_Standard listing)
        {
            listing.Gap();
            listing.Label(Translator.Translate("HR_Politics_Polities"));
            foreach (HistoricalPolityState polity in HistoricalPoliticsManager.Polities)
            {
                HistoricalStateDef def = DefDatabase<HistoricalStateDef>.GetNamedSilentFail(polity.PolityId);
                string name = def == null ? polity.PolityId : def.LabelCap;
                string nominalOverlord = ResolvePolityName(polity.NominalOverlordId);
                string effectiveOverlord = ResolvePolityName(polity.EffectiveOverlordId);
                listing.Label(string.Format(Translator.Translate("HR_Politics_PolitySummary"),
                    name,
                    Translator.Translate("HR_Politics_Status_" + polity.Status),
                    nominalOverlord,
                    effectiveOverlord));

                if (Prefs.DevMode)
                {
                    HistoricalPolityStatus nextStatus = polity.Status == HistoricalPolityStatus.Active
                        ? HistoricalPolityStatus.Subordinate
                        : HistoricalPolityStatus.Active;
                    if (listing.ButtonText(string.Format(Translator.Translate("HR_Politics_DebugTogglePolity"), name)))
                    {
                        string error;
                        if (!HistoricalPoliticsManager.TrySetPolityStatus(polity.PolityId, nextStatus,
                            HistoricalPoliticalChangeSource.Debug, out error)) Log.Error("[Historical RimWorld] " + error);
                    }
                }
            }
        }

        private static void DrawRegions(Listing_Standard listing)
        {
            listing.Gap();
            listing.Label(Translator.Translate("HR_Politics_Regions"));
            foreach (HistoricalRegionPoliticalState region in HistoricalPoliticsManager.Regions)
            {
                HistoricalRegionDef def = DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(region.RegionId);
                string name = def == null ? region.RegionId : def.LabelCap;
                listing.Label(string.Format(Translator.Translate("HR_Politics_RegionSummary"),
                    name,
                    ResolvePolityName(region.NominalOwnerId),
                    ResolvePolityName(region.EffectiveControllerId),
                    ResolvePolityName(region.MilitaryOccupierId),
                    Translator.Translate(region.IsContested ? "HR_Politics_Yes" : "HR_Politics_No")));

                if (Prefs.DevMode)
                {
                    List<HistoricalPolityState> polities = HistoricalPoliticsManager.Polities.ToList();
                    int currentIndex = polities.FindIndex(item => item.PolityId == region.EffectiveControllerId);
                    string nextControllerId = polities.Count == 0 ? null : polities[(currentIndex + 1) % polities.Count].PolityId;
                    if (listing.ButtonText(string.Format(Translator.Translate("HR_Politics_DebugCycleController"), name)))
                    {
                        string error;
                        if (!HistoricalPoliticsManager.TryChangeRegionController(region.RegionId, nextControllerId,
                            HistoricalPoliticalChangeSource.Debug, out error)) Log.Error("[Historical RimWorld] " + error);
                    }
                }
            }
        }

        private static void DrawDomains(Listing_Standard listing)
        {
            listing.Gap();
            listing.Label(Translator.Translate("HR_Politics_Domains"));
            foreach (HistoricalDomainState domain in HistoricalPoliticsManager.Domains)
            {
                HistoricalDomainDef def = DefDatabase<HistoricalDomainDef>.GetNamedSilentFail(domain.DomainId);
                string name = def == null ? domain.DomainId : def.LabelCap;
                listing.Label(string.Format(Translator.Translate("HR_Politics_DomainSummary"), name,
                    ResolvePolityName(domain.ParentPolityId), ResolvePolityName(domain.NominalOwnerId),
                    ResolvePolityName(domain.EffectiveControllerId), domain.IsDisputed
                        ? Translator.Translate("HR_Politics_Yes") : Translator.Translate("HR_Politics_No")));
            }
        }

        private static void DrawCommunities(Listing_Standard listing)
        {
            listing.Gap();
            listing.Label(Translator.Translate("HR_Politics_Communities"));
            foreach (HistoricalLocalCommunityState community in HistoricalPoliticsManager.Communities)
            {
                listing.Label(string.Format(Translator.Translate("HR_Politics_CommunitySummary"),
                    community.CommunityId,
                    ResolveRegionName(community.RegionId),
                    ResolvePolityName(community.NominalPolityId),
                    ResolvePolityName(community.AdministrativePolityId),
                    ResolvePolityName(community.EffectiveControllerId),
                    ResolvePolityName(community.AllegiancePolityId),
                    Translator.Translate("HR_Politics_Posture_" + community.Posture)));

                if (Prefs.DevMode)
                {
                    HistoricalRegionPoliticalState region;
                    string targetId = HistoricalPoliticsManager.TryGetRegion(community.RegionId, out region)
                        ? (region.MilitaryOccupierId ?? region.EffectiveControllerId)
                        : null;
                    if (targetId != null && community.Posture != HistoricalCommunityPosture.Submitting
                        && listing.ButtonText(string.Format(Translator.Translate("HR_Politics_DebugSubmitCommunity"), community.CommunityId)))
                    {
                        string error;
                        if (!HistoricalPoliticsManager.TrySubmitCommunity(community.CommunityId, targetId,
                            HistoricalPoliticalChangeSource.Debug, out error)) Log.Error("[Historical RimWorld] " + error);
                    }

                    HistoricalCommunityPosture nextPosture = community.Posture == HistoricalCommunityPosture.Resisting
                        ? HistoricalCommunityPosture.Peaceful : HistoricalCommunityPosture.Resisting;
                    HistoricalRegionPoliticalState communityRegion;
                    bool canToggleResistance = community.Posture == HistoricalCommunityPosture.Resisting
                        || (HistoricalPoliticsManager.TryGetRegion(community.RegionId, out communityRegion)
                            && !string.IsNullOrEmpty(community.AllegiancePolityId)
                            && communityRegion.EffectiveControllerId != community.AllegiancePolityId);
                    if (canToggleResistance && listing.ButtonText(string.Format(Translator.Translate("HR_Politics_DebugToggleResistance"), community.CommunityId)))
                    {
                        string error;
                        if (!HistoricalPoliticsManager.TrySetCommunityPosture(community.CommunityId, nextPosture,
                            HistoricalPoliticalChangeSource.Debug, out error)) Log.Error("[Historical RimWorld] " + error);
                    }
                }
            }
        }

        private static void DrawChanges(Listing_Standard listing)
        {
            listing.Gap();
            listing.Label(Translator.Translate("HR_Politics_Changes"));
            foreach (HistoricalPoliticalChangeRecord change in HistoricalPoliticsManager.Changes.Reverse())
            {
                HistoricalDate date = new HistoricalDate(change.AstronomicalYear, change.Month, change.Day);
                listing.Label(string.Format(Translator.Translate("HR_Politics_ChangeSummary"),
                    change.ChangeId,
                    Translator.Translate("HR_Politics_ChangeType_" + change.ChangeType),
                    HistoricalDateFormatter.Format(date),
                    change.TargetId,
                    change.OldValue, change.NewValue,
                    Translator.Translate("HR_Politics_Source_" + change.Source)));
            }
        }

        private static string ResolvePolityName(string polityId)
        {
            if (polityId == null) return Translator.Translate("HR_Politics_None");
            HistoricalStateDef def = DefDatabase<HistoricalStateDef>.GetNamedSilentFail(polityId);
            return def == null ? polityId : def.LabelCap;
        }

        private static string ResolveRegionName(string regionId)
        {
            HistoricalRegionDef def = DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId);
            return def == null ? regionId : def.LabelCap;
        }
    }
}
