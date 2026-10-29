using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Technology;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_HistoricalTechnology : MainTabWindow
    {
        private Vector2 listScroll;
        private string selectedId;

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), Translator.Translate("HR_Tech_Title"));
            if (!HistoricalTechnologyManager.IsActive)
            {
                Widgets.Label(new Rect(inRect.x, inRect.y + 40f, inRect.width, 32f), Translator.Translate("HR_Tech_Inactive"));
                return;
            }
            Rect listRect = new Rect(inRect.x, inRect.y + 38f, inRect.width * .40f, inRect.height - 42f);
            Rect detailRect = new Rect(listRect.xMax + 10f, listRect.y, inRect.width - listRect.width - 10f, listRect.height);
            List<HistoricalTechnologyDef> defs = HistoricalTechnologyManager.Technologies
                .Where(item => HistoricalTechnologyManager.PlayerTechnologies.FirstOrDefault(state => state.TechnologyId == item.defName)?.Availability
                    != HistoricalTechnologyAvailability.NotYetHistoricallyAvailable)
                .OrderBy(item => item.category).ThenBy(item => item.label).ToList();
            Rect viewRect = new Rect(0f, 0f, listRect.width - 18f, Math.Max(listRect.height, defs.Count * 42f));
            Widgets.BeginScrollView(listRect, ref listScroll, viewRect);
            float y = 0f;
            foreach (HistoricalTechnologyDef def in defs)
            {
                HistoricalTechnologyState state = HistoricalTechnologyManager.PlayerTechnologies.FirstOrDefault(item => item.TechnologyId == def.defName);
                string row = (def.label ?? def.defName) + "\n" + Translator.Translate("HR_Tech_Category_" + def.category) + " · " + Translator.Translate("HR_Tech_Adoption_" + (state?.Adoption ?? HistoricalTechnologyAdoption.Unknown));
                if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, 38f), row)) selectedId = def.defName;
                y += 41f;
            }
            Widgets.EndScrollView();
            DrawDetails(detailRect, DefDatabase<HistoricalTechnologyDef>.GetNamedSilentFail(selectedId));
        }

        private static void DrawDetails(Rect rect, HistoricalTechnologyDef def)
        {
            if (def == null) { Widgets.Label(rect, Translator.Translate("HR_Tech_Select")); return; }
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            HistoricalTechnologyState state = HistoricalTechnologyManager.PlayerTechnologies.FirstOrDefault(item => item.TechnologyId == def.defName);
            listing.Label(def.label ?? def.defName);
            listing.Label(def.description ?? string.Empty);
            listing.Label(Translator.Translate("HR_Tech_Category_" + def.category));
            listing.Label(Translator.Translate("HR_Tech_Availability_" + (state?.Availability ?? HistoricalTechnologyAvailability.ExistsElsewhere)));
            listing.Label(Translator.Translate("HR_Tech_Adoption_" + (state?.Adoption ?? HistoricalTechnologyAdoption.Unknown)));
            if (!string.IsNullOrWhiteSpace(def.sourceNotes)) listing.Label(def.sourceNotes);
            if (def.prerequisites != null && def.prerequisites.Count > 0)
                listing.Label(Translator.Translate("HR_Tech_Prerequisites") + ": " + string.Join(", ", def.prerequisites.Where(item => item != null).Select(item => item.label)));
            if (def.researchProject != null)
            {
                float progress = Find.ResearchManager.GetProgress(def.researchProject);
                listing.Label(string.Format(Translator.Translate("HR_Tech_Progress"), progress, def.researchProject.baseCost));
                if (HistoricalTechnologyManager.CanStudy(HistoricalTechnologyManager.GetPlayerCommunityId(), def.defName)
                    && listing.ButtonText(Translator.Translate("HR_Tech_BeginStudy")))
                    Find.ResearchManager.SetCurrentProject(def.researchProject);
            }
            else listing.Label(Translator.Translate("HR_Tech_NoResearchProject"));
            listing.End();
        }
    }
}
