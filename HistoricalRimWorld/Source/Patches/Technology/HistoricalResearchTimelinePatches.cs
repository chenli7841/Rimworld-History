using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.Technology;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.Patches.Technology
{
    internal static class HistoricalResearchTimelineUIState
    {
        public const string TabDefName = "HR_HistoricalTechnologies";
        private static HistoricalTechnologyCategoryGroup selectedCategory = HistoricalTechnologyCategoryGroup.All;
        private static HistoricalTechnologyCategoryGroup cachedCategory = HistoricalTechnologyCategoryGroup.All;
        private static int cachedYear = int.MinValue;
        private static bool cachedShowFuture;
        private static List<ResearchProjectDef> cachedProjects = new List<ResearchProjectDef>();
        private static string pendingFocusProject;

        public static bool IsHistoricalTab(MainTabWindow_Research window) => window?.CurTab?.defName == TabDefName;
        public static HistoricalTechnologyCategoryGroup SelectedCategory => selectedCategory;

        public static List<ResearchProjectDef> VisibleProjects()
        {
            HistoricalDate date;
            int currentYear = HistoricalCalendarManager.TryGetCurrentDate(out date) ? date.AstronomicalYear : int.MinValue;
            bool showFuture = HistoricalRimWorldMod.CurrentSettings?.ShowFutureHistoricalTechnologies ?? false;
            if (cachedCategory == selectedCategory && cachedYear == currentYear && cachedShowFuture == showFuture)
                return cachedProjects;

            cachedCategory = selectedCategory;
            cachedYear = currentYear;
            cachedShowFuture = showFuture;
            cachedProjects = HistoricalTechnologyTimelineLayout.HistoricalProjects
                .Where(project => selectedCategory == HistoricalTechnologyCategoryGroup.All
                    || HistoricalTechnologyTimelineLayout.GetCategoryGroup(project) == selectedCategory)
                .Where(project => IsVisible(project, currentYear, showFuture))
                .OrderBy(project => HistoricalTechnologyTimelineLayout.TryGetPosition(project, out HistoricalTechnologyNodePosition position)
                    ? position.X : float.MaxValue)
                .ThenBy(project => project.LabelCap.ToString())
                .ToList();
            return cachedProjects;
        }

        public static void SelectCategory(HistoricalTechnologyCategoryGroup category)
        {
            selectedCategory = category;
            cachedYear = int.MinValue;
        }

        public static void Invalidate() => cachedYear = int.MinValue;

        private static bool IsVisible(ResearchProjectDef project, int currentYear, bool showFuture)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || !HistoricalTechnologyManager.IsActive)
                return showFuture || currentYear == int.MinValue
                    || HistoricalTechnologyTimelineLayout.IsHistoricallyAvailable(project, currentYear);

            PlayerHistoricalTechAwareness awareness = HistoricalTechnologyManager.ComponentAwareness(project);
            bool known = awareness != PlayerHistoricalTechAwareness.Unknown;
            if (showFuture && !HistoricalTechnologyManager.HasTechnologyEmerged(project)) return true;
            return HistoricalTechnologyManager.HasTechnologyEmerged(project) && known;
        }

        public static void FocusProject(string defName) => pendingFocusProject = defName;

        public static bool TryConsumeFocus(out ResearchProjectDef project)
        {
            project = null;
            if (string.IsNullOrWhiteSpace(pendingFocusProject)) return false;
            string name = pendingFocusProject;
            pendingFocusProject = null;
            project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name);
            return project != null;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Research), "get_VisibleResearchProjects")]
    internal static class HistoricalResearchTimelineProjectListPatch
    {
        private static void Postfix(MainTabWindow_Research __instance, ref List<ResearchProjectDef> __result)
        {
            if (!HistoricalResearchTimelineUIState.IsHistoricalTab(__instance)) return;
            __result = HistoricalResearchTimelineUIState.VisibleProjects();
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Research), "PosX")]
    internal static class HistoricalResearchTimelineXPositionPatch
    {
        private static void Postfix(MainTabWindow_Research __instance, ResearchProjectDef d, ref float __result)
        {
            if (!HistoricalResearchTimelineUIState.IsHistoricalTab(__instance)) return;
            HistoricalTechnologyNodePosition position;
            if (HistoricalTechnologyTimelineLayout.TryGetPosition(d, out position)) __result = position.X;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Research), "PosY")]
    internal static class HistoricalResearchTimelineYPositionPatch
    {
        private static void Postfix(MainTabWindow_Research __instance, ResearchProjectDef d, ref float __result)
        {
            if (!HistoricalResearchTimelineUIState.IsHistoricalTab(__instance)) return;
            HistoricalTechnologyNodePosition position;
            if (HistoricalTechnologyTimelineLayout.TryGetPosition(d, out position)) __result = position.Y;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Research), "ViewSize")]
    internal static class HistoricalResearchTimelineViewSizePatch
    {
        private static void Postfix(ResearchTabDef tab, ref Vector2 __result)
        {
            if (tab?.defName != HistoricalResearchTimelineUIState.TabDefName) return;
            float maximumX = HistoricalTechnologyTimelineLayout.ZoomedX(HistoricalTechnologyTimelineLayout.UndatedX);
            float maximumY = (HistoricalTechnologyTimelineLayout.CategoryCount + 1)
                * 2f * 100f;
            foreach (ResearchProjectDef project in HistoricalTechnologyTimelineLayout.HistoricalProjects)
            {
                HistoricalTechnologyNodePosition position;
                if (HistoricalTechnologyTimelineLayout.TryGetPosition(project, out position))
                {
                    maximumX = Mathf.Max(maximumX, position.X);
                    maximumY = Mathf.Max(maximumY, position.Y * 100f);
                }
            }
            __result = new Vector2(Mathf.Max(__result.x, maximumX * 190f + 260f),
                Mathf.Max(__result.y, maximumY + 220f));
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Research), "ListProjects")]
    internal static class HistoricalResearchTimelineControlsPatch
    {
        private const float HeaderHeight = 58f;
        private const float FooterHeight = 40f;

        private static void Prefix(MainTabWindow_Research __instance, ref Rect __0)
        {
            if (!HistoricalResearchTimelineUIState.IsHistoricalTab(__instance)) return;
            __0.yMin += HeaderHeight;
            __0.yMax -= FooterHeight;
        }

        private static void Postfix(MainTabWindow_Research __instance, Rect __0, ref Vector2 ___leftScrollPosition)
        {
            if (!HistoricalResearchTimelineUIState.IsHistoricalTab(__instance)) return;
            if (HistoricalResearchTimelineUIState.TryConsumeFocus(out ResearchProjectDef focus)
                && HistoricalTechnologyTimelineLayout.TryGetPosition(focus, out HistoricalTechnologyNodePosition position))
            {
                ___leftScrollPosition = new Vector2(
                    Mathf.Max(0f, position.X * 190f - __0.width * 0.5f),
                    Mathf.Max(0f, position.Y * 100f - __0.height * 0.5f));
            }
            DrawCategoryFilters(__0);
            DrawTimeline(__instance, __0, ___leftScrollPosition);
        }

        private static void DrawCategoryFilters(Rect graphRect)
        {
            List<HistoricalTechnologyCategoryGroup> categories = new List<HistoricalTechnologyCategoryGroup>
            {
                HistoricalTechnologyCategoryGroup.All
            };
            categories.AddRange(HistoricalTechnologyCategoryGroups.All);

            const int columns = 6;
            float buttonWidth = Mathf.Max(65f, graphRect.width / columns);
            const float buttonHeight = 25f;
            float startY = graphRect.yMin - HeaderHeight + 2f;
            for (int i = 0; i < categories.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                Rect buttonRect = new Rect(graphRect.xMin + column * buttonWidth, startY + row * 27f,
                    buttonWidth - 3f, buttonHeight);
                string key = categories[i] == HistoricalTechnologyCategoryGroup.All
                    ? "HR_Timeline_Category_All"
                    : HistoricalTechnologyCategoryGroups.TranslationKey(categories[i]);
                if (Widgets.ButtonText(buttonRect, Translator.Translate(key)))
                    HistoricalResearchTimelineUIState.SelectCategory(categories[i]);
                if (HistoricalResearchTimelineUIState.SelectedCategory == categories[i])
                    Widgets.DrawBox(buttonRect, 2);
            }

            float zoomRowY = startY + 27f;
            float zoomWidth = 34f;
            Rect zoomOut = new Rect(graphRect.xMax - zoomWidth * 2f - 8f, zoomRowY, zoomWidth, buttonHeight);
            Rect zoomIn = new Rect(graphRect.xMax - zoomWidth - 4f, zoomRowY, zoomWidth, buttonHeight);
            if (Widgets.ButtonText(zoomOut, "−")) ChangeZoom(-0.2f);
            if (Widgets.ButtonText(zoomIn, "+")) ChangeZoom(0.2f);
            Widgets.Label(new Rect(zoomOut.x - 74f, zoomRowY, 70f, buttonHeight),
                string.Format(Translator.Translate("HR_Timeline_Zoom"), HistoricalTechnologyTimelineLayout.Zoom.ToString("0.0")));
        }

        private static void ChangeZoom(float delta)
        {
            HistoricalRimWorldSettings settings = HistoricalRimWorldMod.CurrentSettings;
            if (settings == null) return;
            settings.HistoricalTimelineZoom = Mathf.Clamp(settings.HistoricalTimelineZoom + delta, 0.5f, 2f);
            HistoricalTechnologyTimelineLayout.Invalidate();
            HistoricalResearchTimelineUIState.Invalidate();
        }

        private static void DrawTimeline(MainTabWindow_Research window, Rect graphRect, Vector2 scrollPosition)
        {
            HistoricalDate currentDate;
            bool hasCurrentDate = HistoricalCalendarManager.TryGetCurrentDate(out currentDate);
            float axisTop = graphRect.yMax + 3f;
            Color previousColor = GUI.color;
            GUI.BeginGroup(graphRect);

            if (hasCurrentDate)
            {
                float currentX = HistoricalTechnologyTimelineLayout.ZoomedX(
                    HistoricalTechnologyTimelineLayout.YearToTimelineX(currentDate.AstronomicalYear))
                    * 190f - scrollPosition.x;
                if (currentX >= 0f && currentX <= graphRect.width)
                    DrawSolidRect(new Rect(currentX, 0f, 2f, graphRect.height), new Color(1f, 0.86f, 0.15f, 0.50f));
            }

            GUI.EndGroup();
            GUI.color = previousColor;

            DrawSolidRect(new Rect(graphRect.xMin, axisTop, graphRect.width, 1f), new Color(0.78f, 0.78f, 0.78f, 0.9f));
            HistoricalTechnologyTimelineLayoutDef layout = DefDatabase<HistoricalTechnologyTimelineLayoutDef>
                .GetNamedSilentFail("HR_TechnologyTimeline_EasternZhou");
            if (layout?.segments == null || layout.segments.Count == 0) return;

            HashSet<int> tickYears = new HashSet<int>();
            tickYears.Add(layout.segments[0].startAstronomicalYear);
            foreach (HistoricalTimelineSegment segment in layout.segments) tickYears.Add(segment.endAstronomicalYear);
            foreach (int year in tickYears.OrderBy(item => item))
            {
                float x = HistoricalTechnologyTimelineLayout.ZoomedX(
                    HistoricalTechnologyTimelineLayout.YearToTimelineX(year)) * 190f - scrollPosition.x;
                if (x < 0f || x > graphRect.width) continue;
                float screenX = graphRect.xMin + x;
                DrawSolidRect(new Rect(screenX, axisTop - 4f, 1f, 7f), new Color(0.85f, 0.85f, 0.85f, 1f));
                GUI.Label(new Rect(screenX - 38f, axisTop + 6f, 76f, 20f), HistoricalTechnologyTimelineLayout.FormatYear(year));
            }

            if (hasCurrentDate)
            {
                float x = HistoricalTechnologyTimelineLayout.ZoomedX(
                    HistoricalTechnologyTimelineLayout.YearToTimelineX(currentDate.AstronomicalYear)) * 190f - scrollPosition.x;
                if (x >= 0f && x <= graphRect.width)
                    GUI.Label(new Rect(graphRect.xMin + x + 3f, axisTop + 6f, 150f, 20f),
                        string.Format(Translator.Translate("HR_Timeline_CurrentYear"),
                            HistoricalTechnologyTimelineLayout.FormatYear(currentDate.AstronomicalYear)));
            }

            float undatedX = HistoricalTechnologyTimelineLayout.ZoomedX(HistoricalTechnologyTimelineLayout.UndatedX)
                * 190f - scrollPosition.x;
            if (undatedX >= 0f && undatedX <= graphRect.width)
                GUI.Label(new Rect(graphRect.xMin + undatedX - 42f, axisTop + 6f, 110f, 20f),
                    Translator.Translate("HR_Timeline_UndatedLane"));
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, BaseContent.WhiteTex);
            GUI.color = previous;
        }
    }

    [HarmonyPatch(typeof(ResearchProjectDef), "get_Description")]
    internal static class HistoricalResearchProjectDescriptionPatch
    {
        private static void Postfix(ResearchProjectDef __instance, ref string __result)
        {
            HistoricalTechnologyDef technology = HistoricalTechnologyManager.Technologies.FirstOrDefault(item => item.researchProject == __instance);
            HistoricalTechExtension extension = HistoricalTechnologyTimelineLayout.GetExtension(__instance);
            if (technology == null && extension == null) return;

            List<string> lines = new List<string>();
            if (extension != null)
            {
                string historicalDetails = HistoricalTechnologyTimelineLayout.HistoricalDetails(__instance);
                if (!string.IsNullOrWhiteSpace(historicalDetails)) lines.Add(historicalDetails);
                HistoricalWorldTechStatus worldStatus = HistoricalTechnologyManager.GetWorldStatus(__instance);
                PlayerHistoricalTechAwareness awareness = HistoricalTechnologyManager.ComponentAwareness(__instance);
                lines.Add(string.Format(Translator.Translate("HR_Tech_WorldStatus"), Translator.Translate("HR_Tech_WorldStatus_" + worldStatus)));
                lines.Add(string.Format(Translator.Translate("HR_Tech_PlayerAwareness"), Translator.Translate("HR_Tech_Awareness_" + awareness)));
                if (awareness != PlayerHistoricalTechAwareness.Unknown)
                {
                    IReadOnlyList<Faction> sources = HistoricalTechnologyManager.GetKnownTechnologySources(__instance);
                    if (sources.Count > 0)
                        lines.Add(string.Format(Translator.Translate("HR_Tech_KnownSources"), string.Join(", ", sources.Select(item => item.Name))));
                }
            }

            if (technology != null)
            {
                HistoricalResearchMode mode = HistoricalBlueprintService.GetMode(__instance);
                lines.Add(string.Format(Translator.Translate("HR_Blueprint_ResearchMode"),
                    Translator.Translate("HR_Blueprint_ResearchMode_" + mode)));
                if (mode == HistoricalResearchMode.Historical)
                {
                    HistoricalBlueprintStatus status = HistoricalBlueprintService.GetStatus(__instance);
                    lines.Add("HR_Blueprint_Status".Translate("HR_Blueprint_Status_" + status));
                    if (!HistoricalBlueprintService.CanStartHistoricalResearch(__instance, out string reason)) lines.Add(reason);
                    IReadOnlyList<Faction> sources = HistoricalTechnologyManager.GetKnownTechnologySources(__instance);
                    if (sources.Count > 0)
                        lines.Add("HR_Blueprint_KnownSources".Translate(string.Join(", ", sources.Select(item => item.Name))));
                    if (HistoricalBlueprintService.GetKnowledgeRequirement(__instance) == HistoricalKnowledgeRequirement.Blueprint
                        && status == HistoricalBlueprintStatus.NotOwned)
                        lines.Add("HR_Blueprint_TradeHint".Translate());
                }
                else lines.Add("HR_Blueprint_BasicResearch".Translate());
            }
            if (lines.Count > 0) __result = (string.IsNullOrWhiteSpace(__result) ? string.Empty : __result + "\n\n") + string.Join("\n", lines);
        }

        private static string GetStatus(ResearchProjectDef project)
        {
            HistoricalTechnologyDef technology = HistoricalTechnologyTimelineLayout.GetTechnology(project);
            if (technology == null) return string.Empty;
            HistoricalTechnologyState state = HistoricalTechnologyManager.PlayerTechnologies
                .FirstOrDefault(item => item.TechnologyId == technology.defName);

            if (state?.Adoption == HistoricalTechnologyAdoption.Adopted || project.IsFinished)
                return Translator.Translate("HR_Timeline_Status_Completed");
            if (Find.ResearchManager != null && Find.ResearchManager.IsCurrentProject(project))
                return Translator.Translate("HR_Timeline_Status_InProgress");
            if (!HistoricalTechnologyManager.CanStartHistoricalResearch(project, out _))
                return Translator.Translate("HR_Timeline_Status_KnownButUnavailable");
            return Translator.Translate("HR_Timeline_Status_ResearchAvailable");
        }
    }
}
