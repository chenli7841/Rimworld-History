using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalEventDef : Def
    {
        public HistoricalScenarioDef scenario;
        public string eventId;
        public string historicalInterpretationId;
        public HistoricalDateReference announcementDate;
        public List<HistoricalEventStageEntry> stages = new List<HistoricalEventStageEntry>();
        public List<HistoricalEventOutcomeEntry> outcomes = new List<HistoricalEventOutcomeEntry>();
        public bool mandatoryHistoricalOutcome = true;
        public bool progressWithoutPlayer = true;
        public bool allowPlayerObservation = true;
        public HistoricalDateReference historicalDate;
        public HistoricalEventCategory category = HistoricalEventCategory.Other;
        public HistoricalEventScope scope = HistoricalEventScope.World;
        public int priority = 100;
        public List<string> prerequisiteEventIds = new List<string>();
        public List<HistoricalEventCondition> conditions = new List<HistoricalEventCondition>();
        public List<HistoricalEventAction> actions = new List<HistoricalEventAction>();
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;
        public HistoricalEventProvenance provenance = HistoricalEventProvenance.AttestedHistoricalEvent;
        public HistoricalChronologyTradition chronologyTradition = HistoricalChronologyTradition.TraditionalReceivedChronology;
        public HistoricalEventActivationMode activationMode = HistoricalEventActivationMode.Scheduled;
        public HistoricalEventVisibility visibility = HistoricalEventVisibility.Public;
        public HistoricalEventNotificationPolicy notificationPolicy = HistoricalEventNotificationPolicy.ChronicleOnly;
        public string regionId;
        public string polityId;
        public string sourceNotes;
        public bool recordInChronicle = true;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            foreach (string error in HistoricalEventValidator.ValidateDef(this)) yield return error;
        }
    }

    public sealed class HistoricalEventStageEntry
    {
        public string stageId;
        public HistoricalDateReference scheduledDate;
        public string displayLabelKey;
        public string notificationDescriptionKey;
        public bool showNotification;
        public bool recordInChronicle;
        public bool requiresScene;
        public bool canProgressOffMap = true;
    }

    public sealed class HistoricalEventOutcomeEntry
    {
        public string outcomeId;
        public string stageId;
        public string titleKey;
        public string descriptionKey;
        public bool mandatory = true;
        public bool recordInChronicle = true;
    }
}
