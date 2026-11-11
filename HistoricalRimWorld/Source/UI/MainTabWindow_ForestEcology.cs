using System;
using System.Linq;
using HistoricalRimWorld.Forest;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_ForestEcology : MainTabWindow
    {
        private Vector2 scrollPosition;
        private int selectedRegionId;

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label(Translator.Translate("HR_Forest_Title"));
            if (!HistoricalScenarioManager.IsScenarioActive || Find.CurrentMap == null)
            {
                listing.Label(Translator.Translate("HR_Forest_Inactive"));
                listing.End();
                return;
            }

            MapComponent_ForestEcology ecology = Find.CurrentMap.GetComponent<MapComponent_ForestEcology>();
            if (ecology == null)
            {
                listing.Label(Translator.Translate("HR_Forest_NotReady"));
                listing.End();
                return;
            }

            if (listing.ButtonText(Translator.Translate("HR_Forest_ToggleAll")))
                ecology.ToggleAllKnownRegionHighlight();
            if (listing.ButtonText(Translator.Translate("HR_Forest_ClearHighlight")))
                ecology.ClearHighlight();
            listing.Label(string.Format(Translator.Translate("HR_Forest_KnownSummary"),
                ecology.GetKnownRegions().Count));
            listing.GapLine();
            listing.End();

            Rect listRect = new Rect(inRect.x, inRect.y + 126f, inRect.width, inRect.height - 126f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 18f,
                Math.Max(listRect.height, ecology.GetKnownRegions().Count * 54f + 360f));
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            Listing_Standard rows = new Listing_Standard();
            rows.Begin(viewRect);
            int knownOrdinal = 0;
            foreach (ForestEcologyRegion region in ecology.GetKnownRegions().OrderBy(item => item.Center.z).ThenBy(item => item.Center.x))
            {
                knownOrdinal++;
                string title = string.Format(Translator.Translate("HR_Forest_RegionButton"), knownOrdinal,
                    region.KnownCellCount);
                if (rows.ButtonText(title))
                {
                    selectedRegionId = region.RegionId;
                    ecology.SelectRegion(selectedRegionId);
                }
                if (selectedRegionId != region.RegionId) continue;
                rows.Label(Translator.Translate("HR_Forest_ExploredOnly"));
                if (!region.FullyExplored)
                {
                    rows.Label(string.Format(Translator.Translate("HR_Forest_PartialStats"), region.KnownCellCount));
                    rows.GapLine();
                    continue;
                }

                rows.Label(string.Format(Translator.Translate("HR_Forest_SizeArea"), SizeLabel(region.CellCount), region.CellCount));
                rows.Label(string.Format(Translator.Translate("HR_Forest_Trees"), region.TreeCount,
                    region.AncientTreeCount, region.MatureAncientTreeCount));
                rows.Label(string.Format(Translator.Translate("HR_Forest_Density"), region.TreeDensity * 100f,
                    region.MatureAncientTreeRatio * 100f));
                rows.Label(string.Format(Translator.Translate("HR_Forest_Integrity"), region.EcologicalIntegrity,
                    StatusLabel(region.Status)));
                rows.Label(string.Format(Translator.Translate("HR_Forest_Habitat"), region.HabitatCapacity,
                    region.HabitatCapacityScore, region.HabitatCapacityTier));
                rows.Label(string.Format(Translator.Translate("HR_Forest_Trend"), TrendLabel(region.Trend)));
                rows.GapLine();
            }
            rows.End();
            Widgets.EndScrollView();
        }

        private static string SizeLabel(int area)
        {
            if (area >= 2000) return Translator.Translate("HR_Forest_Size_Vast");
            if (area >= 1000) return Translator.Translate("HR_Forest_Size_Large");
            if (area >= 400) return Translator.Translate("HR_Forest_Size_Medium");
            if (area >= 100) return Translator.Translate("HR_Forest_Size_Small");
            return Translator.Translate("HR_Forest_Size_Scattered");
        }

        private static string StatusLabel(ForestEcologyStatus status) => Translator.Translate("HR_Forest_Status_" + status);
        private static string TrendLabel(ForestEcologyTrend trend) => Translator.Translate("HR_Forest_Trend_" + trend);
    }
}
