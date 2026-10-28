using UnityEngine;
using Verse;

namespace HistoricalRimWorld
{
    public sealed class HistoricalRimWorldMod : Mod
    {
        private readonly HistoricalRimWorldSettings settings;

        public HistoricalRimWorldMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<HistoricalRimWorldSettings>();
            Log.Message("[Historical RimWorld] Mod initialized.");
        }

        public override string SettingsCategory()
        {
            return Translator.Translate("HR_SettingsCategory");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            settings.DoWindowContents(inRect);
        }
    }
}
