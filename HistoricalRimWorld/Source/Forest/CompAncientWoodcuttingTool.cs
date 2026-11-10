using System;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    public sealed class CompProperties_AncientWoodcuttingTool : CompProperties
    {
        public CompProperties_AncientWoodcuttingTool() => compClass = typeof(CompAncientWoodcuttingTool);
    }

    public sealed class CompAncientWoodcuttingTool : ThingComp
    {
        private float workSinceWear;

        public void ApplyWorkWear(float effectiveWork)
        {
            HR_WoodcuttingToolExtension extension = parent?.def?.GetModExtension<HR_WoodcuttingToolExtension>();
            if (extension == null || extension.workPerHitPoint <= 0f || effectiveWork <= 0f || parent.Destroyed) return;

            workSinceWear += effectiveWork;
            int damage = (int)(workSinceWear / extension.workPerHitPoint);
            if (damage <= 0) return;
            workSinceWear -= damage * extension.workPerHitPoint;
            parent.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, damage));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref workSinceWear, "ancientWoodcuttingWorkSinceWear", 0f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && (float.IsNaN(workSinceWear) || float.IsInfinity(workSinceWear)))
                workSinceWear = 0f;
        }
    }
}
