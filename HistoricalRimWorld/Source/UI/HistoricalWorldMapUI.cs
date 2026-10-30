using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public static class HistoricalWorldMapUI
    {
        public static void Draw(HistoricalWorldRevealWorldComponent component)
        {
            if (component == null || !WorldRendererUtility.WorldSelected) return;

            Rect panel = new Rect(16f, 70f, 390f, 560f);
            Widgets.DrawWindowBackground(panel);
            Rect viewRect = new Rect(0f, 0f, panel.width - 18f, CalculateContentHeight(component));
            Widgets.BeginScrollView(panel, ref scrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            listing.Label(Translator.Translate("HR_World_Title"));
            listing.Label(Translator.Translate(component.MapMode == HistoricalWorldMapMode.PrototypeWorld
                ? "HR_World_PrototypeMode" : "HR_World_EarthMode"));
            HistoricalMapScaleState mapScale = Find.World?.GetComponent<HistoricalFactionWorldComponent>()?.MapScaleState;
            if (mapScale != null)
            {
                listing.Label("历史地图比例尺：" + mapScale.ScaleFactor.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "×");
                listing.Label("投影中心：" + mapScale.CenterLatitude.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + "°N, " + mapScale.CenterLongitude.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "°E");
                listing.Label("投影版本：" + mapScale.ProjectionVersion + "；世界生成版本：" + mapScale.WorldGenerationVersion);
            }
            listing.GapLine();
            listing.Label(Translator.Translate("HR_World_Legend"));
            DrawSelectedTileInformation(listing, component);

            int lockedCount = component.Regions.Count(item => item.Access == HistoricalRegionAccess.Locked);
            if (lockedCount > 0 && !Prefs.DevMode)
            {
                listing.Label(string.Format(Translator.Translate("HR_World_LockedAreaCount"), lockedCount));
            }

            foreach (HistoricalRegionRevealState state in component.Regions.OrderBy(item => item.RegionId))
            {
                HistoricalRegionDef def = DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(state.RegionId);
                if (state.Access == HistoricalRegionAccess.Locked && !Prefs.DevMode) continue;
                string label = def == null ? state.RegionId : def.LabelCap;
                listing.Gap();

                if (state.Access == HistoricalRegionAccess.Locked)
                {
                    listing.Label(Translator.Translate("HR_World_LockedRegion"));
                }
                else if (state.Access == HistoricalRegionAccess.Unexplored)
                {
                    listing.Label(label + " — " + Translator.Translate("HR_World_UnexploredRegion"));
                }
                else
                {
                    listing.Label(label);
                    DrawKnownPoliticalInformation(listing, state.RegionId);
                }

                if (Prefs.DevMode)
                {
                    if (state.Access == HistoricalRegionAccess.Locked
                        && listing.ButtonText(string.Format(Translator.Translate("HR_World_DebugUnlock"), label)))
                    {
                        TryAction(delegate(out string error)
                        {
                            return HistoricalWorldRevealManager.TryUnlockRegion(state.RegionId,
                                HistoricalRevealSource.DeveloperAction, out error);
                        }, state.RegionId);
                    }
                    else if (state.Access == HistoricalRegionAccess.Unexplored
                        && listing.ButtonText(string.Format(Translator.Translate("HR_World_DebugReveal"), label)))
                    {
                        TryAction(delegate(out string error)
                        {
                            return HistoricalWorldRevealManager.TryRevealRegion(state.RegionId,
                                HistoricalRevealSource.DeveloperAction, out error);
                        }, state.RegionId);
                    }

                    DrawPrototypeMappingControls(listing, component, state, label);
                }
            }

            listing.End();
            Widgets.EndScrollView();
        }

        private static Vector2 scrollPosition;

        private static float CalculateContentHeight(HistoricalWorldRevealWorldComponent component)
        {
            float lines = 8f + component.Regions.Count() * (Prefs.DevMode ? 5f : 3f);
            return Mathf.Max(560f, lines * 28f);
        }

        private static void DrawKnownPoliticalInformation(Listing_Standard listing, string regionId)
        {
            HistoricalDate date;
            if (HistoricalCalendarManager.TryGetCurrentDate(out date))
            {
                listing.Label(string.Format(Translator.Translate("HR_World_CurrentHistoricalDate"),
                    HistoricalDateFormatter.Format(date)));
            }

            HistoricalRegionPoliticalState politics;
            if (HistoricalPoliticsManager.TryGetRegion(regionId, out politics))
            {
                listing.Label(string.Format(Translator.Translate("HR_World_RegionPolitics"),
                    ResolvePolity(politics.NominalOwnerId), ResolvePolity(politics.EffectiveControllerId)));
            }
        }

        private static void DrawSelectedTileInformation(Listing_Standard listing, HistoricalWorldRevealWorldComponent component)
        {
            if (Find.WorldSelector == null || !Find.WorldSelector.SelectedTile.Valid) return;
            int tileId = (int)Find.WorldSelector.SelectedTile;
            IReadOnlyList<string> regionIds = HistoricalWorldRevealManager.GetRegionsForTile(tileId).ToList().AsReadOnly();
            if (regionIds.Count == 0) return;

            listing.Gap();
            listing.Label(Translator.Translate("HR_World_SelectedTile"));
            if (!HistoricalWorldRevealManager.IsTileVisible(tileId))
            {
                listing.Label(Translator.Translate("HR_World_LockedHover"));
                return;
            }

            List<HistoricalRegionRevealState> states = regionIds
                .Select(component.FindRegion)
                .Where(item => item != null)
                .ToList();
            if (states.Any(item => item.Access == HistoricalRegionAccess.Unexplored))
            {
                listing.Label(Translator.Translate("HR_World_UnexploredHover"));
            }
            else
            {
                foreach (HistoricalRegionRevealState state in states)
                {
                    HistoricalRegionDef def = DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(state.RegionId);
                    if (def != null) listing.Label(def.LabelCap);
                }
            }
        }

        private static string ResolvePolity(string polityId)
        {
            if (polityId == null) return Translator.Translate("HR_Politics_None");
            HistoricalStateDef def = DefDatabase<HistoricalStateDef>.GetNamedSilentFail(polityId);
            return def == null ? polityId : def.LabelCap;
        }

        private static void DrawPrototypeMappingControls(Listing_Standard listing,
            HistoricalWorldRevealWorldComponent component, HistoricalRegionRevealState state, string label)
        {
            if (component.MapMode != HistoricalWorldMapMode.PrototypeWorld || Find.WorldSelector == null
                || !Find.WorldSelector.SelectedTile.Valid) return;

            int selectedTileId = (int)Find.WorldSelector.SelectedTile;
            HistoricalPrototypeTileMapping mapping = component.FindTileMapping(selectedTileId);
            bool mapped = mapping != null && mapping.RegionIds.Contains(state.RegionId);
            string buttonKey = mapped ? "HR_World_DebugUnmapTile" : "HR_World_DebugMapTile";
            if (!listing.ButtonText(string.Format(Translator.Translate(buttonKey), selectedTileId, label))) return;

            string error;
            bool success = mapped
                ? HistoricalWorldRevealManager.TryUnmapPrototypeTile(selectedTileId, state.RegionId,
                    HistoricalRevealSource.DeveloperAction, out error)
                : HistoricalWorldRevealManager.TryMapPrototypeTile(selectedTileId, state.RegionId,
                    HistoricalRevealSource.DeveloperAction, out error);
            if (!success) Log.Error("[Historical RimWorld] " + error);
        }

        private delegate bool RegionAction(out string error);

        private static void TryAction(RegionAction action, string regionId)
        {
            string error;
            if (!action(out error)) Log.Error("[Historical RimWorld] Region access change failed for " + regionId + ": " + error);
        }
    }
}
