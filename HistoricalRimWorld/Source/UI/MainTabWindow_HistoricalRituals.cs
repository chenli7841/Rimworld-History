using System.Linq;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Rituals;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_HistoricalRituals : MainTabWindow
    {
        private Vector2 scroll;
        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), Translator.Translate("HR_Ritual_Title"));
            if (!HistoricalRitualManager.IsActive) { Widgets.Label(new Rect(rect.x, rect.y + 35f, rect.width, 30f), Translator.Translate("HR_Culture_Inactive")); return; }
            Pawn host = Find.Selector?.SingleSelectedThing as Pawn;
            string hostPerson = host != null && HistoricalGenealogyManager.TryGetPersonId(host, out string id) ? id : null;
            float y = rect.y + 36f;
            string hostLabel = hostPerson == null ? (string)Translator.Translate("HR_Ritual_SelectHost") : string.Format(Translator.Translate("HR_Ritual_Host"), host.LabelShort);
            Widgets.Label(new Rect(rect.x, y, rect.width, 24f), hostLabel); y += 26f;
            foreach (HistoricalRitualDef def in DefDatabase<HistoricalRitualDef>.AllDefsListForReading.OrderBy(item => item.label))
            {
                if (Widgets.ButtonText(new Rect(rect.x, y, Mathf.Min(rect.width * .42f, 250f), 30f), Translator.Translate(def.label)))
                {
                    if (HistoricalRitualManager.TryPlanRitual(def.defName, host, out _, out string error)) Messages.Message(Translator.Translate("HR_Ritual_Planned"), MessageTypeDefOf.PositiveEvent);
                    else Messages.Message(error, MessageTypeDefOf.RejectInput);
                }
                y += 34f;
            }
            y += 8f;
            Rect listRect = new Rect(rect.x, y, rect.width, rect.height - (y - rect.y));
            var items = HistoricalRitualManager.Instances.OrderByDescending(item => item.StartGameTick).ToList();
            Rect view = new Rect(0f, 0f, listRect.width - 18f, Mathf.Max(listRect.height, items.Count * 58f));
            Widgets.BeginScrollView(listRect, ref scroll, view);
            float row = 0f;
            foreach (HistoricalRitualInstance item in items)
            {
                string ritualLabel = DefDatabase<HistoricalRitualDef>.GetNamedSilentFail(item.RitualDefName)?.label ?? item.RitualDefName;
                string label = Translator.Translate(ritualLabel) + " — " + Translator.Translate("HR_Ritual_Status_" + item.Status);
                Widgets.Label(new Rect(0f, row, view.width * .63f, 28f), label);
                if (item.Status == HistoricalRitualStatus.Planned || item.Status == HistoricalRitualStatus.InProgress)
                {
                    if (Widgets.ButtonText(new Rect(view.width * .64f, row, 100f, 26f), Translator.Translate("HR_Ritual_Complete")))
                    {
                        if (HistoricalRitualManager.TryCompleteRitual(item.RitualInstanceId, out string error)) Messages.Message(Translator.Translate("HR_Ritual_Completed"), MessageTypeDefOf.PositiveEvent);
                        else Messages.Message(error, MessageTypeDefOf.RejectInput);
                    }
                    if (Widgets.ButtonText(new Rect(view.width * .64f + 105f, row, 90f, 26f), Translator.Translate("HR_Ritual_Cancel"))) HistoricalRitualManager.TryCancelRitual(item.RitualInstanceId, out _);
                }
                row += 56f;
            }
            Widgets.EndScrollView();
        }
    }
}
