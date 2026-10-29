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
                HistoricalEventCategory.Other, HistoricalEventScope.LocalCommunity, true,
                HistoricalEventProvenance.HistoricalContextSimulation, knownDate: reference))
                || Reject("Could not add local chronicle entry.", out error);
        }

        internal static bool TryAddInteractiveLocalEventEntry(HistoricalEventDef source, string entryId,
            string titleKey, string descriptionKey, out string error)
        {
            error = null;
            if (source == null || source.activationMode != HistoricalEventActivationMode.LocalTemplate)
                return Reject("A LocalTemplate HistoricalEventDef is required.", out error);
            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date))
                return Reject("The historical calendar is inactive.", out error);
            if (!TryGetInitializedComponent(out HistoricalChronicleGameComponent component, out error)) return false;
            HistoricalDateReference reference = new HistoricalDateReference(date.AstronomicalYear, date.Month, date.Day, HistoricalDatePrecision.Day);
            HistoricalChronicleEntry entry = CreateEntry(entryId, source.defName, HistoricalChronicleEntryType.FictionalLocalEvent,
                reference, titleKey, descriptionKey, source.regionId, source.polityId, HistoricalEvidenceLevel.Speculative,
                source.sourceNotes, source.category, HistoricalEventScope.LocalCommunity, true,
                HistoricalEventProvenance.FictionalLocalEvent, source.chronologyTradition, reference);
            return component.AddEntry(entry) || Reject("Could not add local event choice to the chronicle.", out error);
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
            HistoricalChronicleEntryType type = item.provenance == HistoricalEventProvenance.HistoricalReconstruction
                ? HistoricalChronicleEntryType.HistoricalReconstruction
                : item.provenance == HistoricalEventProvenance.HistoricalContextSimulation
                    ? HistoricalChronicleEntryType.HistoricalContext : HistoricalChronicleEntryType.AttestedHistory;
            HistoricalDateReference knownDate = null;
            if (visible && HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate learnedOn))
                knownDate = new HistoricalDateReference(learnedOn.AstronomicalYear, learnedOn.Month, learnedOn.Day, HistoricalDatePrecision.Day);
            return component.AddEntry(CreateEntry("Event:" + item.defName, item.defName,
                type, date, item.label, item.description,
                item.regionId, item.polityId, item.evidenceLevel, item.sourceNotes, item.category, item.scope, visible,
                item.provenance, item.chronologyTradition, knownDate))
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
            HistoricalEventDef source = string.IsNullOrWhiteSpace(sourceEventId)
                ? null : DefDatabase<HistoricalEventDef>.GetNamedSilentFail(sourceEventId);
            HistoricalEventProvenance provenance = source?.provenance ?? HistoricalEventProvenance.AttestedHistoricalEvent;
            HistoricalChronicleEntryType type = provenance == HistoricalEventProvenance.HistoricalReconstruction
                ? HistoricalChronicleEntryType.HistoricalReconstruction
                : provenance == HistoricalEventProvenance.HistoricalContextSimulation
                    ? HistoricalChronicleEntryType.HistoricalContext : HistoricalChronicleEntryType.AttestedHistory;
            return component.AddEntry(CreateEntry(entryId, sourceEventId, type,
                date, titleKey, descriptionKey, regionId, polityId, evidence, sourceNotes, category, scope, visible,
                provenance, source?.chronologyTradition ?? HistoricalChronologyTradition.TraditionalReceivedChronology,
                visible && HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate learnedOn)
                    ? new HistoricalDateReference(learnedOn.AstronomicalYear, learnedOn.Month, learnedOn.Day, HistoricalDatePrecision.Day) : null))
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
                    && access == HistoricalRegionAccess.Revealed
                    && HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date))
                    entry.SetVisible(true, new HistoricalDateReference(date.AstronomicalYear, date.Month, date.Day, HistoricalDatePrecision.Day));
            }
        }

        public static bool TryRevealEventToPlayer(string eventId, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(eventId) || !HistoricalEventManager.WasExecuted(eventId))
                return Reject("Only a historical event that has already occurred can be revealed.", out error);
            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate today))
                return Reject("The historical calendar is inactive.", out error);
            HistoricalChronicleEntry entry = Entries.FirstOrDefault(item => item != null && item.SourceEventId == eventId);
            if (entry == null) return Reject("The historical event has no chronicle record.", out error);
            if (entry.VisibleToPlayer) return true;
            entry.SetVisible(true, new HistoricalDateReference(today.AstronomicalYear, today.Month, today.Day, HistoricalDatePrecision.Day));
            return true;
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
            HistoricalEventCategory category, HistoricalEventScope scope, bool visible,
            HistoricalEventProvenance provenance = HistoricalEventProvenance.AttestedHistoricalEvent,
            HistoricalChronologyTradition tradition = HistoricalChronologyTradition.TraditionalReceivedChronology,
            HistoricalDateReference knownDate = null)
        {
            HistoricalChronicleEntry entry = new HistoricalChronicleEntry();
            entry.Initialize(id, sourceEventId, type, date, title, description, region, polity, evidence, sources, category, scope, visible, provenance, tradition, knownDate ?? (visible ? date : null));
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
