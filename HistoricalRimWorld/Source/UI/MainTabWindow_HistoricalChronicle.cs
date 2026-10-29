using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_HistoricalChronicle : MainTabWindow
    {
        private const int EntriesPerPage = 20;
        private static Vector2 scrollPosition;
        private static int typeFilter = -1;
        private static int categoryFilter = -1;
        private static string regionFilter;
        private static string polityFilter;
        private int page;

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label(Translator.Translate("HR_Chronicle_Title"));
            if (!HistoricalScenarioManager.IsScenarioActive || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate))
            {
                listing.Label(Translator.Translate("HR_Chronicle_Inactive"));
                listing.End();
                return;
            }

            List<HistoricalChronicleEntry> allEntries = HistoricalChronicleManager.Entries
                .Where(item => item != null && item.VisibleToPlayer && item.KnownAbsoluteDay.HasValue
                    && item.KnownAbsoluteDay.Value <= currentDate.AbsoluteDay)
                .OrderBy(item => item.AbsoluteDay).ThenBy(item => item.EntryId, StringComparer.Ordinal).ToList();
            List<HistoricalChronicleEntry> entries = ApplyFilters(allEntries);

            if (listing.ButtonText(FilterLabel("HR_Chronicle_FilterType", typeFilter < 0 ? "HR_Chronicle_All" : "HR_Chronicle_Type_" + (HistoricalChronicleEntryType)typeFilter)))
                typeFilter = typeFilter >= Enum.GetValues(typeof(HistoricalChronicleEntryType)).Length - 1 ? -1 : typeFilter + 1;
            if (listing.ButtonText(FilterLabel("HR_Chronicle_FilterCategory", categoryFilter < 0 ? "HR_Chronicle_All" : "HR_Chronicle_Category_" + (HistoricalEventCategory)categoryFilter)))
                categoryFilter = categoryFilter >= Enum.GetValues(typeof(HistoricalEventCategory)).Length - 1 ? -1 : categoryFilter + 1;
            if (listing.ButtonText(FilterLabel("HR_Chronicle_FilterRegion", ResolveFilterLabel(regionFilter, allEntries.Select(item => item.RegionId)))))
                regionFilter = NextFilter(regionFilter, allEntries.Select(item => item.RegionId));
            if (listing.ButtonText(FilterLabel("HR_Chronicle_FilterPolity", ResolveFilterLabel(polityFilter, allEntries.Select(item => item.PolityId)))))
                polityFilter = NextFilter(polityFilter, allEntries.Select(item => item.PolityId));

            int pendingCount = HistoricalEventManager.States.Count(item => item.Status == HistoricalEventExecutionStatus.Pending);
            int failedCount = HistoricalEventManager.States.Count(item => item.Status == HistoricalEventExecutionStatus.Failed);
            int executedCount = HistoricalEventManager.States.Count(item => item.Status == HistoricalEventExecutionStatus.Executed);
            int skippedCount = HistoricalEventManager.States.Count(item => item.Status == HistoricalEventExecutionStatus.Skipped);
            listing.Label(string.Format(Translator.Translate("HR_Chronicle_EventStatus"), pendingCount, executedCount, skippedCount, failedCount));
            if (Prefs.DevMode)
            {
                foreach (HistoricalEventRuntimeState failed in HistoricalEventManager.States
                    .Where(item => item.Status == HistoricalEventExecutionStatus.Failed).Take(3))
                    listing.Label(string.Format(Translator.Translate("HR_Chronicle_EventFailure"), failed.EventId, failed.FailureReason));
            }

            int pageCount = Math.Max(1, (entries.Count + EntriesPerPage - 1) / EntriesPerPage);
            page = Mathf.Clamp(page, 0, pageCount - 1);
            listing.Label(string.Format(Translator.Translate("HR_Chronicle_Page"), page + 1, pageCount, entries.Count));
            listing.GapLine();
            listing.End();

            float headerHeight = Prefs.DevMode ? 310f : 220f;
            Rect listRect = new Rect(inRect.x, inRect.y + headerHeight, inRect.width, inRect.height - headerHeight - 30f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 18f, EntriesPerPage * 160f);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            Listing_Standard rows = new Listing_Standard();
            rows.Begin(viewRect);
            foreach (HistoricalChronicleEntry entry in entries.Skip(page * EntriesPerPage).Take(EntriesPerPage))
            {
                rows.Label(string.Format(Translator.Translate("HR_Chronicle_OccurredAndLearned"), FormatDate(entry), FormatKnownDate(entry))
                    + "  ·  " + Translator.Translate("HR_Chronicle_Type_" + entry.EntryType));
                rows.Label(Translator.Translate(entry.TitleKey ?? "HR_Chronicle_Untitled"));
                rows.Label(Translator.Translate(entry.DescriptionKey ?? "HR_Chronicle_NoDescription"));
                rows.Label(string.Format(Translator.Translate("HR_Chronicle_Metadata"),
                    Translator.Translate("HR_Chronicle_Category_" + entry.Category),
                    Translator.Translate("HR_Chronicle_Evidence_" + entry.EvidenceLevel),
                    Translator.Translate("HR_Chronicle_Provenance_" + entry.Provenance),
                    Translator.Translate("HR_Chronicle_Chronology_" + entry.ChronologyTradition),
                    string.IsNullOrEmpty(entry.SourceNotes) ? Translator.Translate("HR_Chronicle_NoSource") : Translator.Translate(entry.SourceNotes)));
                rows.GapLine();
            }
            rows.End();
            Widgets.EndScrollView();

            Rect pageButtons = new Rect(inRect.x, inRect.yMax - 28f, inRect.width, 28f);
            if (Widgets.ButtonText(new Rect(pageButtons.x, pageButtons.y, 100f, pageButtons.height), Translator.Translate("HR_Chronicle_Previous")))
                page = Math.Max(0, page - 1);
            if (Widgets.ButtonText(new Rect(pageButtons.xMax - 100f, pageButtons.y, 100f, pageButtons.height), Translator.Translate("HR_Chronicle_Next")))
                page = Math.Min(pageCount - 1, page + 1);
        }

        private static List<HistoricalChronicleEntry> ApplyFilters(List<HistoricalChronicleEntry> entries)
        {
            IEnumerable<HistoricalChronicleEntry> result = entries;
            if (typeFilter >= 0) result = result.Where(item => (int)item.EntryType == typeFilter);
            if (categoryFilter >= 0) result = result.Where(item => (int)item.Category == categoryFilter);
            if (regionFilter != null) result = result.Where(item => item.RegionId == regionFilter);
            if (polityFilter != null) result = result.Where(item => item.PolityId == polityFilter);
            return result.ToList();
        }

        private static string FilterLabel(string key, string value) =>
            string.Format(Translator.Translate(key), Translator.Translate(value));

        private static string ResolveFilterLabel(string selected, IEnumerable<string> values)
        {
            if (selected == null) return "HR_Chronicle_All";
            return (values ?? Enumerable.Empty<string>()).FirstOrDefault(item => item == selected) ?? "HR_Chronicle_All";
        }

        private static string NextFilter(string selected, IEnumerable<string> values)
        {
            List<string> options = (values ?? Enumerable.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();
            if (options.Count == 0) return null;
            int current = selected == null ? -1 : options.IndexOf(selected);
            return current + 1 >= options.Count ? null : options[current + 1];
        }

        private static string FormatDate(HistoricalChronicleEntry entry)
        {
            HistoricalYearConverter.FromAstronomicalYear(entry.AstronomicalYear, out int year, out bool bce);
            string era = Translator.Translate(bce ? "HR_Chronicle_BCE" : "HR_Chronicle_CE");
            string date = string.Format(Translator.Translate("HR_Chronicle_Year"), year, era);
            if (entry.Precision >= HistoricalDatePrecision.Month && entry.Month.HasValue)
                date += " " + Translator.Translate("HR_Calendar_Month_" + entry.Month.Value);
            if (entry.Precision == HistoricalDatePrecision.Day && entry.Day.HasValue)
                date += " " + Translator.Translate("HR_Calendar_Day_" + entry.Day.Value);
            return date;
        }

        private static string FormatKnownDate(HistoricalChronicleEntry entry)
        {
            if (!entry.KnownAstronomicalYear.HasValue) return Translator.Translate("HR_Chronicle_UnknownLearnedDate");
            HistoricalYearConverter.FromAstronomicalYear(entry.KnownAstronomicalYear.Value, out int year, out bool bce);
            string era = Translator.Translate(bce ? "HR_Chronicle_BCE" : "HR_Chronicle_CE");
            string date = string.Format(Translator.Translate("HR_Chronicle_Year"), year, era);
            if (entry.KnownPrecision >= HistoricalDatePrecision.Month && entry.KnownMonth.HasValue)
                date += " " + Translator.Translate("HR_Calendar_Month_" + entry.KnownMonth.Value);
            if (entry.KnownPrecision == HistoricalDatePrecision.Day && entry.KnownDay.HasValue)
                date += " " + Translator.Translate("HR_Calendar_Day_" + entry.KnownDay.Value);
            return date;
        }
    }
}
