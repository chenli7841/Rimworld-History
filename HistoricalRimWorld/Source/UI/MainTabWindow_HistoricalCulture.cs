using System.Linq;
using HistoricalRimWorld.Culture;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_HistoricalCulture : MainTabWindow
    {
        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), Translator.Translate("HR_Culture_Title"));
            if (!HistoricalCultureManager.IsActive || !HistoricalScenarioManager.IsScenarioActive)
            {
                Widgets.Label(new Rect(rect.x, rect.y + 38f, rect.width, 32f), Translator.Translate("HR_Culture_Inactive")); return;
            }
            float y = rect.y + 40f;
            foreach (HistoricalCommunityCultureState community in HistoricalCultureManager.Communities)
            {
                string name = DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(community.DominantCultureId)?.historicalName ?? community.DominantCultureId;
                Widgets.Label(new Rect(rect.x, y, rect.width, 30f), string.Format(Translator.Translate("HR_Culture_Community"), community.CommunityId, name)); y += 32f;
                Widgets.Label(new Rect(rect.x, y, rect.width, 42f), Translator.Translate("HR_Culture_TransmissionNote")); y += 44f;
                foreach (HistoricalCulturalFamiliarity presence in community.CulturalPresence ?? Enumerable.Empty<HistoricalCulturalFamiliarity>())
                {
                    string culture = DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(presence.CultureId)?.historicalName ?? presence.CultureId;
                    Widgets.Label(new Rect(rect.x + 16f, y, rect.width - 16f, 24f), string.Format(Translator.Translate("HR_Culture_Presence"), culture, presence.Familiarity.ToString("P0"))); y += 26f;
                }
                foreach (string customId in community.ActiveCustomIds ?? Enumerable.Empty<string>())
                {
                    HistoricalCustomDef custom = DefDatabase<HistoricalCustomDef>.GetNamedSilentFail(customId);
                    if (custom != null) { Widgets.Label(new Rect(rect.x + 16f, y, rect.width - 16f, 24f), "• " + Translator.Translate(custom.label)); y += 25f; }
                }
            }
            Pawn selected = Find.Selector?.SingleSelectedThing as Pawn;
            if (selected != null && HistoricalGenealogyManager.TryGetPersonId(selected, out string id))
            {
                HistoricalPersonCultureState person = HistoricalCultureManager.FindPerson(id);
                if (person != null)
                {
                    y += 12f;
                    Widgets.Label(new Rect(rect.x, y, rect.width, 28f), string.Format(Translator.Translate("HR_Culture_Person"), selected.LabelShort, person.PrimaryCultureId)); y += 28f;
                    foreach (HistoricalCulturalFamiliarity familiarity in person.Familiarities ?? Enumerable.Empty<HistoricalCulturalFamiliarity>())
                    {
                        string culture = DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(familiarity.CultureId)?.historicalName ?? familiarity.CultureId;
                        Widgets.Label(new Rect(rect.x + 16f, y, rect.width - 16f, 24f), string.Format(Translator.Translate("HR_Culture_Presence"), culture, familiarity.Familiarity.ToString("P0"))); y += 24f;
                    }
                    HistoricalFamilyCultureState family = HistoricalCultureManager.FindFamily(HistoricalGenealogyManager.FindPerson(id)?.FamilyId);
                    if (family?.PreservedTraditionIds?.Count > 0)
                    {
                        Widgets.Label(new Rect(rect.x, y, rect.width, 24f), Translator.Translate("HR_Culture_FamilyTraditions")); y += 24f;
                        foreach (string traditionId in family.PreservedTraditionIds)
                        { Widgets.Label(new Rect(rect.x + 16f, y, rect.width - 16f, 24f), "• " + Translator.Translate(traditionId)); y += 24f; }
                    }
                }
            }
        }
    }
}
