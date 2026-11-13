using HistoricalRimWorld.Characters;
using HarmonyLib;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HistoricalRimWorld.Patches.Characters
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TryStartAttack), new[] { typeof(LocalTargetInfo) })]
    internal static class HistoricalPawnAttackProtectionPatch
    {
        private static bool Prefix(Pawn __instance, LocalTargetInfo targ, ref bool __result)
        {
            if (__instance?.Faction != Faction.OfPlayer || !targ.HasThing || !(targ.Thing is Pawn target)
                || HistoricalCharacterProtectionService.CanPlayerTarget(target)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("player attack", target);
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedAttack), "GetOptionsFor", new[] { typeof(Thing), typeof(FloatMenuContext) })]
    internal static class HistoricalDraftedAttackMenuProtectionPatch
    {
        private static bool Prefix(Thing t, ref IEnumerable<FloatMenuOption> __result)
        {
            if (!(t is Pawn target) || HistoricalCharacterProtectionService.CanPlayerTarget(target)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("player attack menu", target);
            __result = System.Array.Empty<FloatMenuOption>();
            return false;
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.CanHitTargetFrom), new[] { typeof(IntVec3), typeof(LocalTargetInfo) })]
    internal static class HistoricalVerbTargetProtectionPatch
    {
        private static bool Prefix(Verb __instance, LocalTargetInfo targ, ref bool __result)
        {
            if (__instance?.Caster?.Faction != Faction.OfPlayer || !targ.HasThing || !(targ.Thing is Pawn target)
                || HistoricalCharacterProtectionService.CanPlayerTarget(target)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("player ranged target", target);
            __result = false;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class HistoricalVerbCastProtectionPatch
    {
        private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(Verb), nameof(Verb.TryStartCastOn),
            new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });

        private static bool Prefix(Verb __instance, object[] __args)
        {
            if (__instance?.Caster?.Faction != Faction.OfPlayer || __args == null || __args.Length == 0
                || !(__args[0] is LocalTargetInfo targetInfo) || !targetInfo.HasThing || !(targetInfo.Thing is Pawn target)
                || HistoricalCharacterProtectionService.CanPlayerTarget(target)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("player ranged attack", target);
            return false;
        }
    }

    [HarmonyPatch]
    internal static class HistoricalVerbSingleTargetCastProtectionPatch
    {
        private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(Verb), nameof(Verb.TryStartCastOn),
            new[] { typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });

        private static bool Prefix(Verb __instance, object[] __args)
        {
            if (__instance?.Caster?.Faction != Faction.OfPlayer || __args == null || __args.Length == 0
                || !(__args[0] is LocalTargetInfo targetInfo) || !targetInfo.HasThing || !(targetInfo.Thing is Pawn target)
                || HistoricalCharacterProtectionService.CanPlayerTarget(target)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("player ranged cast", target);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    internal static class HistoricalPawnDamageProtectionPatch
    {
        private static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (HistoricalCharacterProtectionService.CanReceiveOrdinaryDamage(__instance)) return true;
            absorbed = true;
            HistoricalCharacterProtectionService.ReportBlocked("pre-fate damage", __instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "ShouldBeDeadFromLethalDamageThreshold")]
    internal static class HistoricalPawnLethalThresholdPatch
    {
        private static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (HistoricalCharacterProtectionService.IsFateProtected(___pawn)) __result = false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill), new[] { typeof(DamageInfo?), typeof(Hediff) })]
    internal static class HistoricalPawnDeathProtectionPatch
    {
        private static bool Prefix(Pawn __instance)
        {
            if (HistoricalCharacterProtectionService.CanDieNaturally(__instance)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("unauthorized death", __instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Destroy), new[] { typeof(DestroyMode) })]
    internal static class HistoricalPawnDestroyProtectionPatch
    {
        private static bool Prefix(Pawn __instance)
        {
            if (__instance == null || __instance.Dead || HistoricalCharacterProtectionService.CanRelocate(__instance)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("Pawn destruction or removal", __instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.CheckAcceptArrest), new[] { typeof(Pawn) })]
    internal static class HistoricalArrestAcceptancePatch
    {
        private static void Postfix(Pawn __instance, ref bool __result)
        {
            if (HistoricalCharacterProtectionService.CanCapture(__instance)) return;
            __result = false;
            HistoricalCharacterProtectionService.ReportBlocked("arrest", __instance);
        }
    }

    [HarmonyPatch]
    internal static class HistoricalCanArrestPatch
    {
        private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(CaptureUtility), nameof(CaptureUtility.CanArrest),
            new[] { typeof(Pawn), typeof(Pawn), typeof(string).MakeByRefType() });

        private static void Postfix(Pawn victim, ref bool __result, ref string reason)
        {
            if (HistoricalCharacterProtectionService.CanCapture(victim)) return;
            __result = false;
            reason = "HR_HistoricalCharacter_Protected".Translate();
            HistoricalCharacterProtectionService.ReportBlocked("capture order", victim);
        }
    }

    [HarmonyPatch(typeof(CaptureUtility), nameof(CaptureUtility.OrderArrest), new[] { typeof(Pawn), typeof(Pawn) })]
    internal static class HistoricalOrderArrestPatch
    {
        private static bool Prefix(Pawn victim)
        {
            if (HistoricalCharacterProtectionService.CanCapture(victim)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("arrest order", victim);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_GuestTracker), "get_Recruitable")]
    internal static class HistoricalRecruitablePatch
    {
        private static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (HistoricalCharacterProtectionService.CanRecruit(___pawn)) return;
            __result = false;
            HistoricalCharacterProtectionService.ReportBlocked("recruitment", ___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
    internal static class HistoricalGuestStatusPatch
    {
        private static bool Prefix(Pawn ___pawn)
        {
            if (HistoricalCharacterProtectionService.CanCapture(___pawn)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("prisoner or slave status change", ___pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.CapturedBy))]
    internal static class HistoricalCapturedByPatch
    {
        private static bool Prefix(Pawn ___pawn)
        {
            if (HistoricalCharacterProtectionService.CanCapture(___pawn)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("capture", ___pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.Notify_PawnRecruited))]
    internal static class HistoricalRecruitedPatch
    {
        private static bool Prefix(Pawn ___pawn)
        {
            if (HistoricalCharacterProtectionService.CanRecruit(___pawn)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("recruit", ___pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction), new[] { typeof(Faction), typeof(Pawn) })]
    internal static class HistoricalPawnFactionChangePatch
    {
        private static bool Prefix(Pawn __instance, Faction newFaction)
        {
            if (__instance == null || __instance.Dead || __instance.Faction == newFaction
                || HistoricalCharacterProtectionService.CanChangeFaction(__instance)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("faction change", __instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.SetFactionDirect), new[] { typeof(Faction) })]
    internal static class HistoricalPawnDirectFactionChangePatch
    {
        private static bool Prefix(Thing __instance, Faction newFaction)
        {
            if (!(__instance is Pawn pawn) || pawn.Dead || pawn.Faction == newFaction
                || HistoricalCharacterProtectionService.CanChangeFaction(pawn)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("direct faction change", pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap), new[] { typeof(bool), typeof(Rot4) })]
    internal static class HistoricalPawnExitMapPatch
    {
        private static bool Prefix(Pawn __instance)
        {
            if (HistoricalCharacterProtectionService.CanRelocate(__instance)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("map exit or forced relocation", __instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(WorldPawns), nameof(WorldPawns.PassToWorld), new[] { typeof(Pawn), typeof(PawnDiscardDecideMode) })]
    internal static class HistoricalPawnWorldTransferPatch
    {
        private static bool Prefix(Pawn pawn)
        {
            if (HistoricalCharacterProtectionService.CanRelocate(pawn)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("transfer to world pawns", pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldPawns), "AddPawn", new[] { typeof(Pawn) })]
    internal static class HistoricalWorldPawnAddPatch
    {
        private static bool Prefix(Pawn p)
        {
            if (HistoricalCharacterProtectionService.CanRelocate(p)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("world pawn transfer", p);
            return false;
        }
    }

    [HarmonyPatch(typeof(Caravan), nameof(Caravan.AddPawn), new[] { typeof(Pawn), typeof(bool) })]
    internal static class HistoricalCaravanPawnTransferPatch
    {
        private static bool Prefix(Pawn p)
        {
            if (HistoricalCharacterProtectionService.CanRelocate(p)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("caravan transfer", p);
            return false;
        }
    }

    [HarmonyPatch(typeof(Caravan), nameof(Caravan.AddPawnOrItem), new[] { typeof(Thing), typeof(bool) })]
    internal static class HistoricalCaravanThingTransferPatch
    {
        private static bool Prefix(Thing thing)
        {
            if (!(thing is Pawn pawn) || HistoricalCharacterProtectionService.CanRelocate(pawn)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("caravan cargo transfer", pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(CompTransporter), nameof(CompTransporter.AddToTheToLoadList), new[] { typeof(TransferableOneWay), typeof(int) })]
    internal static class HistoricalTransportPodLoadingPatch
    {
        private static bool Prefix(TransferableOneWay t)
        {
            if (!(t?.AnyThing is Pawn pawn) || HistoricalCharacterProtectionService.CanRelocate(pawn)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("transport pod loading", pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryStartCarry), new[] { typeof(Thing) })]
    internal static class HistoricalPawnCarryProtectionPatch
    {
        private static bool Prefix(Thing item, ref bool __result)
        {
            if (!(item is Pawn pawn) || HistoricalCharacterProtectionService.CanRelocate(pawn)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("carrying a historical character", pawn);
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryStartCarry), new[] { typeof(Thing), typeof(int), typeof(bool) })]
    internal static class HistoricalPawnCarryCountProtectionPatch
    {
        private static bool Prefix(Thing item, ref int __result)
        {
            if (!(item is Pawn pawn) || HistoricalCharacterProtectionService.CanRelocate(pawn)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("carrying a historical character", pawn);
            __result = 0;
            return false;
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_CarryPawn), "GetSingleOptionFor", new[] { typeof(Pawn), typeof(FloatMenuContext) })]
    internal static class HistoricalCarryMenuProtectionPatch
    {
        private static bool Prefix(Pawn clickedPawn, ref FloatMenuOption __result)
        {
            if (HistoricalCharacterProtectionService.CanRelocate(clickedPawn)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("carry menu", clickedPawn);
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_CarryPawnToExit), "GetSingleOptionFor", new[] { typeof(Pawn), typeof(FloatMenuContext) })]
    internal static class HistoricalCarryToExitMenuProtectionPatch
    {
        private static bool Prefix(Pawn clickedPawn, ref FloatMenuOption __result)
        {
            if (HistoricalCharacterProtectionService.CanRelocate(clickedPawn)) return true;
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_CarryToCryptosleepCasket), "GetSingleOptionFor", new[] { typeof(Pawn), typeof(FloatMenuContext) })]
    internal static class HistoricalCasketMenuProtectionPatch
    {
        private static bool Prefix(Pawn clickedPawn, ref FloatMenuOption __result)
        {
            if (HistoricalCharacterProtectionService.CanRelocate(clickedPawn)) return true;
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_CarryToShuttle), "GetSingleOptionFor", new[] { typeof(Pawn), typeof(FloatMenuContext) })]
    internal static class HistoricalShuttleMenuProtectionPatch
    {
        private static bool Prefix(Pawn clickedPawn, ref FloatMenuOption __result)
        {
            if (HistoricalCharacterProtectionService.CanRelocate(clickedPawn)) return true;
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_CarryToBiosculpterPod), "GetOptionsFor", new[] { typeof(Pawn), typeof(FloatMenuContext) })]
    internal static class HistoricalBiosculpterMenuProtectionPatch
    {
        private static bool Prefix(Pawn clickedPawn, ref IEnumerable<FloatMenuOption> __result)
        {
            if (HistoricalCharacterProtectionService.CanRelocate(clickedPawn)) return true;
            __result = System.Array.Empty<FloatMenuOption>();
            return false;
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_CapturePawn), "GetSingleOptionFor", new[] { typeof(Pawn), typeof(FloatMenuContext) })]
    internal static class HistoricalCaptureMenuProtectionPatch
    {
        private static bool Prefix(Pawn clickedPawn, ref FloatMenuOption __result)
        {
            if (HistoricalCharacterProtectionService.CanCapture(clickedPawn)) return true;
            HistoricalCharacterProtectionService.ReportBlocked("capture menu", clickedPawn);
            __result = null;
            return false;
        }
    }
}
