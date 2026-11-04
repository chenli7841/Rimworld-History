using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Characters
{
    /// <summary>A scenario-specific pool for generating single-surname, single-given-character names.</summary>
    public sealed class HistoricalChineseNamePoolDef : Def
    {
        public List<string> surnames;
        public List<string> givenNames;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (surnames == null || surnames.Count == 0 || surnames.Any(string.IsNullOrWhiteSpace))
                yield return "Historical Chinese name pool requires at least one surname.";
            if (givenNames == null || givenNames.Count == 0 || givenNames.Any(string.IsNullOrWhiteSpace))
                yield return "Historical Chinese name pool requires at least one given-name character.";
            if (surnames != null && surnames.Any(name => name != null && name.Length != 1))
                yield return "Every surname in this pool must be one character.";
            if (givenNames != null && givenNames.Any(name => name != null && name.Length != 1))
                yield return "Every given name in this pool must be one character.";
        }
    }
}
