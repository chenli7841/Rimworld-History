using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Patches.Technology;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.Technology
{
    /// <summary>Deterministic, interval-based world technology updates. No random rolls occur from UI drawing.</summary>
    public static class HistoricalTechnologyDiffusionService
    {
        private static readonly HashSet<string> MissingOriginWarningKeys = new HashSet<string>(StringComparer.Ordinal);
        private static bool suppressNotifications;

        public static void ProcessDay(HistoricalDate date)
        {
            HistoricalTechnologyGameComponent component = Current.Game?.GetComponent<HistoricalTechnologyGameComponent>();
            HistoricalScenarioDef scenario = HistoricalScenarioManager.CurrentScenario;
            if (component == null || !component.Initialized || scenario == null || !HistoricalCalendarManager.IsActive) return;

            List<ResearchProjectDef> projects = HistoricalTechnologyTimelineLayout.HistoricalProjects
                .Where(item => HistoricalTechnologyTimelineLayout.GetExtension(item) != null)
                .OrderBy(item => item.defName, StringComparer.Ordinal).ToList();
            foreach (ResearchProjectDef project in projects) TryEmerge(project, date, component);

            HistoricalTechnologySetupDef setup = DefDatabase<HistoricalTechnologySetupDef>.AllDefsListForReading
                .FirstOrDefault(item => item?.scenario?.defName == scenario.defName);
            int intervalDays = Math.Max(1, setup?.diffusionIntervalDays ?? 30);
            foreach (ResearchProjectDef project in projects)
            {
                HistoricalTechnologyScheduleState schedule = component.GetOrAddSchedule(project.defName);
                if (schedule == null) continue;
                if (schedule.LastDiffusionAbsoluteDay == long.MinValue) schedule.LastDiffusionAbsoluteDay = date.AbsoluteDay;
                long firstDueDay = schedule.LastDiffusionAbsoluteDay + intervalDays;
                if (firstDueDay > date.AbsoluteDay) continue;
                long catchUpFloor = date.AbsoluteDay - (long)intervalDays * 120;
                if (firstDueDay < catchUpFloor) firstDueDay = catchUpFloor;
                for (long updateDay = firstDueDay; updateDay <= date.AbsoluteDay; updateDay += intervalDays)
                {
                    Diffuse(project, HistoricalDate.FromAbsoluteDay(updateDay), component, intervalDays, setup, knowledgeDate: date);
                    schedule.LastDiffusionAbsoluteDay = updateDay;
                }
            }
        }

        public static void ForceEmergence(ResearchProjectDef project)
        {
            if (project == null || !HistoricalTechnologyManager.IsActive) return;
            HistoricalTechnologyGameComponent component = Current.Game?.GetComponent<HistoricalTechnologyGameComponent>();
            if (component == null || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date)) return;
            if (GetMasteredSources(project, component).Count > 0) return;
            HistoricalFactionTechnologyState source = GetEligibleEmergenceSources(project, component).FirstOrDefault();
            if (source == null) return;
            Master(project, source, date.AstronomicalYear, "DeveloperForcedEmergence");
            NotifyEmergence(project, date, component, true);
            HistoricalResearchTimelineUIState.Invalidate();
        }

        public static int ForceDiffusion(ResearchProjectDef project)
        {
            if (project == null || !HistoricalTechnologyManager.IsActive) return 0;
            HistoricalTechnologyGameComponent component = Current.Game?.GetComponent<HistoricalTechnologyGameComponent>();
            if (component == null || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date)) return 0;
            int changed = Diffuse(project, date, component, 1, null, force: true);
            HistoricalResearchTimelineUIState.Invalidate();
            return changed;
        }

        public static bool RevealToPlayer(ResearchProjectDef project)
        {
            if (project == null || !HistoricalTechnologyManager.IsActive) return false;
            HistoricalTechnologyGameComponent component = Current.Game?.GetComponent<HistoricalTechnologyGameComponent>();
            if (component == null || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date)) return false;
            component.GetOrAddAwareness(project.defName)?.Raise(PlayerHistoricalTechAwareness.Confirmed, date.AstronomicalYear);
            HistoricalResearchTimelineUIState.Invalidate();
            return true;
        }

        public static int SimulateYears(int years)
        {
            if (!HistoricalTechnologyManager.IsActive || years <= 0
                || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate start)) return 0;
            HistoricalTechnologyGameComponent component = Current.Game?.GetComponent<HistoricalTechnologyGameComponent>();
            if (component == null) return 0;
            HistoricalTechnologySetupDef setup = DefDatabase<HistoricalTechnologySetupDef>.AllDefsListForReading
                .FirstOrDefault(item => item?.scenario?.defName == HistoricalScenarioManager.ActiveScenarioDefName);
            int intervalDays = Math.Max(1, setup?.diffusionIntervalDays ?? 30);
            long endDay = start.AddYears(years).AbsoluteDay;
            int updates = 0;
            suppressNotifications = true;
            try
            {
                for (long day = start.AbsoluteDay + intervalDays; day <= endDay; day += intervalDays)
                {
                    ProcessDay(HistoricalDate.FromAbsoluteDay(day));
                    updates++;
                }
                if ((endDay - start.AbsoluteDay) % intervalDays != 0)
                {
                    ProcessDay(HistoricalDate.FromAbsoluteDay(endDay));
                    updates++;
                }
                foreach (HistoricalTechnologyScheduleState state in component.ScheduleStates)
                    state.LastDiffusionAbsoluteDay = endDay;
                component.MarkDayProcessed(endDay);
            }
            finally { suppressNotifications = false; }
            HistoricalResearchTimelineUIState.Invalidate();
            return updates;
        }

        private static void TryEmerge(ResearchProjectDef project, HistoricalDate date, HistoricalTechnologyGameComponent component)
        {
            if (GetMasteredSources(project, component).Count > 0) return;
            HistoricalTechExtension extension = HistoricalTechnologyTimelineLayout.GetExtension(project);
            int? start = HistoricalTechnologyTimelineLayout.GetSpreadStartAstronomicalYear(project);
            if (extension == null || !start.HasValue || date.AstronomicalYear < start.Value) return;
            HistoricalTechnologyScheduleState schedule = component.GetOrAddSchedule(project.defName);
            if (schedule == null || schedule.LastEmergenceAttemptAstronomicalYear >= date.AstronomicalYear) return;

            List<HistoricalFactionTechnologyState> candidates = GetEligibleEmergenceSources(project, component);
            if (candidates.Count == 0)
            {
                string key = project.defName;
                if (MissingOriginWarningKeys.Add(key))
                    Log.Warning("[Historical RimWorld] No eligible origin faction profile is available for " + project.defName
                        + "; the technology remains un-emerged rather than being assigned to an unrelated polity.");
                schedule.LastEmergenceAttemptAstronomicalYear = date.AstronomicalYear;
                return;
            }

            candidates = candidates.OrderBy(item => item.StableFactionId, StringComparer.Ordinal).ToList();
            int currentYear = date.AstronomicalYear;
            int firstYear = schedule.LastEmergenceAttemptAstronomicalYear == int.MinValue
                ? start.Value : Math.Max(start.Value, schedule.LastEmergenceAttemptAstronomicalYear + 1);
            int catchUpFloorYear = Math.Max(start.Value, currentYear - 200);
            int? maturityEnd = HistoricalTechnologyTimelineLayout.GetSpreadEndAstronomicalYear(project);
            if (maturityEnd.HasValue && maturityEnd.Value < catchUpFloorYear
                && schedule.LastEmergenceAttemptAstronomicalYear < maturityEnd.Value)
                catchUpFloorYear = maturityEnd.Value;
            firstYear = Math.Max(firstYear, catchUpFloorYear);

            for (int year = firstYear; year <= currentYear; year++)
            {
                schedule.LastEmergenceAttemptAstronomicalYear = year;
                float chance = extension.EmergenceChancePerYear * Maturity(project, year);
                int originRoll = StableHash(project.defName + "|emergence-source|" + year) % candidates.Count;
                HistoricalFactionTechnologyState source = candidates[originRoll];
                bool reachedMatureEnd = maturityEnd.HasValue && year >= maturityEnd.Value;
                if (!reachedMatureEnd && StableUnit(project.defName + "|emergence|" + source.StableFactionId + "|" + year) >= chance)
                    continue;

                Master(project, source, year, "HistoricalEmergenceWindow");
                NotifyEmergence(project, date, component, false);
                break;
            }
        }

        private static List<HistoricalFactionTechnologyState> GetEligibleEmergenceSources(ResearchProjectDef project,
            HistoricalTechnologyGameComponent component)
        {
            HistoricalTechExtension extension = HistoricalTechnologyTimelineLayout.GetExtension(project);
            List<HistoricalFactionTechnologyState> all = component.FactionStates
                .Where(state => state != null && !state.IsPlayerFaction && HistoricalTechnologyManager.ResolveFaction(state) != null)
                .OrderBy(state => state.StableFactionId, StringComparer.Ordinal).ToList();
            if (extension?.OriginFactionTags == null || extension.OriginFactionTags.Count == 0) return all;
            List<HistoricalFactionTechnologyState> tagged = all.Where(state =>
            {
                HistoricalFactionTechnologyProfileDef profile = FindProfile(state);
                return profile?.profileTags != null && profile.profileTags.Any(extension.OriginFactionTags.Contains);
            }).ToList();
            return tagged.OrderBy(item => item.StableFactionId, StringComparer.Ordinal).ToList();
        }

        private static int Diffuse(ResearchProjectDef project, HistoricalDate date,
            HistoricalTechnologyGameComponent component, int intervalDays, HistoricalTechnologySetupDef setup,
            bool force = false, HistoricalDate? knowledgeDate = null)
        {
            HistoricalTechExtension extension = HistoricalTechnologyTimelineLayout.GetExtension(project);
            int? spreadStart = HistoricalTechnologyTimelineLayout.GetSpreadStartAstronomicalYear(project);
            if (extension == null || !spreadStart.HasValue || date.AstronomicalYear < spreadStart.Value) return 0;
            List<HistoricalFactionTechnologyState> sources = GetMasteredSources(project, component);
            if (sources.Count == 0) return 0;
            int changed = 0;
            foreach (HistoricalFactionTechnologyState target in component.FactionStates.Where(item => item != null))
            {
                Faction targetFaction = HistoricalTechnologyManager.ResolveFaction(target);
                if (targetFaction == null) continue;
                HistoricalFactionTechRecord targetRecord = target.Find(project.defName);
                FactionTechKnowledgeLevel current = targetRecord?.Level ?? FactionTechKnowledgeLevel.Unknown;
                if (current >= FactionTechKnowledgeLevel.Mastered) continue;
                List<Tuple<HistoricalFactionTechnologyState, float>> links = sources
                    .Where(source => source.StableFactionId != target.StableFactionId)
                    .Select(source => Tuple.Create(source, ConnectionWeight(HistoricalTechnologyManager.ResolveFaction(source),
                        targetFaction, source, target, extension, setup)))
                    .Where(pair => pair.Item2 > 0f).ToList();
                if (target.IsPlayerFaction && current == FactionTechKnowledgeLevel.HeardOf)
                {
                    HashSet<string> alreadyKnown = new HashSet<string>(component.FindAwareness(project.defName)?.KnownSourceFactionIds
                        ?? new List<string>(), StringComparer.Ordinal);
                    List<Tuple<HistoricalFactionTechnologyState, float>> newSources = links
                        .Where(pair => !alreadyKnown.Contains(pair.Item1.StableFactionId)).ToList();
                    if (newSources.Count > 0) links = newSources;
                }
                if (links.Count == 0) continue;

                links.Sort((a, b) => b.Item2.CompareTo(a.Item2));
                HistoricalFactionTechnologyState sourceState = links[0].Item1;
                float probability = Mathf.Clamp01(extension.DiffusionChancePerInterval * Maturity(project, date.AstronomicalYear)
                    * links[0].Item2 / Math.Max(0.1f, extension.DiffusionDifficulty)
                    * (FindProfile(target)?.diffusionSusceptibility ?? 1f));
                string rollKey = project.defName + "|diffuse|" + sourceState.StableFactionId + "|"
                    + target.StableFactionId + "|" + (date.AbsoluteDay / Math.Max(1, intervalDays));
                if (!force && StableUnit(rollKey) >= probability) continue;

                if (targetRecord == null) targetRecord = target.GetOrAdd(project.defName);
                if (current == FactionTechKnowledgeLevel.Unknown)
                {
                    targetRecord.SetLevel(FactionTechKnowledgeLevel.HeardOf, date.AstronomicalYear, "DiffusionRumor:" + sourceState.StableFactionId);
                    changed++;
                    if (target.IsPlayerFaction)
                        OnPlayerLearnedSource(project, sourceState, knowledgeDate ?? date, component);
                    continue;
                }

                if (target.IsPlayerFaction)
                {
                    OnPlayerLearnedSource(project, sourceState, knowledgeDate ?? date, component);
                    continue; // Phase 2 creates intelligence only; no player mastery or research authorization.
                }

                targetRecord.SetLevel(FactionTechKnowledgeLevel.Mastered, date.AstronomicalYear,
                    (extension.AllowTradeDiffusion ? "TradeOrContact:" : "NeighborContact:") + sourceState.StableFactionId);
                targetRecord.LastDiffusionGameTick = Find.TickManager?.TicksGame ?? -1;
                changed++;
            }
            return changed;
        }

        private static float ConnectionWeight(Faction source, Faction target, HistoricalFactionTechnologyState sourceState,
            HistoricalFactionTechnologyState targetState, HistoricalTechExtension extension, HistoricalTechnologySetupDef setup)
        {
            if (source == null || target == null || source == target) return 0f;
            float weight = 0f;
            float distance = NearestSettlementDistance(source, target);
            if (extension.AllowNeighborDiffusion)
            {
                if (distance != float.MaxValue)
                {
                    float distanceWeight = distance <= 30f ? 1f : distance <= 80f ? 0.55f : (setup?.remoteConnectionWeight ?? 0.18f);
                    weight = Math.Max(weight, distanceWeight * (setup?.neighborDistanceWeight ?? 1f));
                }
            }
            if (extension.AllowTradeDiffusion)
            {
                float relationWeight = source.HostileTo(target) ? 0.3f : 1f;
                int goodwill = source.BaseGoodwillWith(target);
                if (goodwill >= 40) relationWeight *= 1.35f;
                else if (goodwill < 0) relationWeight *= 0.6f;
                bool hasModeledTradeContact = distance <= 80f || goodwill >= 40;
                HistoricalStateDef sourceStateDef;
                HistoricalStateDef targetStateDef;
                if (HistoricalFactionManager.TryGetHistoricalState(source, out sourceStateDef)
                    && HistoricalFactionManager.TryGetHistoricalState(target, out targetStateDef))
                {
                    if (HistoricalPoliticsManager.HasRelation(sourceStateDef.defName, targetStateDef.defName, HistoricalPoliticalRelationType.Alliance)
                        || HistoricalPoliticsManager.HasRelation(sourceStateDef.defName, targetStateDef.defName, HistoricalPoliticalRelationType.Tributary))
                    {
                        relationWeight *= 1.3f;
                        hasModeledTradeContact = true;
                    }
                    if (HistoricalPoliticsManager.HasRelation(sourceStateDef.defName, targetStateDef.defName, HistoricalPoliticalRelationType.Hostility))
                        relationWeight *= 0.45f;
                }
                if (hasModeledTradeContact)
                    weight = Math.Max(weight, relationWeight * (setup?.tradeConnectionWeight ?? 1.35f));
            }
            if (extension.IsRestrictedMilitaryTechnology) weight *= 0.35f;
            if (extension.PreferredCultureTags != null && extension.PreferredCultureTags.Count > 0)
            {
                HistoricalFactionTechnologyProfileDef targetProfile = FindProfile(targetState);
                if (targetProfile?.cultureTags != null
                    && targetProfile.cultureTags.Any(tag => extension.PreferredCultureTags.Contains(tag)))
                    weight *= 1.2f;
            }
            return Mathf.Clamp(weight, 0f, 2f);
        }

        private static float NearestSettlementDistance(Faction first, Faction second)
        {
            if (Find.World?.worldObjects == null || Find.World.grid == null) return float.MaxValue;
            List<Settlement> a = Find.World.worldObjects.Settlements.Where(item => item?.Faction == first).ToList();
            List<Settlement> b = Find.World.worldObjects.Settlements.Where(item => item?.Faction == second).ToList();
            if (a.Count == 0 || b.Count == 0) return float.MaxValue;
            float nearest = float.MaxValue;
            foreach (Settlement left in a)
            foreach (Settlement right in b)
                nearest = Math.Min(nearest, Find.World.grid.ApproxDistanceInTiles(left.Tile, right.Tile));
            return nearest;
        }

        private static List<HistoricalFactionTechnologyState> GetMasteredSources(ResearchProjectDef project,
            HistoricalTechnologyGameComponent component) => component.FactionStates
            .Where(state => state != null && state.Find(project.defName)?.Level == FactionTechKnowledgeLevel.Mastered
                && HistoricalTechnologyManager.ResolveFaction(state) != null)
            .OrderBy(state => state.StableFactionId, StringComparer.Ordinal).ToList();

        private static void Master(ResearchProjectDef project, HistoricalFactionTechnologyState state, int year, string source)
        {
            if (project == null || state == null) return;
            state.GetOrAdd(project.defName)?.SetLevel(FactionTechKnowledgeLevel.Mastered, year, source);
        }

        private static float Maturity(ResearchProjectDef project, int astronomicalYear)
        {
            int? start = HistoricalTechnologyTimelineLayout.GetSpreadStartAstronomicalYear(project);
            int? end = HistoricalTechnologyTimelineLayout.GetSpreadEndAstronomicalYear(project);
            if (!start.HasValue) return 0f;
            if (!end.HasValue || end.Value <= start.Value) return astronomicalYear >= start.Value ? 1f : 0f;
            return Mathf.Clamp01(Mathf.InverseLerp(start.Value, end.Value, astronomicalYear));
        }

        private static HistoricalFactionTechnologyProfileDef FindProfile(HistoricalFactionTechnologyState state)
        {
            if (state == null) return null;
            string scenarioId = HistoricalScenarioManager.ActiveScenarioDefName;
            return DefDatabase<HistoricalFactionTechnologyProfileDef>.AllDefsListForReading.FirstOrDefault(profile =>
                profile?.scenario?.defName == scenarioId
                && ((state.HistoricalStateId != null && profile.historicalState?.defName == state.HistoricalStateId)
                    || (profile.factionDef != null && profile.factionDef.defName == state.FactionDefName)));
        }

        private static void NotifyEmergence(ResearchProjectDef project, HistoricalDate date,
            HistoricalTechnologyGameComponent component, bool forced)
        {
            if (suppressNotifications) return;
            RecordChronicle(project, date, "HR_Tech_EmergenceChronicleTitle", "HR_Tech_EmergenceChronicleText");
            if (HistoricalRimWorldMod.CurrentSettings?.UseKnowledgePropagationForTechnologyNotifications == true) return;
            HistoricalTechExtension extension = HistoricalTechnologyTimelineLayout.GetExtension(project);
            if (extension?.NotifyPlayerOnEmergence == false) return;
            PlayerHistoricalTechAwarenessState awareness = component.GetOrAddAwareness(project.defName);
            if (awareness.EmergenceNoticeSent) return;
            awareness.EmergenceNoticeSent = true;
            awareness.LastNoticeAbsoluteDay = date.AbsoluteDay;
            awareness.Raise(PlayerHistoricalTechAwareness.Confirmed, date.AstronomicalYear);
            string title = "HR_Tech_EmergenceLetterTitle".Translate(project.LabelCap);
            string worldStatus = "HR_Tech_WorldStatus_" + HistoricalTechnologyManager.GetWorldStatus(project);
            string body = "HR_Tech_EmergenceLetterText".Translate(project.LabelCap,
                Translator.Translate(worldStatus));
            ReceiveTechnologyLetter(project, title, body);
            Log.Message("[Historical RimWorld] Historical technology emerged: " + project.defName
                + " in astronomical year " + date.AstronomicalYear + (forced ? " (developer-forced)." : "."));
        }

        private static void OnPlayerLearnedSource(ResearchProjectDef project, HistoricalFactionTechnologyState source,
            HistoricalDate date, HistoricalTechnologyGameComponent component)
        {
            PlayerHistoricalTechAwarenessState awareness = component.GetOrAddAwareness(project.defName);
            if (awareness == null || source == null) return;
            bool wasKnown = awareness.KnownSourceFactionIds.Contains(source.StableFactionId);
            PlayerHistoricalTechAwareness nextAwareness = awareness.Awareness == PlayerHistoricalTechAwareness.Unknown
                && awareness.KnownSourceFactionIds.Count == 0
                ? PlayerHistoricalTechAwareness.Rumored : PlayerHistoricalTechAwareness.Confirmed;
            awareness.Raise(nextAwareness, date.AstronomicalYear, source.StableFactionId);
            if (wasKnown || awareness.SourceNoticeSent) return;
            awareness.SourceNoticeSent = true;
            awareness.LastNoticeAbsoluteDay = date.AbsoluteDay;
            if (suppressNotifications) return;
            string factionLabel = HistoricalTechnologyManager.ResolveFaction(source)?.Name ?? source.HistoricalStateId ?? source.FactionDefName;
            bool rumor = nextAwareness == PlayerHistoricalTechAwareness.Rumored;
            ReceiveTechnologyLetter(project,
                rumor ? "HR_Tech_RumorLetterTitle".Translate(project.LabelCap) : "HR_Tech_SourceLetterTitle".Translate(project.LabelCap),
                rumor ? "HR_Tech_RumorLetterText".Translate(project.LabelCap, factionLabel)
                    : "HR_Tech_SourceLetterText".Translate(project.LabelCap, factionLabel));
        }

        private static void RecordChronicle(ResearchProjectDef project, HistoricalDate date, string title, string description)
        {
            HistoricalChronicleManager.TryAddLocalExperience("TechnologyEmergence:" + project.defName, title, description,
                null, null, HistoricalEvidenceLevel.Speculative,
                "Historical spread window is a scenario model based on the technology Def; it is not a precise invention date.", out _);
        }

        private static void ReceiveTechnologyLetter(ResearchProjectDef project, string title, string body)
        {
            LetterDef letterDef = DefDatabase<LetterDef>.GetNamedSilentFail("HR_HistoricalTechnologyLetter");
            if (letterDef == null)
            {
                Find.LetterStack?.ReceiveLetter(title, body, LetterDefOf.NeutralEvent);
                return;
            }
            ChoiceLetter_HistoricalTechnology letter = LetterMaker.MakeLetter(title, body, letterDef) as ChoiceLetter_HistoricalTechnology;
            if (letter == null)
            {
                Find.LetterStack?.ReceiveLetter(title, body, LetterDefOf.NeutralEvent);
                return;
            }
            letter.ResearchDefName = project.defName;
            Find.LetterStack?.ReceiveLetter(letter);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? string.Empty) hash = (hash ^ character) * 16777619;
                return (int)(hash & 0x7fffffff);
            }
        }

        private static float StableUnit(string value) => (StableHash(value) % 1000000) / 1000000f;
    }
}
