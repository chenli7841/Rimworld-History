using System;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Genealogy;
using Verse;

namespace HistoricalRimWorld.Lifespan
{
    public static class HistoricalNaturalDeathService
    {
        internal static void ProcessDueDeaths(HistoricalGenealogyGameComponent component, HistoricalDate currentDate)
        {
            if (component == null) return;
            foreach (HistoricalNaturalLifespanRecord lifespan in component.LifespanRecords
                .Where(item => item != null && item.IsValid() && !item.NaturalDeathProcessed && item.NaturalDeathDate <= currentDate).ToList())
            {
                HistoricalPersonRecord person = component.FindPerson(lifespan.PersonId);
                Pawn pawn = component.FindPawn(lifespan.PersonId);
                if (person == null) continue;
                if (!person.IsAlive) { lifespan.SetNaturalDeathProcessed(true); continue; }
                if (pawn == null || pawn.Destroyed) continue;
                if (pawn.Dead)
                {
                    component.RecordPawnDeath(lifespan.PersonId, currentDate, HistoricalDeathCause.NaturalLifespan);
                    lifespan.SetNaturalDeathProcessed(true);
                    continue;
                }

                try
                {
                    // Verified against the local RimWorld 1.6 Assembly-CSharp API: Pawn.Kill(Nullable<DamageInfo>, Hediff).
                    pawn.Kill(null, null);
                    if (pawn.Dead)
                    {
                        component.RecordPawnDeath(lifespan.PersonId, currentDate, HistoricalDeathCause.NaturalLifespan);
                        lifespan.SetNaturalDeathProcessed(true);
                    }
                    else Log.Warning("[Historical RimWorld] Pawn.Kill did not kill person " + lifespan.PersonId + "; natural death will be checked again next historical day.");
                }
                catch (Exception exception)
                {
                    Log.Error("[Historical RimWorld] Natural death processing failed for " + lifespan.PersonId + ": " + exception);
                }
            }
        }
    }
}
