using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using Verse;

namespace HistoricalRimWorld.Chronicle
{
    public static class HistoricalChronicleManager
    {
        public static bool IsActive => GetComponent()?.Initialized == true;
        public static IEnumerable<HistoricalChronicleEntry> Entries => GetComponent()?.Entries ?? Enumerable.Empty<HistoricalChronicleEntry>();

        public static bool TryAddLocalExperience(string entryId, string titleKey, string descriptionKey,
            string regionId, string polityId, HistoricalEvidenceLevel evidence, string sourceNotes, out string error)
        {
            HistoricalDate date;
            error = null;
            if (!HistoricalCalendarManager.TryGetCurrentDate(out date)) return Reject("The historical calendar is inactive.", out error);
            if (!TryGetInitializedComponent(out HistoricalChronicleGameComponent component, out error)) return false;
            HistoricalDateReference reference = new HistoricalDateReference(date.AstronomicalYear, date.Month, date.Day, HistoricalDatePrecision.Day);
            return component.AddEntry(CreateEntry(entryId, null, HistoricalChronicleEntryType.LocalExperience,
                reference, titleKey, descriptionKey, regionId, polityId, evidence, sourceNotes,
                HistoricalEventCategory.Other, HistoricalEventScope.LocalCommunity, true))
                || Reject("Could not add local chronicle entry.", out error);
        }

        public static bool TryAddInferredLegacy(string entryId, HistoricalDateReference date, string titleKey,
            string descriptionKey, string regionId, string polityId, string sourceNotes, bool visible, out string error)
        {
            error = null;
            if (date == null) return Reject("A historical date is required.", out error);
            try { date.Validate(); }
            catch (Exception exception) { return Reject("Invalid date: " + exception.Message, out error); }
            if (!TryGetInitializedComponent(out HistoricalChronicleGameComponent component, out error)) return false;
            return component.AddEntry(CreateEntry(entryId, null, HistoricalChronicleEntryType.InferredLegacy,
                date, titleKey, descriptionKey, regionId, polityId, HistoricalEvidenceLevel.Speculative, sourceNotes,
                HistoricalEventCategory.Other, HistoricalEventScope.LocalCommunity, visible))
                || Reject("Could not add legacy chronicle entry.", out error);
        }

        internal static bool TryInitializeForScenario(string scenarioId, out string error)
        {
            error = null;
            if (Current.Game == null) return Reject("A game must be loaded.", out error);
            HistoricalChronicleGameComponent component = GetComponent();
            if (component == null) return Reject("HistoricalChronicleGameComponent is unavailable.", out error);
            if (!component.InitializeForScenario(scenarioId)) return Reject("Chronicle is initialized for another scenario.", out error);
            return true;
        }

        internal static bool TryAddEventEntry(HistoricalEventDef item, HistoricalDateReference date, bool visible, out string error)
        {
            error = null;
            if (!TryGetInitializedComponent(out HistoricalChronicleGameComponent component, out error)) return false;
            return component.AddEntry(CreateEntry("Event:" + item.defName, item.defName,
                HistoricalChronicleEntryType.AttestedHistory, date, item.label, item.description,
                item.regionId, item.polityId, item.evidenceLevel, item.sourceNotes, item.category, item.scope, visible))
                || Reject("Could not add historical event to the chronicle.", out error);
        }

        internal static bool TryAddCustomEventEntry(string entryId, string sourceEventId, HistoricalDateReference date,
            string titleKey, string descriptionKey, string regionId, string polityId, HistoricalEvidenceLevel evidence,
            string sourceNotes, HistoricalEventCategory category, HistoricalEventScope scope, bool visible, out string error)
        {
            error = null;
            if (date == null) return Reject("A historical date is required.", out error);
            try { date.Validate(); }
            catch (Exception exception) { return Reject("Invalid date: " + exception.Message, out error); }
            if (!TryGetInitializedComponent(out HistoricalChronicleGameComponent component, out error)) return false;
            return component.AddEntry(CreateEntry(entryId, sourceEventId, HistoricalChronicleEntryType.AttestedHistory,
                date, titleKey, descriptionKey, regionId, polityId, evidence, sourceNotes, category, scope, visible))
                || Reject("Could not add historical chronicle entry.", out error);
        }

        internal static void RefreshRegionVisibility()
        {
            HistoricalChronicleGameComponent component = GetComponent();
            if (component == null || !component.Initialized) return;
            foreach (HistoricalChronicleEntry entry in component.Entries)
            {
                if (entry.VisibleToPlayer || entry.EntryType != HistoricalChronicleEntryType.AttestedHistory
                    || string.IsNullOrWhiteSpace(entry.RegionId)) continue;
                HistoricalRegionAccess access;
                if (HistoricalWorldRevealManager.TryGetRegionAccess(entry.RegionId, out access)
                    && access == HistoricalRegionAccess.Revealed) entry.SetVisible(true);
            }
        }

        public static bool IsKnownRegion(string regionId)
        {
            HistoricalRegionAccess access;
            return HistoricalWorldRevealManager.TryGetRegionAccess(regionId, out access)
                && access == HistoricalRegionAccess.Revealed;
        }

        private static HistoricalChronicleEntry CreateEntry(string id, string sourceEventId,
            HistoricalChronicleEntryType type, HistoricalDateReference date, string title, string description,
            string region, string polity, HistoricalEvidenceLevel evidence, string sources,
            HistoricalEventCategory category, HistoricalEventScope scope, bool visible)
        {
            HistoricalChronicleEntry entry = new HistoricalChronicleEntry();
            entry.Initialize(id, sourceEventId, type, date, title, description, region, polity, evidence, sources, category, scope, visible);
            return entry;
        }

        private static HistoricalChronicleGameComponent GetComponent() =>
            Current.Game == null ? null : Current.Game.GetComponent<HistoricalChronicleGameComponent>();

        private static bool TryGetInitializedComponent(out HistoricalChronicleGameComponent component, out string error)
        {
            component = GetComponent();
            if (component != null && component.Initialized) { error = null; return true; }
            return Reject("Historical Chronicle is not initialized for a historical scenario.", out error);
        }

        private static bool Reject(string message, out string error) { error = message; return false; }
    }
}
