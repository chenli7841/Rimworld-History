using System.Collections.Generic;
using System.Linq;
using RimWorld;
using HistoricalRimWorld.Production.SealedStorage;
using Verse;

namespace HistoricalRimWorld.Production.Granary
{
    public sealed class CompProperties_Granary : CompProperties
    {
        public CompProperties_Granary() => compClass = typeof(CompGranary);
    }

    /// <summary>Inspector information only; storage, filters, hauling and item ownership remain vanilla.</summary>
    public sealed class CompGranary : ThingComp
    {
        public override string CompInspectStringExtra()
        {
            Building_Storage storage = parent as Building_Storage;
            if (storage == null || parent.Map == null || parent.def.building == null) return null;

            CellRect cells = GenAdj.OccupiedRect(parent);
            int stackSlots = cells.Cells.Count() * parent.def.building.maxItemsInCell;
            int usedSlots = 0;
            Dictionary<ThingDef, int> stock = new Dictionary<ThingDef, int>();
            StorageSettings fixedSettings = parent.def.building.fixedStorageSettings;

            foreach (IntVec3 cell in cells)
            {
                List<Thing> things = parent.Map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing item = things[i];
                    if (item == parent || item.def.category == ThingCategory.Building
                        || fixedSettings == null || !fixedSettings.AllowedToAccept(item)) continue;

                    usedSlots++;
                    if (stock.TryGetValue(item.def, out int count)) stock[item.def] = count + item.stackCount;
                    else stock[item.def] = item.stackCount;
                }
            }

            string detail = stock.Count == 0
                ? "HR_Granary_Empty".Translate().ToString()
                : string.Join("\n", stock.OrderBy(pair => pair.Key.label)
                    .Select(pair => "  " + pair.Key.LabelCap + ": " + pair.Value));

            return "HR_Granary_Capacity".Translate(usedSlots, stackSlots)
                + "\n" + "HR_Granary_StoredGrain".Translate() + "\n" + detail
                + "\n" + "HR_Granary_AnnualLoss".Translate((GranaryStorageUtility.AnnualLossRateForGranary * 100f).ToString("0.0"));
        }
    }

    public static class GranaryStorageUtility
    {
        public static float AnnualLossRateForGranary => GranarySettingsDef.Settings?.granaryAnnualLossRate ?? 0.015f;

        public static bool IsEligibleGrain(Thing thing)
        {
            if (thing?.def == null) return false;
            GranarySettingsDef settings = GranarySettingsDef.Settings;
            return settings?.eligibleGrains != null && settings.eligibleGrains.Contains(thing.def);
        }

        public static float AnnualLossRateFor(Thing thing)
        {
            Map map = thing?.Map;
            if (map == null || !thing.Position.InBounds(map)) return 0f;

            List<Thing> atCell = map.thingGrid.ThingsListAtFast(thing.Position);
            for (int i = 0; i < atCell.Count; i++)
            {
                Thing building = atCell[i];
                if (building.TryGetComp<CompGranary>() != null)
                    return AnnualLossRateForGranary;
                if (building.TryGetComp<CompSealedStorage>() != null)
                    return GranarySettingsDef.Settings?.sealedJarAnnualLossRate ?? 0.02f;
            }

            return map.roofGrid.Roofed(thing.Position)
                ? GranarySettingsDef.Settings?.indoorAnnualLossRate ?? 0.06f
                : GranarySettingsDef.Settings?.outdoorAnnualLossRate ?? 0.12f;
        }
    }
}
