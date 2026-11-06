using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Characters
{
    public static class HistoricalChineseNameGenerator
    {
        private static readonly HashSet<string> usedNames = new HashSet<string>(StringComparer.Ordinal);

        public static NameTriple Generate(HistoricalChineseNamePoolDef pool)
        {
            if (pool?.surnames == null || pool.surnames.Count == 0
                || pool.givenNames == null || pool.givenNames.Count == 0)
                return new NameTriple("无", "无名", "名");

            int combinationCount = pool.surnames.Count * pool.givenNames.Count;
            lock (usedNames)
            {
                for (int attempt = 0; attempt < combinationCount; attempt++)
                {
                    string surname = pool.surnames[Rand.Range(0, pool.surnames.Count)];
                    string givenName = pool.givenNames[Rand.Range(0, pool.givenNames.Count)];
                    string fullName = surname + givenName;
                    if (usedNames.Add(fullName)) return AsSingleDisplayedName(surname, givenName);
                }
            }

            // Extremely unlikely with the initial pool size; retain normal generation if exhausted.
            return AsSingleDisplayedName(pool.surnames.RandomElement(), pool.givenNames.RandomElement());
        }

        private static NameTriple AsSingleDisplayedName(string surname, string givenName)
        {
            // RimWorld requires both first and last names to be non-empty in the
            // starting-pawn editor, and its parent-relation worker expects NameTriple.
            // Use the full Chinese name as the nickname shown in pawn labels.
            return new NameTriple(surname, surname + givenName, givenName);
        }

        public static void ResetUsedNames()
        {
            lock (usedNames) usedNames.Clear();
        }
    }
}
