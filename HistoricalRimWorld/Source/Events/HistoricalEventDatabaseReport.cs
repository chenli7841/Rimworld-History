using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalEventDatabaseReport
    {
        public int DatedHistoryCount { get; private set; }
        public int ContextCount { get; private set; }
        public int LocalTemplateCount { get; private set; }
        public int DisabledTemplateCount { get; private set; }
        public int StorylineCount { get; private set; }
        public List<string> Warnings { get; } = new List<string>();

        public static HistoricalEventDatabaseReport Build(HistoricalScenarioDef scenario)
        {
            HistoricalEventDatabaseReport report = new HistoricalEventDatabaseReport();
            if (scenario == null) return report;
            report.DatedHistoryCount = DefDatabase<HistoricalEventDef>.AllDefsListForReading.Count(item =>
                item.scenario?.defName == scenario.defName && item.activationMode != HistoricalEventActivationMode.DevelopmentOnly
                && item.activationMode != HistoricalEventActivationMode.LocalTemplate);
            List<HistoricalContextDef> contexts = DefDatabase<HistoricalContextDef>.AllDefsListForReading
                .Where(item => item.scenario?.defName == scenario.defName).ToList();
            report.ContextCount = contexts.Count;
            foreach (HistoricalContextDef context in contexts)
                report.Warnings.AddRange(context.ConfigErrors().Select(error => context.defName + ": " + error));
            List<HistoricalLocalEventDef> locals = DefDatabase<HistoricalLocalEventDef>.AllDefsListForReading
                .Where(item => item.scenario?.defName == scenario.defName).ToList();
            report.LocalTemplateCount = locals.Count;
            report.DisabledTemplateCount = locals.Count(item => !item.enabled);
            report.Warnings.AddRange(DefDatabase<HistoricalStorylineDef>.AllDefsListForReading
                .Where(item => item.scenario?.defName == scenario.defName)
                .SelectMany(item => item.ConfigErrors().Select(error => item.defName + ": " + error)));
            report.StorylineCount = DefDatabase<HistoricalStorylineDef>.AllDefsListForReading
                .Count(item => item.scenario?.defName == scenario.defName);
            if (report.DatedHistoryCount + report.ContextCount != 15)
                report.Warnings.Add("Expected 15 historical/context records; found " + (report.DatedHistoryCount + report.ContextCount) + ".");
            if (report.LocalTemplateCount != 20)
                report.Warnings.Add("Expected 20 local templates; found " + report.LocalTemplateCount + ".");
            if (report.StorylineCount != 5)
                report.Warnings.Add("Expected 5 continuous storylines; found " + report.StorylineCount + ".");
            return report;
        }

        public string Summary => "HR-011G database: " + DatedHistoryCount + " dated historical events + " + ContextCount
            + " ongoing contexts, " + LocalTemplateCount + " local templates (" + DisabledTemplateCount + " disabled), "
            + StorylineCount + "/5 storylines.";
    }
}
