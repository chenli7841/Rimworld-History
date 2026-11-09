using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HistoricalRimWorld.FogOfWar;
using RimWorld;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Patches.FogOfWar
{
    [HarmonyPatch]
    internal static class HistoricalFogPawnDrawPatch
    {
        private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(Pawn), "DrawAt", new[] { typeof(UnityEngine.Vector3), typeof(bool) });
        private static bool Prefix(Pawn __instance) => !HistoricalLocalFogOfWarManager.ShouldHide(__instance);
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt), new[] { typeof(DrawPhase), typeof(UnityEngine.Vector3), typeof(bool) })]
    internal static class HistoricalFogPawnDynamicDrawPatch
    {
        // RimWorld 1.6's DynamicDrawManager renders map pawns through this phase
        // entry point. Suppress the complete draw phase for unseen pawns so an
        // animal cannot move visibly beneath the explored-memory fog overlay.
        private static bool Prefix(Pawn __instance) => !HistoricalLocalFogOfWarManager.ShouldHide(__instance);
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawGUIOverlay))]
    internal static class HistoricalFogPawnGuiOverlayPatch
    {
        private static bool Prefix(Pawn __instance) => !HistoricalLocalFogOfWarManager.ShouldHide(__instance);
    }

    [HarmonyPatch(typeof(SilhouetteUtility), nameof(SilhouetteUtility.ShouldDrawSilhouette), new[] { typeof(Thing) })]
    internal static class HistoricalFogPawnSilhouettePatch
    {
        private static void Postfix(Thing __0, ref bool __result)
        {
            if (__result && __0 is Pawn && HistoricalLocalFogOfWarManager.ShouldHide(__0))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawExtraSelectionOverlays))]
    internal static class HistoricalFogPawnSelectionOverlayPatch
    {
        private static bool Prefix(Pawn __instance) => !HistoricalLocalFogOfWarManager.ShouldHide(__instance);
    }

    [HarmonyPatch]
    internal static class HistoricalFogSelectableObjectsAtPatch
    {
        private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(Selector), "SelectableObjectsAt", new[] { typeof(IntVec3), typeof(Map) });
        private static void Postfix(ref IEnumerable<object> __result)
        {
            if (!HistoricalLocalFogOfWarManager.IsEnabled || __result == null) return;
            __result = __result.Where(obj => !(obj is Thing thing && HistoricalLocalFogOfWarManager.ShouldHide(thing))).ToList();
        }
    }

    [HarmonyPatch]
    internal static class HistoricalFogSelectableObjectsUnderMousePatch
    {
        private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(Selector), "SelectableObjectsUnderMouse");
        private static void Postfix(ref IEnumerable<object> __result)
        {
            if (!HistoricalLocalFogOfWarManager.IsEnabled || __result == null) return;
            __result = __result.Where(obj => !(obj is Thing thing && HistoricalLocalFogOfWarManager.ShouldHide(thing))).ToList();
        }
    }

    [HarmonyPatch(typeof(Selector), nameof(Selector.Select), new[] { typeof(object), typeof(bool), typeof(bool) })]
    internal static class HistoricalFogDirectSelectionPatch
    {
        private static bool Prefix(object __0) => !(__0 is Thing thing && HistoricalLocalFogOfWarManager.ShouldHide(thing));
    }

    [HarmonyPatch(typeof(Building), nameof(Building.SpawnSetup), new[] { typeof(Map), typeof(bool) })]
    internal static class HistoricalFogBuildingSpawnPatch
    {
        private static void Postfix(Building __instance) =>
            HistoricalLocalFogOfWarManager.GetComponent(__instance?.Map)?.MarkVisibilityDirty();
    }

    [HarmonyPatch(typeof(Building), nameof(Building.DeSpawn), new[] { typeof(DestroyMode) })]
    internal static class HistoricalFogBuildingDespawnPatch
    {
        private static void Prefix(Building __instance) =>
            HistoricalLocalFogOfWarManager.GetComponent(__instance?.Map)?.MarkVisibilityDirty();
    }

    [HarmonyPatch(typeof(Room), nameof(Room.Notify_RoomShapeChanged))]
    internal static class HistoricalFogRoomShapeChangedPatch
    {
        private static void Postfix(Room __instance) =>
            HistoricalLocalFogOfWarManager.GetComponent(__instance?.Map)?.MarkVisibilityDirty();
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.CanHitTargetFrom), new[] { typeof(IntVec3), typeof(LocalTargetInfo) })]
    internal static class HistoricalFogVerbTargetPatch
    {
        private static bool Prefix(Verb __instance, LocalTargetInfo __1, ref bool __result)
        {
            Pawn caster = __instance.CasterPawn;
            if (caster?.Faction == Faction.OfPlayer && __1.HasThing
                && HistoricalLocalFogOfWarManager.ShouldHide(__1.Thing))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget), new[] {
        typeof(IAttackTargetSearcher), typeof(TargetScanFlags), typeof(System.Predicate<Thing>),
        typeof(float), typeof(float), typeof(IntVec3), typeof(float), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    internal static class HistoricalFogBestAttackTargetPatch
    {
        private static void Prefix(IAttackTargetSearcher searcher, ref System.Predicate<Thing> validator)
        {
            if (searcher?.Thing?.Faction != Faction.OfPlayer) return;
            System.Predicate<Thing> original = validator;
            validator = thing => (original == null || original(thing)) && !HistoricalLocalFogOfWarManager.ShouldHide(thing);
        }
    }
}
