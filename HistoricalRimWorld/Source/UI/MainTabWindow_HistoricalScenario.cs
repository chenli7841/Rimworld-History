using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Culture;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Legacy;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.Scenarios.EasternZhou;
using HistoricalRimWorld.World;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_HistoricalScenario : MainTabWindow
    {
        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label(Translator.Translate("HR_EZ_Title"));
            listing.Gap();
            if (!HistoricalScenarioProgressManager.IsPlayableScenarioActive)
            {
                listing.Label(Translator.Translate("HR_EZ_NotActive"));
                listing.End();
                return;
            }

            HistoricalDate date;
            if (HistoricalCalendarManager.TryGetCurrentDate(out date))
                listing.Label(string.Format(Translator.Translate("HR_EZ_CurrentDate"), HistoricalDateFormatter.Format(date)));
            listing.Label(Translator.Translate("HR_EZ_Background"));
            listing.Gap();
            string regionLabel = HistoricalScenarioManager.CurrentScenario?.defaultStartingRegion?.LabelCap.ToString()
                ?? Translator.Translate("HR_EZ_StartingRegion");
            listing.Label(string.Format(Translator.Translate("HR_EZ_Region"), regionLabel));
            string communityId = HistoricalScenarioProgressManager.ActiveSetup?.startingCommunityName;
            HistoricalLocalCommunityState community = HistoricalPoliticsManager.Communities.FirstOrDefault(item => item.CommunityId == communityId);
            string polityId = community?.AllegiancePolityId ?? HistoricalScenarioProgressManager.ActiveSetup?.defaultStartingPolity?.defName ?? "HR_State_Zheng";
            string polityLabel = DefDatabase<HistoricalStateDef>.GetNamedSilentFail(polityId)?.LabelCap.ToString() ?? polityId;
            listing.Label(string.Format(Translator.Translate("HR_EZ_Allegiance"), polityLabel));
            listing.Label(string.Format(Translator.Translate("HR_EZ_Population"), Find.CurrentMap?.mapPawns?.FreeColonists?.Count ?? 0));
            listing.Label(string.Format(Translator.Translate("HR_EZ_Families"), HistoricalGenealogyManager.Families.Count()));
            int revealedRegions = Find.World?.GetComponent<HistoricalWorldRevealWorldComponent>()?.Regions
                .Count(item => item.Access == HistoricalRegionAccess.Revealed) ?? 0;
            listing.Label(string.Format(Translator.Translate("HR_EZ_RevealedRegions"), revealedRegions));
            listing.Gap();
            listing.Label(Translator.Translate(HistoricalScenarioProgressManager.IsCompleted ? "HR_EZ_Completed" :
                HistoricalScenarioProgressManager.IsEndDateReached ? "HR_EZ_EndDateReady" : "HR_EZ_Objectives"));

            if (!HistoricalScenarioProgressManager.IsCompleted && HistoricalScenarioProgressManager.IsEndDateReached
                && listing.ButtonText(Translator.Translate("HR_EZ_CompleteButton")))
            {
                if (!HistoricalScenarioProgressManager.TryCompleteScenario(out string error)) Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            }
            if (HistoricalScenarioProgressManager.IsCompleted && listing.ButtonText(Translator.Translate("HR_EZ_ExportButton")))
            {
                if (HistoricalLegacyExporter.TryExport(Translator.Translate("HR_EZ_ArchiveName"), out string path, out string error))
                    Messages.Message(string.Format(Translator.Translate("HR_EZ_Exported"), path), MessageTypeDefOf.PositiveEvent, false);
                else Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            }
            listing.End();
        }
    }
}
