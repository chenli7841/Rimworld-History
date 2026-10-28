using UnityEngine;
using Verse;

namespace HistoricalRimWorld
{
    public sealed class HistoricalRimWorldSettings : ModSettings
    {
        public bool EnableDebugLogging = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref EnableDebugLogging, "EnableDebugLogging", false);
            base.ExposeData();
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(Translator.Translate("HR_EnableDebugLogging"), ref EnableDebugLogging);
            listing.End();
        }
    }
}
