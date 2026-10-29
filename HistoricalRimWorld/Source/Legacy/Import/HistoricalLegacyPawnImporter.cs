using System;
using HistoricalRimWorld.Legacy.Simulation;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Legacy.Import
{
    /// <summary>Creates a fresh game Pawn for an already validated archive person.</summary>
    public static class HistoricalLegacyPawnImporter
    {
        public static bool TryCreatePawn(LegacyDescendantCandidate candidate, out Pawn pawn, out string error)
        {
            pawn = null;
            error = null;
            if (candidate == null || !candidate.IsEligibleForStartingPawn)
            { error = "The selected person is not an eligible starting Pawn."; return false; }
            if (string.IsNullOrWhiteSpace(candidate.DisplayName) || candidate.HistoricalAgeYears < 18 || candidate.HistoricalAgeYears > 70)
            { error = "The selected candidate has invalid name or age data."; return false; }
            try
            {
                pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                if (pawn == null || pawn.Destroyed || pawn.ageTracker == null)
                { error = "RimWorld did not generate a valid colonist Pawn."; pawn = null; return false; }
                pawn.Name = new NameSingle(candidate.DisplayName, false);
                // Historical age stays on the HistoricalPersonRecord calendar; Biotech owns biological age.
                if (!string.IsNullOrWhiteSpace(candidate.Gender)
                    && Enum.TryParse(candidate.Gender, true, out Gender parsedGender)
                    && (parsedGender == Gender.Male || parsedGender == Gender.Female))
                    pawn.gender = parsedGender;
                return true;
            }
            catch (Exception exception)
            {
                if (pawn != null && !pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
                pawn = null;
                error = "Pawn generation failed: " + exception.Message;
                return false;
            }
        }
    }
}
