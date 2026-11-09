using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Scenarios.EasternZhou;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Characters
{
    /// <summary>Selects the mod's whitelist of life stories for pawns generated in a historical scenario.</summary>
    public static class HistoricalBackstorySelector
    {
        private const string EasternZhouScenario = "HR_Scenario_EasternZhou_770BCE";
        private const int AdultAge = 18;
        private static bool validated;
        private static List<BackstoryDef> childhoodBackstories;
        private static List<BackstoryDef> adulthoodBackstories;

        public static bool IsHistoricalScenarioActive()
        {
            return HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
                && setup.scenario != null
                && string.Equals(setup.scenario.defName, EasternZhouScenario, StringComparison.Ordinal);
        }

        public static void AssignForGeneratedPawn(Pawn pawn)
        {
            if (pawn?.story == null || pawn.RaceProps == null || !pawn.RaceProps.Humanlike || !IsHistoricalScenarioActive()) return;
            EnsureValidated();
            if (!TryGetCurrentBceYear(out int currentBceYear)) return;

            int age = Math.Max(0, pawn.ageTracker?.AgeBiologicalYears ?? 0);
            string identity = InferIdentity(pawn);
            int childhoodYearBce = currentBceYear + Math.Max(0, age - 8);
            BackstoryDef childhood = Choose(BackstorySlot.Childhood, childhoodYearBce, identity);
            if (childhood != null) pawn.story.Childhood = childhood;

            if (age >= AdultAge)
            {
                int adulthoodYearBce = currentBceYear + Math.Max(0, age - AdultAge);
                BackstoryDef adulthood = Choose(BackstorySlot.Adulthood, adulthoodYearBce, identity);
                if (adulthood != null) pawn.story.Adulthood = adulthood;
            }
            else
            {
                // Leave adulthood unset for children; the adulthood getter below assigns it only
                // when vanilla or another system first requests an adult history.
                pawn.story.Adulthood = null;
            }
        }

        public static BackstoryDef Choose(BackstorySlot slot, int livedYearBce, string identity)
        {
            EnsureIndex();
            List<BackstoryDef> indexed = slot == BackstorySlot.Childhood ? childhoodBackstories : adulthoodBackstories;
            List<BackstoryDef> candidates = indexed.Where(def => IsAvailable(def, livedYearBce)).ToList();
            if (candidates.Count == 0) return null;

            float total = candidates.Sum(def => WeightFor(def, identity));
            if (total <= 0f) return candidates[0];
            float choice = Rand.Value * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                choice -= WeightFor(candidates[i], identity);
                if (choice <= 0f) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        public static string InferIdentity(Pawn pawn)
        {
            string kind = ((pawn?.kindDef?.defName ?? string.Empty) + " " + (pawn?.kindDef?.label ?? string.Empty)).ToLowerInvariant();
            if (ContainsAny(kind, "farmer", "farm", "herder", "shepherd", "ranch")) return "Farmer";
            if (ContainsAny(kind, "craft", "smith", "artisan", "builder", "worker")) return "Craftsman";
            if (ContainsAny(kind, "merchant", "trader", "caravan", "visitor")) return "Merchant";
            if (ContainsAny(kind, "noble", "royal", "court", "official")) return "Noble";
            if (ContainsAny(kind, "scholar", "teacher", "scribe", "intellectual")) return "Scholar";
            if (ContainsAny(kind, "soldier", "warrior", "guard", "militia", "pirate", "raider")) return "Soldier";
            if (ContainsAny(kind, "priest", "shaman", "cult", "religious")) return "Religious";
            if (ContainsAny(kind, "servant", "slave", "laborer", "labor", "worker")) return "Servant";
            return "Wanderer";
        }

        public static void EnsureAdultBackstory(Pawn pawn)
        {
            Pawn_StoryTracker story = pawn?.story;
            if (story == null || story.Adulthood != null || pawn?.ageTracker == null
                || pawn.ageTracker.AgeBiologicalYearsFloat < AdultAge || !pawn.RaceProps.Humanlike
                || !IsHistoricalScenarioActive() || !TryGetCurrentBceYear(out int currentBceYear)) return;

            EnsureValidated();
            int age = Math.Max(AdultAge, pawn.ageTracker.AgeBiologicalYears);
            int livedYearBce = currentBceYear + Math.Max(0, age - AdultAge);
            story.Adulthood = Choose(BackstorySlot.Adulthood, livedYearBce, InferIdentity(pawn));
        }

        private static bool IsAvailable(BackstoryDef def, int livedYearBce)
        {
            HistoricalBackstoryDefExtension extension = def.GetModExtension<HistoricalBackstoryDefExtension>();
            if (extension == null || !extension.availableFromYearBCE.HasValue) return true;
            return livedYearBce <= extension.availableFromYearBCE.Value;
        }

        private static float WeightFor(BackstoryDef def, string identity)
        {
            HistoricalBackstoryDefExtension extension = def.GetModExtension<HistoricalBackstoryDefExtension>();
            HistoricalBackstoryIdentityWeight match = extension?.identityWeights?.FirstOrDefault(item =>
                item != null && string.Equals(item.identity, identity, StringComparison.OrdinalIgnoreCase));
            if (match != null) return Math.Max(0.01f, match.weight);
            return 1f;
        }

        private static bool TryGetCurrentBceYear(out int yearBce)
        {
            if (HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date))
            {
                yearBce = AstronomicalToBce(date.AstronomicalYear);
                return true;
            }

            if (HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
                && setup.scenario != null)
            {
                yearBce = AstronomicalToBce(setup.scenario.startAstronomicalYear);
                return true;
            }

            yearBce = 0;
            return false;
        }

        private static int AstronomicalToBce(int astronomicalYear)
        {
            HistoricalYearConverter.FromAstronomicalYear(astronomicalYear, out int year, out bool isBce);
            return isBce ? year : 0;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (value.IndexOf(needles[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static void EnsureValidated()
        {
            if (validated) return;
            validated = true;
            List<string> errors = HistoricalBackstoryValidator.ValidateAll();
            int childhood = DefDatabase<BackstoryDef>.AllDefsListForReading.Count(def => def?.defName?.StartsWith("HR_C_", StringComparison.Ordinal) == true);
            int adulthood = DefDatabase<BackstoryDef>.AllDefsListForReading.Count(def => def?.defName?.StartsWith("HR_A_", StringComparison.Ordinal) == true);
            if (errors.Count == 0)
                Log.Message("[Historical RimWorld] HR-036 backstories validated: childhood=" + childhood + ", adulthood=" + adulthood + ", total=" + (childhood + adulthood) + ".");
            else
                foreach (string error in errors) Log.Error("[Historical RimWorld] HR-036 validation: " + error);
        }

        private static void EnsureIndex()
        {
            if (childhoodBackstories != null && adulthoodBackstories != null) return;
            List<BackstoryDef> all = DefDatabase<BackstoryDef>.AllDefsListForReading;
            childhoodBackstories = all.Where(def => def?.defName?.StartsWith("HR_C_", StringComparison.Ordinal) == true).ToList();
            adulthoodBackstories = all.Where(def => def?.defName?.StartsWith("HR_A_", StringComparison.Ordinal) == true).ToList();
        }
    }
}
