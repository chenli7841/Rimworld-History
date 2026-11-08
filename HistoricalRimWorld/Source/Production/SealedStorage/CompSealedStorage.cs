using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Production.SealedStorage
{
    public sealed class CompProperties_SealedStorage : CompProperties
    {
        public float rotMultiplier = 0.5f;
        public float unroofedRotMultiplier = 0.75f;

        public CompProperties_SealedStorage() => compClass = typeof(CompSealedStorage);

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (rotMultiplier <= 0f || rotMultiplier > 1f) yield return "rotMultiplier must be greater than 0 and at most 1.";
            if (unroofedRotMultiplier <= 0f || unroofedRotMultiplier > 1f) yield return "unroofedRotMultiplier must be greater than 0 and at most 1.";
        }
    }

    public sealed class CompSealedStorage : ThingComp
    {
        public CompProperties_SealedStorage Props => (CompProperties_SealedStorage)props;

        public float RotMultiplierAt(IntVec3 cell)
        {
            Map map = parent?.Map;
            return map != null && cell.InBounds(map) && map.roofGrid.Roofed(cell)
                ? Props.rotMultiplier
                : Props.unroofedRotMultiplier;
        }

        public override string CompInspectStringExtra()
        {
            int stackLimit = parent?.def?.building?.maxItemsInCell ?? 0;
            List<IntVec3> cells = parent == null ? new List<IntVec3>() : GenAdj.OccupiedRect(parent).Cells.ToList();
            int slots = stackLimit * cells.Count;
            int used = 0;
            Map map = parent?.Map;
            if (map != null)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    List<Thing> things = map.thingGrid.ThingsListAtFast(cells[i]);
                    for (int j = 0; j < things.Count; j++)
                    {
                        Thing item = things[j];
                        if (item != parent && item.def.category != ThingCategory.Building
                            && parent.def.building.fixedStorageSettings.AllowedToAccept(item)) used++;
                    }
                }
            }
            string capacity = "HR_SealedStorage_Capacity".Translate(used, slots);
            float multiplier = RotMultiplierAt(parent?.Position ?? IntVec3.Invalid);
            string rate = "HR_SealedStorage_RotRate".Translate((multiplier * 100f).ToString("0"));
            return capacity + "\n" + rate + "\n" + "HR_SealedStorage_EligibleOnly".Translate();
        }
    }

    public static class SealedStorageUtility
    {
        public static float RotMultiplierFor(Thing thing)
        {
            if (thing?.def == null || thing.Map == null || !thing.Position.InBounds(thing.Map)) return 1f;
            SealedStorageFoodExtension food = thing.def.GetModExtension<SealedStorageFoodExtension>();
            if (food == null || !food.eligibleForSealedStorage) return 1f;

            List<Thing> thingsAtCell = thing.Map.thingGrid.ThingsListAtFast(thing.Position);
            for (int i = 0; i < thingsAtCell.Count; i++)
            {
                Building_Storage storage = thingsAtCell[i] as Building_Storage;
                CompSealedStorage sealedStorage = storage?.TryGetComp<CompSealedStorage>();
                if (sealedStorage == null) continue;
                if (food.rotMultiplierOverride > 0f && food.rotMultiplierOverride <= 1f) return food.rotMultiplierOverride;
                return sealedStorage.RotMultiplierAt(thing.Position);
            }
            return 1f;
        }
    }
}
