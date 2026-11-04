using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Characters
{
    public static class HistoricalChineseNameGenerator
    {
        private static readonly HashSet<string> usedNames = new HashSet<string>(StringComparer.Ordinal);

        public static NameSingle Generate(HistoricalChineseNamePoolDef pool)
        {
            if (pool?.surnames == null || pool.surnames.Count == 0
                || pool.givenNames == null || pool.givenNames.Count == 0)
                return new NameSingle("无名", false);

            int combinationCount = pool.surnames.Count * pool.givenNames.Count;
            lock (usedNames)
            {
                for (int attempt = 0; attempt < combinationCount; attempt++)
                {
                    string fullName = pool.surnames[Rand.Range(0, pool.surnames.Count)]
                        + pool.givenNames[Rand.Range(0, pool.givenNames.Count)];
                    if (usedNames.Add(fullName)) return new NameSingle(fullName, false);
                }
            }

            // Extremely unlikely with the initial pool size; retain normal generation if exhausted.
            return new NameSingle(pool.surnames.RandomElement() + pool.givenNames.RandomElement(), false);
        }

        public static void ResetUsedNames()
        {
            lock (usedNames) usedNames.Clear();
        }
    }
}
