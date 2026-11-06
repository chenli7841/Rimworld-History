using UnityEngine;
using Verse;
using HistoricalRimWorld.Patches.World;

namespace HistoricalRimWorld
{
    public sealed class HistoricalRimWorldMod : Mod
    {
        private readonly HistoricalRimWorldSettings settings;
        public static HistoricalRimWorldSettings CurrentSettings { get; private set; }

        public HistoricalRimWorldMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<HistoricalRimWorldSettings>();
            CurrentSettings = settings;
            HistoricalWorldPatchBootstrap.Apply();
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

        public override void WriteSettings()
        {
            base.WriteSettings();
            HistoricalLocalFogSettings.NotifyChanged();
        }
    }
}
