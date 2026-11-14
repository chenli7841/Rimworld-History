using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Events
{
    /// <summary>
    /// Presents HE-001's existing authoritative event timeline as one native, persistent Quest.
    /// Stage state remains owned by HistoricalEventGameComponent and its scheduler.
    /// </summary>
    public static class HistoricalEventLongTermQuestBridge
    {
        private const string EventId = "HE_001_LishanCrisis";
        private const string ScenarioId = "HR_Scenario_EasternZhou_770BCE";
        private const string QuestTag = "HR_Quest_HE001_HistoricalChronicle";
        private const string QuestRootDefName = "HR_HistoricalChronicleQuestRoot";

        public static void Reconcile(HistoricalEventDef def, HistoricalEventRuntimeState state)
        {
            if (def == null || state == null || def.stages == null
                || !string.Equals(def.eventId ?? def.defName, EventId, StringComparison.Ordinal)
                || !HistoricalScenarioManager.IsScenarioActive
                || !string.Equals(HistoricalScenarioManager.ActiveScenarioDefName, ScenarioId, StringComparison.Ordinal)
                || !state.HasReachedStage("Forewarning")) return;

            HistoricalEventGameComponent component = Current.Game?.GetComponent<HistoricalEventGameComponent>();
            Quest quest = component?.LishanCrisisQuest;
            if (quest == null)
            {
                quest = Verse.Find.QuestManager?.QuestsListForReading?.FirstOrDefault(item => item != null
                    && item.tags != null && item.tags.Contains(QuestTag));
                if (quest == null) quest = CreateQuest();
                component?.SetLishanCrisisQuest(quest);
            }
            if (quest == null) return;
            EnsureQuestRoot(quest);
            RefreshQuestText(quest, def, state);

            foreach (HistoricalEventStageEntry stage in def.stages)
            {
                if (stage == null || !state.HasReachedStage(stage.stageId)) continue;
                string stageTag = StageTag(stage.stageId);
                if (quest.tags == null) quest.tags = new List<string>();
                if (!quest.tags.Contains(stageTag)) quest.tags.Add(stageTag);

                if (stage.stageId == "Completed" && quest.State == QuestState.Ongoing)
                    quest.End(QuestEndOutcome.Success, true, true);
            }
        }

        private static Quest CreateQuest()
        {
            QuestManager manager = Verse.Find.QuestManager;
            if (manager == null) return null;

            Quest quest = new Quest
            {
                name = "HE001_LongTermQuest_Name".Translate(),
                description = "HE_001_Notice_Forewarning".Translate(),
                initiallyAccepted = true,
                tags = new List<string> { "HistoricalRimWorld", EventId, QuestTag },
                root = DefDatabase<QuestScriptDef>.GetNamedSilentFail(QuestRootDefName)
            };
            if (quest.root == null)
                Log.Error("[Historical RimWorld] Missing QuestScriptDef " + QuestRootDefName + "; the historical Quest may be incompatible with Quest UI extensions.");
            QuestPart_DescriptionPart description = quest.AddPart<QuestPart_DescriptionPart>();
            description.descriptionPart = "HE_001_Notice_Forewarning".Translate().ToString();
            manager.Add(quest);
            Log.Message("[Historical RimWorld] Started the persistent HE-001 historical Quest from the existing event timeline.");
            return quest;
        }

        public static void EnsureQuestRoot(Quest quest)
        {
            if (quest == null || quest.root != null) return;
            quest.root = DefDatabase<QuestScriptDef>.GetNamedSilentFail(QuestRootDefName);
            if (quest.root == null)
                Log.ErrorOnce("[Historical RimWorld] Could not assign the historical Quest metadata root " + QuestRootDefName + ".", QuestRootDefName.GetHashCode());
        }

        public static void RefreshQuestText(Quest quest, HistoricalEventDef def, HistoricalEventRuntimeState state)
        {
            if (quest == null || def?.stages == null || state == null) return;

            string opening = Translator.Translate("HE_001_Notice_Forewarning");
            StringBuilder log = new StringBuilder(opening);
            HistoricalEventStageEntry latest = null;
            foreach (HistoricalEventStageEntry stage in def.stages)
            {
                if (stage == null || !state.HasReachedStage(stage.stageId)) continue;
                latest = stage;
                if (stage.stageId == "Forewarning") continue;
                log.Append("\n\n").Append(FormatStageUpdate(stage));
            }

            QuestPart_DescriptionPart part = quest.GetFirstOrAddPart<QuestPart_DescriptionPart>();
            part.descriptionPart = log.ToString();
            quest.description = latest == null || latest.stageId == "Forewarning"
                ? opening
                : FormatCurrentStatus(latest);
        }

        private static string FormatStageUpdate(HistoricalEventStageEntry stage)
        {
            string date = FormatHistoricalYear(stage);
            string label = Translator.Translate(stage.displayLabelKey);
            string details = Translator.Translate(stage.notificationDescriptionKey ?? "HE_001_LishanCrisis_Description");
            return "HE001_LongTermQuest_UpdateFormat".Translate(date, label, details).ToString();
        }

        private static string FormatCurrentStatus(HistoricalEventStageEntry stage)
        {
            return "HE001_LongTermQuest_CurrentStatus".Translate(
                FormatHistoricalYear(stage), Translator.Translate(stage.displayLabelKey)).ToString();
        }

        private static string FormatHistoricalYear(HistoricalEventStageEntry stage)
        {
            int year;
            bool isBce;
            HistoricalYearConverter.FromAstronomicalYear(stage.scheduledDate.AstronomicalYear, out year, out isBce);
            return (isBce ? "HE001_LongTermQuest_BCEYear" : "HE001_LongTermQuest_CEYear").Translate(year).ToString();
        }

        private static string StageTag(string stageId) => "HR_HE001_STAGE_" + stageId;
    }
}
