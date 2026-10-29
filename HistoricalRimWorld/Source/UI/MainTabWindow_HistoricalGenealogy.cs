using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Lifespan;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_HistoricalGenealogy : MainTabWindow
    {
        private Vector2 familyScroll;
        private Vector2 memberScroll;
        private string selectedFamilyId;
        private string selectedPersonId;

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), Translate("HR_Genealogy_Title"));
            if (!HistoricalScenarioManager.IsScenarioActive || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate))
            {
                Widgets.Label(new Rect(inRect.x, inRect.y + 42f, inRect.width, 40f), Translate("HR_Genealogy_Inactive"));
                return;
            }

            List<HistoricalFamilyRecord> families = HistoricalGenealogyManager.Families
                .Where(item => item != null).OrderBy(item => item.FamilyName, StringComparer.Ordinal).ToList();
            if (selectedFamilyId == null || families.All(item => item.FamilyId != selectedFamilyId))
                selectedFamilyId = families.FirstOrDefault()?.FamilyId;
            HistoricalFamilyRecord selectedFamily = HistoricalGenealogyManager.FindFamily(selectedFamilyId);
            List<HistoricalPersonRecord> members = selectedFamily == null
                ? new List<HistoricalPersonRecord>()
                : selectedFamily.MemberPersonIds.Select(HistoricalGenealogyManager.FindPerson).Where(item => item != null)
                    .OrderBy(item => item.BirthDate.AbsoluteDay).ThenBy(item => item.DisplayName, StringComparer.Ordinal).ToList();
            if (selectedPersonId == null || members.All(item => item.PersonId != selectedPersonId))
                selectedPersonId = members.FirstOrDefault()?.PersonId;

            float bodyY = inRect.y + 38f;
            float bodyHeight = inRect.height - 42f;
            float familyWidth = inRect.width * 0.28f;
            float memberWidth = inRect.width * 0.28f;
            Rect familyRect = new Rect(inRect.x, bodyY, familyWidth, bodyHeight);
            Rect memberRect = new Rect(familyRect.xMax + 8f, bodyY, memberWidth, bodyHeight);
            Rect detailRect = new Rect(memberRect.xMax + 8f, bodyY, inRect.xMax - memberRect.xMax - 8f, bodyHeight);
            DrawFamilies(familyRect, families);
            DrawMembers(memberRect, members);
            DrawPersonDetails(detailRect, HistoricalGenealogyManager.FindPerson(selectedPersonId), currentDate);
        }

        private void DrawFamilies(Rect rect, List<HistoricalFamilyRecord> families)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 28f), Translate("HR_Genealogy_Families"));
            Rect scrollRect = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 18f, Math.Max(scrollRect.height, families.Count * 54f));
            Widgets.BeginScrollView(scrollRect, ref familyScroll, viewRect);
            float y = 0f;
            foreach (HistoricalFamilyRecord family in families)
            {
                int members = family.MemberPersonIds.Count;
                int living = family.MemberPersonIds.Count(id => HistoricalGenealogyManager.FindPerson(id)?.IsAlive == true);
                string label = (family.FamilyName ?? family.FamilyId) + "\n" + string.Format(Translate("HR_Genealogy_FamilyCounts"), members, living);
                if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, 48f), label))
                {
                    selectedFamilyId = family.FamilyId;
                    selectedPersonId = null;
                }
                y += 52f;
            }
            Widgets.EndScrollView();
        }

        private void DrawMembers(Rect rect, List<HistoricalPersonRecord> members)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 28f), Translate("HR_Genealogy_Members"));
            Rect scrollRect = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 18f, Math.Max(scrollRect.height, members.Count * 30f));
            Widgets.BeginScrollView(scrollRect, ref memberScroll, viewRect);
            float y = 0f;
            foreach (HistoricalPersonRecord person in members)
            {
                string name = person.DisplayName ?? person.PersonId;
                if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, 28f), name + (person.IsAlive ? "" : "  †")))
                    selectedPersonId = person.PersonId;
                y += 30f;
            }
            Widgets.EndScrollView();
        }

        private void DrawPersonDetails(Rect rect, HistoricalPersonRecord person, HistoricalDate currentDate)
        {
            if (person == null)
            {
                Widgets.Label(rect, Translate("HR_Genealogy_SelectPerson"));
                return;
            }
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            listing.Label(Translate("HR_Genealogy_PersonTitle"));
            listing.Label(string.Format(Translate("HR_Genealogy_Name"), person.DisplayName ?? Translate("HR_Genealogy_Unknown")));
            if (Prefs.DevMode) listing.Label(string.Format(Translate("HR_Genealogy_PersonId"), person.PersonId));
            listing.Label(string.Format(Translate("HR_Genealogy_Family"), HistoricalGenealogyManager.FindFamily(person.FamilyId)?.FamilyName ?? Translate("HR_Genealogy_Unknown")));
            listing.Label(string.Format(Translate("HR_Genealogy_Birth"), FormatDate(person.BirthDate), Translate("HR_Genealogy_BirthSource_" + person.BirthDateSource)));
            HistoricalCharacterManager.TryGetCurrentHistoricalAge(person.PersonId, out int historicalAge);
            listing.Label(string.Format(Translate("HR_Genealogy_HistoricalAge"), historicalAge));
            if (HistoricalCharacterManager.TryGetPawn(person.PersonId, out Pawn pawn) && !pawn.Dead)
                listing.Label(string.Format(Translate("HR_Genealogy_BiologicalAge"), pawn.ageTracker.AgeBiologicalYears));
            listing.Label(string.Format(Translate("HR_Genealogy_Status"), Translate(person.IsAlive ? "HR_Genealogy_Alive" : "HR_Genealogy_Dead")));
            if (!person.IsAlive && person.DeathDate.HasValue)
                listing.Label(string.Format(Translate("HR_Genealogy_DeathDate"), FormatDate(person.DeathDate.Value), Translate("HR_Genealogy_DeathCause_" + person.LastDeathCause)));
            listing.Label(string.Format(Translate("HR_Genealogy_Region"), ResolveDefLabel(person.CurrentRegionId)));
            listing.Label(string.Format(Translate("HR_Genealogy_Polity"), ResolveDefLabel(person.CurrentAllegiancePolityId)));

            HistoricalNaturalLifespanRecord lifespan = HistoricalLifespanManager.Find(person.PersonId);
            if (lifespan != null)
            {
                listing.Label(string.Format(Translate("HR_Genealogy_NaturalDeathDate"), FormatDate(lifespan.NaturalDeathDate)));
                if (person.IsAlive)
                {
                    long remaining = currentDate.DaysUntil(lifespan.NaturalDeathDate);
                    string formattedRemaining = FormatRemaining(Math.Max(0, remaining));
                    listing.Label(string.Format(Translate("HR_Genealogy_Remaining"), formattedRemaining));
                    if (remaining <= HistoricalCalendarConstants.DaysPerYear * 2L)
                        listing.Label(Translate("HR_Genealogy_WithinTwoYears"));
                }
            }
            DrawImmediateKinship(listing, person.PersonId);
            listing.End();
        }

        private static void DrawImmediateKinship(Listing_Standard listing, string personId)
        {
            IEnumerable<string> parentIds = HistoricalGenealogyManager.RelationsFor(personId)
                .Where(item => item.Type != HistoricalKinshipType.Spouse)
                .Select(item => item.PersonIdB == personId ? item.PersonIdA : null).Where(id => id != null);
            IEnumerable<string> childIds = HistoricalGenealogyManager.RelationsFor(personId)
                .Where(item => item.Type != HistoricalKinshipType.Spouse && item.PersonIdA == personId)
                .Select(item => item.PersonIdB);
            IEnumerable<string> spouseIds = HistoricalGenealogyManager.RelationsFor(personId)
                .Where(item => item.Type == HistoricalKinshipType.Spouse && item.IsActive)
                .Select(item => item.PersonIdA == personId ? item.PersonIdB : item.PersonIdA);
            listing.Label(string.Format(Translate("HR_Genealogy_Parents"), JoinNames(parentIds)));
            listing.Label(string.Format(Translate("HR_Genealogy_Children"), JoinNames(childIds)));
            listing.Label(string.Format(Translate("HR_Genealogy_Spouses"), JoinNames(spouseIds)));
        }

        private static string JoinNames(IEnumerable<string> ids)
        {
            List<string> names = (ids ?? Enumerable.Empty<string>()).Select(HistoricalGenealogyManager.FindPerson)
                .Where(item => item != null).Select(item => item.DisplayName).ToList();
            return names.Count == 0 ? Translate("HR_Genealogy_None") : string.Join(", ", names);
        }

        private static string FormatDate(HistoricalDate date) => HistoricalDateFormatter.Format(date);

        private static string FormatRemaining(long days)
        {
            long years = days / HistoricalCalendarConstants.DaysPerYear;
            long remainder = days % HistoricalCalendarConstants.DaysPerYear;
            long months = remainder / HistoricalCalendarConstants.DaysPerMonth;
            long monthDays = remainder % HistoricalCalendarConstants.DaysPerMonth;
            return string.Format(Translate("HR_Genealogy_RemainingParts"), years, months, monthDays);
        }

        private static string ResolveDefLabel(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return Translate("HR_Genealogy_Unknown");
            HistoricalRegionDef region = DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(id);
            if (region != null) return region.LabelCap;
            HistoricalStateDef polity = DefDatabase<HistoricalStateDef>.GetNamedSilentFail(id);
            if (polity != null) return string.IsNullOrWhiteSpace(polity.historicalName) ? polity.LabelCap : polity.historicalName;
            return string.Format(Translate("HR_Genealogy_UnresolvedDef"), id);
        }

        private static string Translate(string key) => Translator.Translate(key);
    }
}
