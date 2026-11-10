using System;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.Forest
{
    public static class AncientWoodcuttingUtility
    {
        public static HR_AncientTreeExtension TreeExtension(Plant plant)
        {
            return plant?.def?.GetModExtension<HR_AncientTreeExtension>();
        }

        public static bool IsAncientTree(Plant plant) => TreeExtension(plant) != null;

        public static bool CanCutAncientTree(Pawn pawn, Plant tree, out string reason)
        {
            reason = null;
            if (!IsAncientTree(tree) || !HistoricalScenarioManager.IsScenarioActive) return true;
            if (pawn == null || !pawn.Spawned || tree.Destroyed || !tree.Spawned || pawn.Map != tree.Map)
            {
                reason = "HR_AncientTreeInvalidTarget".Translate().ToString();
                return false;
            }
            if (pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) != true)
            {
                reason = "WorkCannotDoBecauseOfIncapable".Translate().ToString();
                return false;
            }
            HR_WoodcuttingToolExtension toolExtension = pawn.equipment?.Primary?.def?.GetModExtension<HR_WoodcuttingToolExtension>();
            if (toolExtension == null || toolExtension.toolTier < AncientWoodcuttingToolTier.Bronze
                || pawn.equipment.Primary.HitPoints <= 0
                || pawn.equipment.Primary.GetComp<CompAncientWoodcuttingTool>() == null)
            {
                reason = "HR_AncientTreeNeedsAxe".Translate().ToString();
                return false;
            }
            return true;
        }

        public static HR_WoodcuttingToolExtension GetEquippedTool(Pawn pawn)
        {
            return pawn?.equipment?.Primary?.def?.GetModExtension<HR_WoodcuttingToolExtension>();
        }

        public static float TreeWorkAmount(Plant tree)
        {
            HR_AncientTreeExtension extension = TreeExtension(tree);
            return Math.Max(1f, tree?.def?.plant?.harvestWork ?? 1000f)
                * Math.Max(1f, extension?.cutWorkMultiplier ?? 1f);
        }

        public static int AdjustWoodYield(Plant tree, int vanillaYield)
        {
            HR_AncientTreeExtension extension = TreeExtension(tree);
            if (extension == null || tree?.def?.plant == null) return vanillaYield;

            // Normalize the ancient-tree Def's authored yield first, so the
            // configured multiplier remains relative to an ordinary mature tree.
            float authoredYield = Math.Max(1f, tree.def.plant.harvestYield);
            float normalizedFactor = Math.Max(0f, extension.ordinaryTreeHarvestYield)
                * Math.Max(0f, extension.woodYieldMultiplier) / authoredYield;
            return Math.Max(0, (int)Math.Round(vanillaYield * normalizedFactor));
        }

        public static void ResolveRareDrops(Plant tree, Pawn cutter, Map map, IntVec3 cell)
        {
            HR_AncientTreeExtension extension = TreeExtension(tree);
            if (extension?.rareResourceDrops == null || extension.rareResourceDrops.Count == 0 || map == null) return;

            float maturityFactor = tree.Growth < 0.30f ? 0f : tree.Growth < 0.80f ? 0.25f : 1f;
            if (maturityFactor <= 0f) return;

            int plantsSkill = cutter?.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0;
            float quantityFactor = plantsSkill <= 5 ? 0.8f : plantsSkill <= 12 ? 1f : 1.15f;
            int producedKinds = 0;
            foreach (AncientTreeRareDrop drop in extension.rareResourceDrops.Where(item => item?.resourceDef != null))
            {
                if (producedKinds >= 3) break;
                if (drop.chance <= 0f || !Rand.Chance(Mathf.Clamp01(drop.chance * maturityFactor))) continue;

                int count = Math.Max(1, Mathf.RoundToInt(drop.countRange.RandomInRange * quantityFactor));
                while (count > 0)
                {
                    Thing thing = ThingMaker.MakeThing(drop.resourceDef);
                    thing.stackCount = Math.Min(count, thing.def.stackLimit);
                    int placedCount = thing.stackCount;
                    if (!GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near, out _))
                    {
                        thing.Destroy(DestroyMode.Vanish);
                        break;
                    }
                    count -= placedCount;
                }
                producedKinds++;
            }
        }
    }
}
