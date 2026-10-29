using System.Linq;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Relics;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_HistoricalRelics : MainTabWindow
    {
        private Vector2 scroll;
        private string selectedRelicId;
        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), Translator.Translate("HR_Relic_Title"));
            if (!HistoricalRelicManager.IsActive) { Widgets.Label(new Rect(rect.x, rect.y + 34f, rect.width, 28f), Translator.Translate("HR_Culture_Inactive")); return; }
            Thing selectedThing = Find.Selector?.SingleSelectedThing;
            Pawn selectedPawn = selectedThing as Pawn;
            string personId = selectedPawn != null && HistoricalGenealogyManager.TryGetPersonId(selectedPawn, out string id) ? id : null;
            float y = rect.y + 36f;
            string selectedLabel = selectedThing == null ? (string)Translator.Translate("HR_Relic_SelectItem") : string.Format(Translator.Translate("HR_Relic_Selected"), selectedThing.LabelCap);
            Widgets.Label(new Rect(rect.x, y, rect.width, 28f), selectedLabel); y += 30f;
            if (selectedThing is Thing thing)
            {
                CompHistoricalRelic comp = thing.TryGetComp<CompHistoricalRelic>();
                if (comp?.IsLinked == true)
                {
                    if (Widgets.ButtonText(new Rect(rect.x, y, 190f, 28f), Translator.Translate("HR_Relic_MarkLost"))) HistoricalRelicManager.TryMarkLost(comp.RelicId, out _);
                    if (personId != null && Widgets.ButtonText(new Rect(rect.x + 198f, y, 190f, 28f), Translator.Translate("HR_Relic_Transfer")))
                    {
                        if (HistoricalRelicManager.TryTransfer(comp.RelicId, personId, HistoricalRelicEventType.Gifted, out string error)) Messages.Message(Translator.Translate("HR_Relic_Transferred"), RimWorld.MessageTypeDefOf.PositiveEvent);
                        else Messages.Message(error, RimWorld.MessageTypeDefOf.RejectInput);
                    }
                }
                else if (comp != null)
                {
                    HistoricalRelicDef def = DefDatabase<HistoricalRelicDef>.AllDefsListForReading.FirstOrDefault(item => item.baseThingDef == thing.def);
                    if (def != null && Widgets.ButtonText(new Rect(rect.x, y, 250f, 28f), Translator.Translate("HR_Relic_Register")))
                    {
                        if (HistoricalRelicManager.TryDesignateAsRelic(thing, def.defName, personId, personId, out _, out string error)) Messages.Message(Translator.Translate("HR_Relic_Registered"), RimWorld.MessageTypeDefOf.PositiveEvent);
                        else Messages.Message(error, RimWorld.MessageTypeDefOf.RejectInput);
                    }
                }
            }
            y += 34f;
            Rect listRect = new Rect(rect.x, y, rect.width * .48f, rect.height - (y - rect.y));
            Rect detailRect = new Rect(listRect.xMax + 10f, listRect.y, rect.xMax - listRect.xMax - 10f, listRect.height);
            var records = HistoricalRelicManager.Records.OrderBy(item => item.DisplayName).ToList();
            Rect view = new Rect(0f, 0f, listRect.width - 18f, Mathf.Max(listRect.height, records.Count * 48f));
            Widgets.BeginScrollView(listRect, ref scroll, view);
            float row = 0f;
            foreach (HistoricalRelicRecord record in records)
            {
                string state = record.IsDestroyed ? Translator.Translate("HR_Relic_Destroyed") : record.IsLost ? Translator.Translate("HR_Relic_Lost") : Translator.Translate("HR_Relic_Present");
                if (Widgets.ButtonText(new Rect(0f, row, view.width, 44f), (record.DisplayName ?? record.RelicId) + "\n" + state)) selectedRelicId = record.RelicId;
                row += 48f;
            }
            Widgets.EndScrollView();
            Listing_Standard listing = new Listing_Standard(); listing.Begin(detailRect);
            listing.Label(Translator.Translate("HR_Relic_ArchiveNote"));
            HistoricalRelicRecord selectedRecord = HistoricalRelicManager.Find(selectedRelicId);
            if (selectedRecord != null)
            {
                listing.Label((selectedRecord.DisplayName ?? selectedRecord.RelicId) + " — " + selectedRecord.OriginCultureId + " / " + selectedRecord.CurrentOwnerPersonId);
                if (!selectedRecord.IsDestroyed)
                {
                    if (selectedRecord.IsLost)
                    {
                        if (listing.ButtonText(Translator.Translate("HR_Relic_Recover")))
                        {
                            if (HistoricalRelicManager.TryRecover(selectedRecord.RelicId, personId, out string error)) Messages.Message(Translator.Translate("HR_Relic_Recovered"), RimWorld.MessageTypeDefOf.PositiveEvent);
                            else Messages.Message(error, RimWorld.MessageTypeDefOf.RejectInput);
                        }
                    }
                    else
                    {
                        if (listing.ButtonText(Translator.Translate("HR_Relic_MarkLost"))) HistoricalRelicManager.TryMarkLost(selectedRecord.RelicId, out _);
                        if (personId != null)
                        {
                            RecordTransfer(listing, selectedRecord, personId, HistoricalRelicEventType.Acquired, "HR_Relic_RecordAcquired");
                            RecordTransfer(listing, selectedRecord, personId, HistoricalRelicEventType.Gifted, "HR_Relic_RecordGift");
                            RecordTransfer(listing, selectedRecord, personId, HistoricalRelicEventType.Inherited, "HR_Relic_RecordInheritance");
                            RecordTransfer(listing, selectedRecord, personId, HistoricalRelicEventType.Traded, "HR_Relic_RecordTrade");
                            RecordTransfer(listing, selectedRecord, personId, HistoricalRelicEventType.Stolen, "HR_Relic_RecordTheft");
                        }
                    }
                }
                foreach (HistoricalRelicEventRecord evt in HistoricalRelicManager.Events.Where(item => item.RelicId == selectedRecord.RelicId).OrderBy(item => item.GameTick))
                    listing.Label("• " + Translator.Translate("HR_Relic_Event_" + evt.EventType) + " — " + evt.AstronomicalYear);
            }
            listing.End();
        }

        private static void RecordTransfer(Listing_Standard listing, HistoricalRelicRecord record, string personId, HistoricalRelicEventType type, string labelKey)
        {
            if (!listing.ButtonText(Translator.Translate(labelKey))) return;
            if (HistoricalRelicManager.TryTransfer(record.RelicId, personId, type, out string error))
                Messages.Message(Translator.Translate("HR_Relic_Transferred"), RimWorld.MessageTypeDefOf.PositiveEvent);
            else Messages.Message(error, RimWorld.MessageTypeDefOf.RejectInput);
        }

    }
}
