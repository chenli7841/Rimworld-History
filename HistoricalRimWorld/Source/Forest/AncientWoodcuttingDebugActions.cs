using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using GameUI = Verse.UI;

namespace HistoricalRimWorld.Forest
{
    public static class AncientWoodcuttingDebugActions
    {
        [DebugAction("Historical RimWorld", "HR Trees: Inspect cut eligibility", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void InspectEligibility()
        {
            if (!Prefs.DevMode) return;
            Pawn pawn = Find.Selector?.SingleSelectedThing as Pawn;
            Map map = Find.CurrentMap;
            IntVec3 cell = GameUI.MouseCell();
            Plant tree = map != null && cell.InBounds(map)
                ? map.thingGrid.ThingsAt(cell).OfType<Plant>().FirstOrDefault(AncientWoodcuttingUtility.IsAncientTree)
                : null;
            if (pawn == null || tree == null)
            {
                Log.Message("[Historical RimWorld][HR-045] Select a pawn and point at an ancient tree to inspect cut eligibility.");
                return;
            }

            bool canCut = AncientWoodcuttingUtility.CanCutAncientTree(pawn, tree, out string reason);
            HR_WoodcuttingToolExtension tool = AncientWoodcuttingUtility.GetEquippedTool(pawn);
            Log.Message("[Historical RimWorld][HR-045] Tree=" + tree.def.defName + "; required=Bronze; pawn tool="
                + (pawn.equipment?.Primary?.def?.defName ?? "none") + "; tier=" + (tool?.toolTier.ToString() ?? "none")
                + "; speed=" + (tool?.ancientTreeCutSpeedFactor.ToString("0.##") ?? "0")
                + "; can cut=" + canCut + (canCut ? string.Empty : "; reason=" + reason));
        }

        [DebugAction("Historical RimWorld", "HR Trees: Set selected axe durability", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SetSelectedAxeDurability()
        {
            if (!Prefs.DevMode) return;
            Pawn pawn = Find.Selector?.SingleSelectedThing as Pawn;
            ThingWithComps tool = pawn?.equipment?.Primary;
            if (tool?.GetComp<CompAncientWoodcuttingTool>() == null)
            {
                Log.Message("[Historical RimWorld][HR-045] Select a pawn equipped with a bronze or iron axe first.");
                return;
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(new[]
            {
                new DebugMenuOption("100%", DebugMenuOptionMode.Action, () => SetDurability(tool, 1f)),
                new DebugMenuOption("50%", DebugMenuOptionMode.Action, () => SetDurability(tool, 0.5f)),
                new DebugMenuOption("10%", DebugMenuOptionMode.Action, () => SetDurability(tool, 0.1f)),
                new DebugMenuOption("1%", DebugMenuOptionMode.Action, () => SetDurability(tool, 0.01f))
            }));
        }

        private static void SetDurability(ThingWithComps tool, float fraction)
        {
            if (tool == null || tool.Destroyed) return;
            tool.HitPoints = UnityEngine.Mathf.Clamp(
                UnityEngine.Mathf.RoundToInt(tool.MaxHitPoints * fraction), 1, tool.MaxHitPoints);
            Log.Message("[Historical RimWorld][HR-045] Set " + tool.def.defName + " durability to "
                + tool.HitPoints + "/" + tool.MaxHitPoints + ".");
        }
    }
}
