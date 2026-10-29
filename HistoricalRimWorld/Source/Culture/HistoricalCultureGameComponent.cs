using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Culture
{
    public sealed class HistoricalCultureGameComponent : GameComponent
    {
        private const int CurrentDataVersion = 1;
        private string scenarioDefName;
        private int dataVersion;
        private bool initialized;
        private List<HistoricalPersonCultureState> persons = new List<HistoricalPersonCultureState>();
        private List<HistoricalFamilyCultureState> families = new List<HistoricalFamilyCultureState>();
        private List<HistoricalCommunityCultureState> communities = new List<HistoricalCommunityCultureState>();

        public HistoricalCultureGameComponent(Game game) { }
        public bool Initialized => initialized;
        public string ScenarioDefName => scenarioDefName;
        public IEnumerable<HistoricalPersonCultureState> Persons => persons ?? Enumerable.Empty<HistoricalPersonCultureState>();
        public IEnumerable<HistoricalFamilyCultureState> Families => families ?? Enumerable.Empty<HistoricalFamilyCultureState>();
        public IEnumerable<HistoricalCommunityCultureState> Communities => communities ?? Enumerable.Empty<HistoricalCommunityCultureState>();
        public HistoricalPersonCultureState FindPerson(string id) => persons?.FirstOrDefault(item => item != null && item.PersonId == id);
        public HistoricalFamilyCultureState FindFamily(string id) => families?.FirstOrDefault(item => item != null && item.FamilyId == id);
        public HistoricalCommunityCultureState FindCommunity(string id) => communities?.FirstOrDefault(item => item != null && item.CommunityId == id);
        public void Add(HistoricalPersonCultureState state) { if (state != null && FindPerson(state.PersonId) == null) (persons ??= new List<HistoricalPersonCultureState>()).Add(state); }
        public void Add(HistoricalFamilyCultureState state) { if (state != null && FindFamily(state.FamilyId) == null) (families ??= new List<HistoricalFamilyCultureState>()).Add(state); }
        public void Add(HistoricalCommunityCultureState state) { if (state != null && FindCommunity(state.CommunityId) == null) (communities ??= new List<HistoricalCommunityCultureState>()).Add(state); }
        public void RollbackLegacyData(IEnumerable<string> personIds, IEnumerable<string> familyIds)
        {
            var people = new HashSet<string>(personIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var familiesToRemove = new HashSet<string>(familyIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            persons.RemoveAll(item => item != null && people.Contains(item.PersonId));
            families.RemoveAll(item => item != null && familiesToRemove.Contains(item.FamilyId));
        }

        public bool Initialize(string scenarioId, HistoricalCultureSetupDef setup)
        {
            if (initialized) return string.Equals(scenarioDefName, scenarioId, StringComparison.Ordinal);
            if (setup == null || string.IsNullOrWhiteSpace(setup.communityId) || setup.dominantCulture == null) return false;
            scenarioDefName = scenarioId;
            dataVersion = CurrentDataVersion;
            communities = communities ?? new List<HistoricalCommunityCultureState>();
            HistoricalCommunityCultureState community = new HistoricalCommunityCultureState
            {
                CommunityId = setup.communityId,
                DominantCultureId = setup.dominantCulture.defName,
                ActiveCustomIds = (setup.activeCustomIds ?? new List<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList()
            };
            community.CulturalPresence = (setup.culturalPresence ?? new List<HistoricalCulturePresenceSetup>())
                .Where(item => item?.culture != null).GroupBy(item => item.culture.defName, StringComparer.Ordinal)
                .Select(group => new HistoricalCulturalFamiliarity { CultureId = group.Key, Familiarity = Clamp(group.First().familiarity) }).ToList();
            if (!community.CulturalPresence.Any(item => item.CultureId == community.DominantCultureId))
                community.CulturalPresence.Add(new HistoricalCulturalFamiliarity { CultureId = community.DominantCultureId, Familiarity = 0.8f });
            Add(community);
            initialized = true;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioDefName, "historicalCultureScenarioDefName");
            Scribe_Values.Look(ref dataVersion, "historicalCultureDataVersion", 0);
            Scribe_Values.Look(ref initialized, "historicalCultureInitialized", false);
            Scribe_Collections.Look(ref persons, "historicalPersonCultures", LookMode.Deep);
            Scribe_Collections.Look(ref families, "historicalFamilyCultures", LookMode.Deep);
            Scribe_Collections.Look(ref communities, "historicalCommunityCultures", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                persons = persons ?? new List<HistoricalPersonCultureState>();
                families = families ?? new List<HistoricalFamilyCultureState>();
                communities = communities ?? new List<HistoricalCommunityCultureState>();
                dataVersion = Math.Max(dataVersion, CurrentDataVersion);
            }
        }
        private static float Clamp(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
    }
}
