using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Events.Actions;
using HistoricalRimWorld.Events.Conditions;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Events
{
    public static class HistoricalEventValidator
    {
        public static IEnumerable<string> ValidateDef(HistoricalEventDef item)
        {
            if (item == null) yield break;
            if (item.scenario == null) yield return "scenario is required.";
            if (item.historicalDate == null)
            {
                if (item.activationMode != HistoricalEventActivationMode.LocalTemplate) yield return "historicalDate is required.";
            }
            if (item.stages != null && item.stages.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(item.eventId)) yield return "Timeline events require a stable eventId.";
                else if (DefDatabase<HistoricalEventDef>.AllDefsListForReading.Any(other => other != item && other.eventId == item.eventId))
                    yield return "eventId must be globally unique.";
                if (string.IsNullOrWhiteSpace(item.historicalInterpretationId)) yield return "Timeline events require historicalInterpretationId.";
                if (item.announcementDate == null) yield return "Timeline events require announcementDate.";
                else
                {
                    string announcementError = null;
                    try { item.announcementDate.Validate(); }
                    catch (Exception exception) { announcementError = exception.Message; }
                    if (announcementError != null) yield return "announcementDate is invalid: " + announcementError;
                }
                HistoricalDate? previous = null;
                HistoricalDate? firstStageDate = null;
                HashSet<string> stageIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (HistoricalEventStageEntry stage in item.stages)
                {
                    if (stage == null) { yield return "stages contains a null entry."; continue; }
                    if (string.IsNullOrWhiteSpace(stage.stageId) || !stageIds.Add(stage.stageId)) yield return "Stage IDs must be non-empty and unique.";
                    if (string.IsNullOrWhiteSpace(stage.displayLabelKey)) yield return "Stage " + stage.stageId + " requires a displayLabelKey.";
                    if (stage.scheduledDate == null) { yield return "Stage " + stage.stageId + " requires scheduledDate."; continue; }
                    string stageDateError = null;
                    HistoricalDate? stageDate = null;
                    try
                    {
                        stage.scheduledDate.Validate();
                        stageDate = HistoricalEventDateResolver.ResolveScheduledDate(stage.scheduledDate);
                    }
                    catch (Exception exception) { stageDateError = exception.Message; }
                    if (stageDateError != null) yield return "Stage " + stage.stageId + " has an invalid date: " + stageDateError;
                    else if (stageDate.HasValue)
                    {
                        if (!firstStageDate.HasValue) firstStageDate = stageDate;
                        if (previous.HasValue && stageDate.Value <= previous.Value) yield return "Stage dates must be strictly increasing.";
                        previous = stageDate;
                    }
                    if (stage.showNotification && string.IsNullOrWhiteSpace(stage.notificationDescriptionKey))
                        yield return "Notifying stage " + stage.stageId + " requires notificationDescriptionKey.";
                    if (!item.progressWithoutPlayer && stage.canProgressOffMap) yield return "canProgressOffMap requires progressWithoutPlayer.";
                }
                if (item.announcementDate != null && firstStageDate.HasValue)
                {
                    string announcementOrderError = null;
                    try
                    {
                        if (HistoricalEventDateResolver.ResolveScheduledDate(item.announcementDate) > firstStageDate.Value)
                            announcementOrderError = "announcementDate must not be later than the first stage.";
                    }
                    catch (Exception exception) { announcementOrderError = "announcementDate is invalid: " + exception.Message; }
                    if (announcementOrderError != null) yield return announcementOrderError;
                }
                HashSet<string> outcomeIds = new HashSet<string>(StringComparer.Ordinal);
                if (DefDatabase<HistoricalEventDef>.AllDefsListForReading.Where(other => other != item)
                    .SelectMany(other => other.outcomes ?? new List<HistoricalEventOutcomeEntry>())
                    .Any(otherOutcome => otherOutcome != null && (item.outcomes ?? new List<HistoricalEventOutcomeEntry>())
                        .Any(outcome => outcome != null && outcome.outcomeId == otherOutcome.outcomeId)))
                    yield return "Outcome IDs must be globally unique.";
                foreach (HistoricalEventOutcomeEntry outcome in item.outcomes ?? new List<HistoricalEventOutcomeEntry>())
                {
                    if (outcome == null) { yield return "outcomes contains a null entry."; continue; }
                    if (string.IsNullOrWhiteSpace(outcome.outcomeId) || !outcomeIds.Add(outcome.outcomeId)) yield return "Outcome IDs must be non-empty and unique.";
                    if (!stageIds.Contains(outcome.stageId)) yield return "Outcome " + outcome.outcomeId + " references a missing stage.";
                    if (string.IsNullOrWhiteSpace(outcome.titleKey) || string.IsNullOrWhiteSpace(outcome.descriptionKey)) yield return "Outcome " + outcome.outcomeId + " requires localized title and description keys.";
                }
                if (item.mandatoryHistoricalOutcome && !(item.outcomes ?? new List<HistoricalEventOutcomeEntry>()).Any(outcome => outcome != null && outcome.mandatory))
                    yield return "mandatoryHistoricalOutcome requires at least one mandatory outcome.";
                if (item.stages.Any(stage => stage != null && stage.requiresScene && !stage.canProgressOffMap))
                    yield return "A scene-gated timeline cannot pause when the player is off-map.";
            }
            else
            {
                string dateError = null;
                try { item.historicalDate.Validate(); }
                catch (Exception exception) { dateError = exception.Message; }
                if (dateError != null) yield return "historicalDate is invalid: " + dateError;
            }
            if (!Enum.IsDefined(typeof(HistoricalEventCategory), item.category)) yield return "category is invalid.";
            if (!Enum.IsDefined(typeof(HistoricalEventScope), item.scope)) yield return "scope is invalid.";
            if (!Enum.IsDefined(typeof(HistoricalEventActivationMode), item.activationMode)) yield return "activationMode is invalid.";
            if (!Enum.IsDefined(typeof(HistoricalEventVisibility), item.visibility)) yield return "visibility is invalid.";
            if (!Enum.IsDefined(typeof(HistoricalEventNotificationPolicy), item.notificationPolicy)) yield return "notificationPolicy is invalid.";
            if (!Enum.IsDefined(typeof(HistoricalEvidenceLevel), item.evidenceLevel)) yield return "evidenceLevel is invalid.";
            if (!Enum.IsDefined(typeof(HistoricalEventProvenance), item.provenance)) yield return "provenance is invalid.";
            if (!Enum.IsDefined(typeof(HistoricalChronologyTradition), item.chronologyTradition)) yield return "chronologyTradition is invalid.";
            if (string.IsNullOrWhiteSpace(item.label)) yield return "a localized label key is required.";
            if (string.IsNullOrWhiteSpace(item.description)) yield return "a localized description key is required.";
            if (item.prerequisiteEventIds == null) yield return "prerequisiteEventIds cannot be null.";
            else
            {
                if (item.prerequisiteEventIds.Contains(item.defName, StringComparer.Ordinal)) yield return "An event cannot depend on itself.";
                if (item.prerequisiteEventIds.Distinct(StringComparer.Ordinal).Count() != item.prerequisiteEventIds.Count)
                    yield return "prerequisiteEventIds contains duplicates.";
            }
            if (item.conditions == null) yield return "conditions cannot be null.";
            else foreach (HistoricalEventCondition condition in item.conditions)
            {
                if (condition == null) yield return "conditions contains a null entry.";
                else if (!condition.ValidateData(out string conditionError)) yield return "Condition data is invalid: " + conditionError;
                else if (condition is Condition_EventExecuted eventCondition && eventCondition.eventId == item.defName)
                    yield return "An event cannot require itself to have executed.";
            }
            if (item.actions == null) yield return "actions cannot be null.";
            else
            {
                foreach (HistoricalEventAction action in item.actions)
                {
                    if (action == null) yield return "actions contains a null entry.";
                    else if (!action.ValidateData(out string actionError)) yield return "Action data is invalid: " + actionError;
                }
                if (item.actions.Count(action => action != null && action.IsStateMutation) > 1)
                    yield return "An event may contain at most one state mutation in this release; this is the supported cross-module atomicity boundary.";
                if (item.actions.Count(action => action is Action_AddChronicleEntry) > 1)
                    yield return "An event may contain at most one custom chronicle entry action.";
            }
            if (item.activationMode == HistoricalEventActivationMode.InitialBackground && item.actions != null && item.actions.Any())
                yield return "InitialBackground events are chronicle-only and cannot contain actions.";
            if (item.activationMode == HistoricalEventActivationMode.InitialBackground && !item.recordInChronicle)
                yield return "InitialBackground events must be recorded in the chronicle.";
            if (item.activationMode == HistoricalEventActivationMode.DevelopmentOnly && item.evidenceLevel != HistoricalEvidenceLevel.Speculative)
                yield return "DevelopmentOnly events must be marked Speculative.";
            if (item.activationMode == HistoricalEventActivationMode.LocalTemplate
                && (item.scope != HistoricalEventScope.LocalCommunity || item.provenance != HistoricalEventProvenance.FictionalLocalEvent
                    || item.actions == null || item.actions.Count != 0))
                yield return "LocalTemplate events must be fictional local-community records without historical actions.";
            if (item.scope == HistoricalEventScope.Region && string.IsNullOrWhiteSpace(item.regionId)) yield return "Region scope requires regionId.";
            if (item.regionId != null && DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(item.regionId) == null)
                yield return "regionId references a missing historical region Def.";
            if (item.polityId != null && DefDatabase<HistoricalStateDef>.GetNamedSilentFail(item.polityId) == null)
                yield return "polityId references a missing historical polity Def.";
            if (item.visibility == HistoricalEventVisibility.RegionKnowledgeRequired && string.IsNullOrWhiteSpace(item.regionId))
                yield return "RegionKnowledgeRequired visibility requires regionId.";
            if (item.notificationPolicy == HistoricalEventNotificationPolicy.Letter && string.IsNullOrWhiteSpace(item.label))
                yield return "Letter notifications require a localized event label.";

            if (item.scenario != null && item.prerequisiteEventIds != null)
            {
                foreach (string prerequisiteId in item.prerequisiteEventIds)
                {
                    if (string.IsNullOrWhiteSpace(prerequisiteId))
                    {
                        yield return "prerequisiteEventIds contains an empty event ID.";
                        continue;
                    }
                    HistoricalEventDef prerequisite = FindEventDef(prerequisiteId);
                    if (prerequisite == null) yield return "Missing prerequisite event " + prerequisiteId + ".";
                    else if (prerequisite.scenario == null || prerequisite.scenario.defName != item.scenario.defName)
                        yield return "Prerequisite " + prerequisiteId + " belongs to another scenario.";
                }
            }
            if (item.scenario != null && item.conditions != null)
            {
                foreach (Condition_EventExecuted condition in item.conditions.OfType<Condition_EventExecuted>())
                {
                    HistoricalEventDef prerequisite = FindEventDef(condition.eventId);
                    if (prerequisite != null && (prerequisite.scenario == null || prerequisite.scenario.defName != item.scenario.defName))
                        yield return "EventExecuted condition " + condition.eventId + " belongs to another scenario.";
                }
            }
        }

        public static IEnumerable<string> ValidateScenarioEvents(HistoricalScenarioDef scenario)
        {
            if (scenario == null) yield break;
            List<HistoricalEventDef> events = DefDatabase<HistoricalEventDef>.AllDefsListForReading
                .Where(item => item.scenario != null && item.scenario.defName == scenario.defName).ToList();
            foreach (var duplicate in events.Where(item => !string.IsNullOrWhiteSpace(item.eventId))
                .GroupBy(item => item.eventId, StringComparer.Ordinal).Where(group => group.Count() > 1))
                yield return "Duplicate eventId mapping: " + duplicate.Key + ".";
            foreach (HistoricalEventDef item in events.Where(item => !string.IsNullOrWhiteSpace(item.eventId) && item.eventId != item.defName))
                if (events.Any(other => other.defName == item.eventId)) yield return "eventId " + item.eventId + " conflicts with another DefName.";
            Dictionary<string, HistoricalEventDef> byId = events.ToDictionary(item => item.defName, StringComparer.Ordinal);
            Dictionary<string, int> marks = new Dictionary<string, int>(StringComparer.Ordinal);

            bool Visit(string id)
            {
                int mark;
                if (marks.TryGetValue(id, out mark)) return mark == 1;
                marks[id] = 1;
                HistoricalEventDef current;
                if (byId.TryGetValue(id, out current) && current.prerequisiteEventIds != null)
                {
                    IEnumerable<string> dependencies = current.prerequisiteEventIds;
                    if (current.conditions != null)
                        dependencies = dependencies.Concat(current.conditions.OfType<Condition_EventExecuted>().Select(condition => condition.eventId));
                    foreach (string prerequisite in dependencies)
                    {
                        if (string.IsNullOrWhiteSpace(prerequisite)) continue;
                        if (byId.ContainsKey(prerequisite) && Visit(prerequisite)) return true;
                    }
                }
                marks[id] = 2;
                return false;
            }

            HistoricalDate? scenarioStart = null;
            try { scenarioStart = scenario.GetStartDate(); }
            catch (Exception) { }
            foreach (HistoricalEventDef item in events)
            {
                if (item.activationMode == HistoricalEventActivationMode.InitialBackground
                    && scenarioStart.HasValue && item.historicalDate != null)
                {
                    HistoricalDate scheduled;
                    try { scheduled = HistoricalEventDateResolver.ResolveScheduledDate(item.historicalDate); }
                    catch (Exception) { scheduled = scenarioStart.Value; }
                    if (scheduled > scenarioStart.Value)
                        yield return "InitialBackground event " + item.defName + " is dated after the scenario start.";
                }
                if (Visit(item.defName))
                {
                    yield return "Prerequisite cycle detected in scenario " + scenario.defName + " at event " + item.defName + ".";
                    yield break;
                }
            }
        }

        internal static HistoricalEventDef FindEventDef(string id) =>
            DefDatabase<HistoricalEventDef>.GetNamedSilentFail(id)
            ?? DefDatabase<HistoricalEventDef>.AllDefsListForReading.FirstOrDefault(def => def.eventId == id);
    }
}
