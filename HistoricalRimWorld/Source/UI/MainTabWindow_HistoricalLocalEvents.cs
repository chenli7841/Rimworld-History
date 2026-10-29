using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_HistoricalLocalEvents : MainTabWindow
    {
        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label(Translator.Translate("HR_LocalEvents_Title"));
            if (!HistoricalScenarioManager.IsScenarioActive || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate))
            {
                listing.Label(Translator.Translate("HR_LocalEvents_Inactive"));
                listing.End();
                return;
            }

            HistoricalLocalEventDef pending = HistoricalEventManager.PendingLocalEvent;
            if (pending == null)
            {
                listing.Label(Translator.Translate("HR_LocalEvents_None"));
                HistoricalYearConverter.FromAstronomicalYear(currentDate.AstronomicalYear, out int year, out bool bce);
                listing.Label(string.Format(Translator.Translate("HR_LocalEvents_Next"), year, HistoricalEventManager.NextLocalEventAbsoluteDay - currentDate.AbsoluteDay));
            }
            else
            {
                listing.Label(pending.LabelCap);
                listing.Label(Translator.Translate(pending.description));
                listing.Label(Translator.Translate(pending.triggerNotes));
                if (!string.IsNullOrWhiteSpace(HistoricalEventManager.PendingStorylineId))
                    listing.Label(string.Format(Translator.Translate("HR_LocalEvents_StoryStage"),
                        Translator.Translate("HR_Storyline_" + HistoricalEventManager.PendingStorylineId)));
                listing.Gap();
                for (int i = 0; i < pending.choices.Count; i++)
                {
                    HistoricalLocalEventChoice choice = pending.choices[i];
                    bool available = HistoricalEventManager.CanResolveLocalEventChoice(i, out string reason);
                    if (listing.ButtonText(Translator.Translate(choice.label)))
                    {
                        if (!HistoricalEventManager.TryResolveLocalEventChoice(i, out string error))
                            Messages.Message(error, MessageTypeDefOf.RejectInput, false);
                        break;
                    }
                    listing.Label(Translator.Translate(choice.description) + " — " + DescribeOutcome(choice));
                    if (!available) listing.Label(Translator.Translate("HR_LocalChoice_Unavailable") + " " + reason);
                    listing.GapLine();
                }
            }

            listing.GapLine();
            listing.Label(Translator.Translate("HR_LocalEvents_Stories"));
            foreach (HistoricalStorylineRuntimeState story in HistoricalEventManager.StorylineStates.OrderBy(item => item.storylineId))
                listing.Label(string.Format(Translator.Translate(story.ended ? "HR_LocalEvents_StoryEnded" : "HR_LocalEvents_StoryProgress"),
                    story.storylineId, story.currentStage, story.outcome ?? string.Empty));
            listing.End();
        }

        private static string DescribeOutcome(HistoricalLocalEventChoice choice)
        {
            string key;
            string value = string.Empty;
            switch (choice.outcome)
            {
                case HistoricalLocalEventOutcome.GainResource:
                    key = "HR_LocalOutcome_Gain";
                    value = choice.amount + " × " + choice.resource.LabelCap;
                    break;
                case HistoricalLocalEventOutcome.SpendResource:
                    key = "HR_LocalOutcome_Spend";
                    value = choice.amount + " × " + choice.resource.LabelCap;
                    break;
                case HistoricalLocalEventOutcome.TradeResource:
                    key = "HR_LocalOutcome_Gain";
                    value = choice.amount + " × " + choice.resource.LabelCap;
                    break;
                case HistoricalLocalEventOutcome.AddColonist: key = "HR_LocalOutcome_Colonist"; break;
                case HistoricalLocalEventOutcome.ChangeCommunityPosture:
                    key = "HR_LocalOutcome_Posture";
                    value = Translator.Translate("HR_Politics_Posture_" + choice.posture);
                    break;
                case HistoricalLocalEventOutcome.RevealRegion: key = "HR_LocalOutcome_Reveal"; break;
                case HistoricalLocalEventOutcome.IncreaseCommunityCulturePresence:
                    key = "HR_LocalOutcome_Culture";
                    value = DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(choice.cultureId)?.LabelCap.ToString() ?? choice.cultureId;
                    break;
                case HistoricalLocalEventOutcome.CreateRelic: key = "HR_LocalOutcome_CreateRelic"; break;
                case HistoricalLocalEventOutcome.InheritRelic: key = "HR_LocalOutcome_InheritRelic"; break;
                case HistoricalLocalEventOutcome.AdoptTechnology:
                    key = "HR_LocalOutcome_Technology";
                    value = DefDatabase<HistoricalTechnologyDef>.GetNamedSilentFail(choice.technologyId)?.LabelCap.ToString() ?? choice.technologyId;
                    break;
                case HistoricalLocalEventOutcome.RevealHistoricalEvent: key = "HR_LocalOutcome_RevealHistory"; break;
                case HistoricalLocalEventOutcome.CreateMarriage: key = "HR_LocalOutcome_Marriage"; break;
                default: key = "HR_LocalOutcome_Chronicle"; break;
            }
            return string.Format(Translator.Translate(key), value);
        }
    }
}
