using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Culture;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Relics;
using HistoricalRimWorld.Technology;
using HistoricalRimWorld.Legacy;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.Scenarios.EasternZhou;
using HistoricalRimWorld.World;
using HistoricalRimWorld.Events.Actions;
using HistoricalRimWorld.Politics;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalEventScheduler
    {
        private readonly HistoricalEventGameComponent component;
        private readonly HistoricalScenarioDef scenario;
        private readonly Dictionary<string, HistoricalEventDef> eventById;
        private readonly List<HistoricalLocalEventDef> localEvents;
        private sealed class LocalCandidate
        {
            public HistoricalLocalEventDef Event;
            public string StorylineId;
        }

        public HistoricalEventScheduler(HistoricalEventGameComponent owner, string scenarioId)
        {
            component = owner;
            scenario = HistoricalScenarioRegistry.TryGetById(scenarioId, out HistoricalScenarioDef found) ? found : null;
            eventById = DefDatabase<HistoricalEventDef>.AllDefsListForReading
                .Where(item => item.scenario != null && item.scenario.defName == scenarioId)
                .ToDictionary(item => item.defName, StringComparer.Ordinal);
            localEvents = DefDatabase<HistoricalLocalEventDef>.AllDefsListForReading
                .Where(item => item.scenario != null && item.scenario.defName == scenarioId)
                .OrderBy(item => item.defName, StringComparer.Ordinal).ToList();
        }

        public void ProcessDueEvents(HistoricalDate currentDate)
        {
            if (scenario == null || !HistoricalCalendarManager.IsActive) return;
            List<HistoricalEventDef> due = new List<HistoricalEventDef>();
            foreach (string id in component.PendingEventIds.ToList())
            {
                HistoricalEventDef item;
                HistoricalEventRuntimeState state = component.FindState(id);
                if (!eventById.TryGetValue(id, out item))
                {
                    if (state != null) Fail(state, HistoricalEventFailureKind.MissingDefinition,
                        "Event Def is missing from the active scenario: " + id + ".", currentDate);
                    else Log.Error("[Historical RimWorld] Pending event ID has no runtime state: " + id + ".");
                    continue;
                }
                if (state == null)
                {
                    Log.Error("[Historical RimWorld] Event Def " + id + " has no runtime state; it was removed from the pending index.");
                    component.RemovePendingId(id);
                    continue;
                }

                HistoricalDate scheduled = HistoricalEventDateResolver.ResolveScheduledDate(item.historicalDate);
                if (scheduled <= currentDate) due.Add(item);
            }

            List<HistoricalEventDef> orderedDue = due
                .OrderBy(def => HistoricalEventDateResolver.ResolveScheduledDate(def.historicalDate))
                .ThenBy(def => def.priority)
                .ThenBy(def => def.defName, StringComparer.Ordinal).ToList();
            bool progress;
            do
            {
                progress = false;
                foreach (HistoricalEventDef item in orderedDue)
                {
                    HistoricalEventRuntimeState state = component.FindState(item.defName);
                    if (state == null || state.Status != HistoricalEventExecutionStatus.Pending) continue;
                    if (state.LastAttemptAbsoluteDay == currentDate.AbsoluteDay
                        && (state.FailureKind != HistoricalEventFailureKind.WaitingForPrerequisite
                            || !PrerequisitesSettled(item))) continue;
                    try
                    {
                        ProcessEvent(item, state, currentDate);
                    }
                    catch (Exception exception)
                    {
                        Fail(state, HistoricalEventFailureKind.ExecutionException, exception.ToString(), currentDate);
                    }
                    if (state.Status != HistoricalEventExecutionStatus.Pending) progress = true;
                }
            } while (progress);

            ProcessLocalEvents(currentDate);
        }

        private void ProcessLocalEvents(HistoricalDate currentDate)
        {
            if (scenario == null) return;
            AdvanceAutomaticStoryStages(currentDate);
            if (component.PendingLocalEventId != null || localEvents.Count == 0
                || currentDate.AbsoluteDay < component.NextLocalEventAbsoluteDay || Find.CurrentMap == null
                || !Find.CurrentMap.IsPlayerHome || Find.CurrentMap.mapPawns.FreeColonists.Count == 0) return;
            List<LocalCandidate> eligible = new List<LocalCandidate>();
            HashSet<string> storyEvents = new HashSet<string>(DefDatabase<HistoricalStorylineDef>.AllDefsListForReading
                .Where(def => def.scenario?.defName == scenario.defName)
                .SelectMany(def => def.stages ?? new List<HistoricalEventChainStageDef>())
                .Where(stage => stage != null && stage.trigger == HistoricalChainStageTrigger.LocalEvent)
                .Select(stage => stage.eventDefName), StringComparer.Ordinal);
            foreach (HistoricalLocalEventDef item in localEvents.Where(item => item.enabled && IsEligibleLocalEvent(item, currentDate)))
            {
                List<string> storyIds = GetEligibleStorylineIds(item, currentDate);
                if (storyIds.Count > 0)
                    eligible.AddRange(storyIds.Select(id => new LocalCandidate { Event = item, StorylineId = id }));
                else if (!storyEvents.Contains(item.defName))
                    eligible.Add(new LocalCandidate { Event = item });
            }
            if (eligible.Count == 0)
            {
                component.SetNextLocalEventDay(currentDate.AbsoluteDay + 60);
                return;
            }

            int totalWeight = eligible.Sum(item => item.Event.weight);
            int roll = Rand.Range(0, totalWeight);
            LocalCandidate selected = eligible[0];
            foreach (LocalCandidate candidate in eligible)
            {
                roll -= candidate.Event.weight;
                if (roll < 0) { selected = candidate; break; }
            }
            component.SetPendingLocalEvent(selected.Event.defName, selected.StorylineId);
            HistoricalLocalEventRuntimeRecord selectedRecord = component.GetOrCreateLocalRecord(selected.Event.defName);
            selectedRecord.triggerCount++;
            selectedRecord.lastTriggerGameTick = Find.TickManager?.TicksGame ?? -1;
            selectedRecord.isActive = true;
            Messages.Message(Translator.Translate(selected.Event.label) + " — " + Translator.Translate("HR_LocalEvent_Available"),
                MessageTypeDefOf.NeutralEvent, false);
            Log.Message("[Historical RimWorld] Local event offered: " + selected.Event.defName
                + (selected.StorylineId == null ? "." : " in chain " + selected.StorylineId + "."));
        }

        private bool IsEligibleLocalEvent(HistoricalLocalEventDef item, HistoricalDate currentDate)
        {
            HistoricalLocalEventRuntimeRecord record = component.FindLocalRecord(item.defName);
            if (record != null && (record.triggerCount >= item.maxAppearances
                || record.nextEligibleAbsoluteDay > currentDate.AbsoluteDay)) return false;
            if (item.requiredHistoricalEventId != null && !HistoricalEventManager.WasExecuted(item.requiredHistoricalEventId)) return false;
            Map map = Find.CurrentMap;
            if (map == null || !map.IsPlayerHome || map.mapPawns.FreeColonists.Count < item.minimumColonists) return false;
            if (item.requiresMedicalCare && !map.mapPawns.FreeColonists.Any(pawn => pawn.skills?.GetSkill(SkillDefOf.Medicine)?.Level > 0)) return false;
            if (item.requiresFamily && !HistoricalRimWorld.Genealogy.HistoricalGenealogyManager.Families.Any()) return false;
            if (item.requiredResourceCount > 0 && map.listerThings.ThingsOfDef(item.requiredResource).Sum(thing => thing.stackCount) < item.requiredResourceCount) return false;
            if (item.choices.Any(choice => choice.outcome == HistoricalLocalEventOutcome.ChangeCommunityPosture
                || choice.outcome == HistoricalLocalEventOutcome.IncreaseCommunityCulturePresence)
                && string.IsNullOrWhiteSpace(HistoricalScenarioProgressManager.ActiveSetup?.startingCommunityName)) return false;
            HistoricalEventContext context = CreateLocalContext(item, currentDate, map);
            foreach (HistoricalEventCondition condition in item.conditions ?? Enumerable.Empty<HistoricalEventCondition>())
                if (condition == null || !condition.Validate(context, out _) || !condition.Evaluate(context)) return false;
            return true;
        }

        private List<string> GetEligibleStorylineIds(HistoricalLocalEventDef item, HistoricalDate currentDate)
        {
            List<string> result = new List<string>();
            foreach (HistoricalStorylineDef def in DefDatabase<HistoricalStorylineDef>.AllDefsListForReading
                .Where(candidate => candidate.scenario?.defName == scenario.defName))
            {
                HistoricalStorylineRuntimeState state = component.FindStoryline(def.defName);
                if (state?.ended == true) continue;
                int index = state?.currentStage ?? 0;
                if (index < 0 || index >= def.stages.Count) continue;
                HistoricalEventChainStageDef stage = def.stages[index];
                if (stage.trigger != HistoricalChainStageTrigger.LocalEvent || stage.eventDefName != item.defName) continue;
                if ((stage.prerequisiteStageIds ?? new List<string>()).Any(id => state?.completedStageIds?.Contains(id) != true)) continue;
                HistoricalEventContext chainContext = CreateLocalContext(item, currentDate, Find.CurrentMap);
                if (!EvaluateConditions(stage.conditions, chainContext, out _)) continue;
                result.Add(def.defName);
            }
            return result;
        }

        private void AdvanceAutomaticStoryStages(HistoricalDate currentDate)
        {
            foreach (HistoricalStorylineDef def in DefDatabase<HistoricalStorylineDef>.AllDefsListForReading
                .Where(candidate => candidate.scenario?.defName == scenario.defName))
            {
                HistoricalStorylineRuntimeState state = component.FindStoryline(def.defName);
                if (state == null || state.ended || state.startedAbsoluteDay == long.MinValue) continue;
                while (state.currentStage >= 0 && state.currentStage < def.stages.Count)
                {
                    HistoricalEventChainStageDef stage = def.stages[state.currentStage];
                    long deadline = stage.optionalAfterYears <= 0 ? long.MaxValue
                        : state.startedAbsoluteDay + (long)stage.optionalAfterYears * 360L;
                    if (stage.trigger == HistoricalChainStageTrigger.LocalEvent)
                    {
                        if (stage.optional && currentDate.AbsoluteDay >= deadline)
                        {
                            state.failedStageIds.Add(stage.stageId);
                            AdvanceStoryState(state, def, stage, currentDate);
                            continue;
                        }
                        break;
                    }
                    if ((stage.prerequisiteStageIds ?? new List<string>()).Any(id => !state.completedStageIds.Contains(id))) break;
                    bool met = IsAutomaticStageMet(stage, state.startedAbsoluteDay);
                    if (!met && stage.optional && currentDate.AbsoluteDay >= deadline)
                    {
                        state.failedStageIds.Add(stage.stageId);
                        Log.Message("[Historical RimWorld] Optional chain milestone expired without forcing an event: " + def.defName + "/" + stage.stageId + ".");
                        AdvanceStoryState(state, def, stage, currentDate);
                        continue;
                    }
                    if (!met) break;
                    if (stage.trigger == HistoricalChainStageTrigger.ScenarioEnd && def.defName == "HR_Storyline_HRC005"
                        && !state.archiveExported && !TryExportStoryArchive(state, currentDate, out string archiveError))
                    {
                        Log.Warning("[Historical RimWorld] HRC-005 archive export is waiting: " + archiveError);
                        break;
                    }
                    state.completedStageIds.Add(stage.stageId);
                    HistoricalChronicleManager.TryAddLocalExperience("StoryStage:" + def.defName + ":" + stage.stageId,
                        "HR_Story_Stage_Title", "HR_Story_Stage_Description", null, null,
                        HistoricalEvidenceLevel.Speculative, def.sourceNotes, out _);
                    AdvanceStoryState(state, def, stage, currentDate);
                }
            }
        }

        private static bool IsAutomaticStageMet(HistoricalEventChainStageDef stage, long startedAbsoluteDay)
        {
            switch (stage.trigger)
            {
                case HistoricalChainStageTrigger.ObservedBirth:
                    return HistoricalGenealogyManager.Persons.Any(person => person.BirthDateSource == HistoricalBirthDateSource.ObservedInGame
                        && person.BirthDate.AbsoluteDay >= startedAbsoluteDay);
                case HistoricalChainStageTrigger.FamilyDeath:
                    return HistoricalGenealogyManager.Persons.Any(person => !person.IsAlive && person.DeathDate.HasValue
                        && person.DeathDate.Value.AbsoluteDay >= startedAbsoluteDay);
                case HistoricalChainStageTrigger.ScenarioEnd:
                    return HistoricalScenarioProgressManager.IsEndDateReached;
                default: return false;
            }
        }

        private static string FindCurrentFamilyId(Map map)
        {
            foreach (Pawn pawn in map?.mapPawns?.FreeColonists ?? Enumerable.Empty<Pawn>())
                if (HistoricalGenealogyManager.TryGetPersonId(pawn, out string personId)
                    && !string.IsNullOrWhiteSpace(HistoricalGenealogyManager.FindPerson(personId)?.FamilyId))
                    return HistoricalGenealogyManager.FindPerson(personId).FamilyId;
            return HistoricalGenealogyManager.Families
                .Where(family => family.MemberPersonIds.Any(id => HistoricalGenealogyManager.FindPerson(id)?.IsAlive == true))
                .OrderBy(family => family.FamilyId, StringComparer.Ordinal).Select(family => family.FamilyId).FirstOrDefault();
        }

        private static bool TryExportStoryArchive(HistoricalStorylineRuntimeState state, HistoricalDate date, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(state.familyId)) state.familyId = FindCurrentFamilyId(Find.CurrentMap);
            if (string.IsNullOrWhiteSpace(state.familyId)) return Reject("No family record is available for the ending archive.", out error);
            string stem = "EasternZhou_770-740_" + state.familyId + "_" + state.startedAbsoluteDay;
            if (!HistoricalLegacyExporter.TryExportFamily(stem, state.familyId, out string path, out error)) return false;
            state.archiveExported = true;
            state.archiveFileName = System.IO.Path.GetFileName(path);
            HistoricalChronicleManager.TryAddLocalExperience("StoryArchive:HRC005:" + state.familyId,
                "HR_Story_Archive_Title", "HR_Story_Archive_Description", null, null,
                HistoricalEvidenceLevel.Speculative, "Exported through the versioned HR-010 family legacy archive.", out _);
            Log.Message("[Historical RimWorld] HRC-005 family archive exported: " + path + ".");
            return true;
        }

        private static void AdvanceStoryState(HistoricalStorylineRuntimeState state, HistoricalStorylineDef def,
            HistoricalEventChainStageDef stage, HistoricalDate date)
        {
            state.currentStage++;
            if (state.currentStage >= def.stages.Count)
            {
                state.ended = true;
                state.outcome = state.failedStageIds.Count == 0 ? "Completed" : "CompletedWithOptionalMilestonesUnavailable";
            }
        }

        public bool TryResolveLocalEventChoice(string eventId, int choiceIndex, out string error)
        {
            error = null;
            if (component.PendingLocalEventId != eventId) return Reject("That local event is no longer awaiting a choice.", out error);
            HistoricalLocalEventDef item = localEvents.FirstOrDefault(candidate => candidate.defName == eventId);
            if (item == null) return Reject("The pending local event definition is missing.", out error);
            if (item.choices == null || choiceIndex < 0 || choiceIndex >= item.choices.Count)
                return Reject("The selected local event choice is invalid.", out error);
            HistoricalLocalEventChoice choice = item.choices[choiceIndex];
            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate))
                return Reject("The historical calendar is inactive.", out error);
            Map map = Find.CurrentMap;
            if (map == null || !map.IsPlayerHome) return Reject("A player home map is required to resolve this local event.", out error);
            string communityId = HistoricalScenarioProgressManager.ActiveSetup?.startingCommunityName;
            HistoricalEventContext choiceContext = CreateLocalContext(item, currentDate, map);
            HistoricalLocalEventRuntimeRecord previousRecord = component.FindLocalRecord(eventId);
            string storylineId = component.PendingStorylineId;
            HistoricalStorylineDef chain = null;
            HistoricalEventChainStageDef chainStage = null;
            if (!string.IsNullOrWhiteSpace(storylineId))
            {
                chain = DefDatabase<HistoricalStorylineDef>.GetNamedSilentFail(storylineId);
                HistoricalStorylineRuntimeState savedStory = component.FindStoryline(storylineId);
                int stageIndex = savedStory?.currentStage ?? 0;
                if (chain == null || stageIndex < 0 || stageIndex >= chain.stages.Count)
                    return Reject("The active story chain stage is missing; the choice was not recorded.", out error);
                chainStage = chain.stages[stageIndex];
                if (chainStage.trigger != HistoricalChainStageTrigger.LocalEvent || chainStage.eventDefName != eventId)
                    return Reject("This local event no longer matches the active story stage.", out error);
            }
            string choiceExecutionId = eventId + ":" + (previousRecord?.triggerCount ?? 0) + ":" + choice.choiceId;
            if (previousRecord?.executedChoiceIds?.Contains(choiceExecutionId) == true)
                return Reject("That choice has already been recorded for this event.", out error);
            if (!EvaluateConditions(choice.conditions, choiceContext, out error)) return false;
            if (!HasResourceCosts(map, choice, out error)) return false;
            if (!ApplyLocalOutcome(choice, map, communityId, out error)) return false;

            HistoricalLocalEventRuntimeRecord record = component.GetOrCreateLocalRecord(eventId);
            record.appearances++;
            record.nextEligibleAbsoluteDay = currentDate.AbsoluteDay + item.cooldownDays;
            record.isActive = false;
            if (record.executedChoiceIds == null) record.executedChoiceIds = new List<string>();
            record.executedChoiceIds.Add(choiceExecutionId);
            if (!string.IsNullOrWhiteSpace(storylineId))
            {
                HistoricalStorylineRuntimeState story = component.GetOrCreateStoryline(storylineId);
                story.communityId = communityId;
                if (story.startedAbsoluteDay == long.MinValue) story.startedAbsoluteDay = currentDate.AbsoluteDay;
                if (string.IsNullOrWhiteSpace(story.familyId)) story.familyId = FindCurrentFamilyId(map);
                story.completedStageIds.Add(chainStage.stageId);
                story.selectedChoiceIds.Add(choiceExecutionId);
                if (choice.terminateStoryline)
                {
                    story.ended = true;
                    story.cancelled = true;
                    story.outcome = "TerminatedByPlayerChoice:" + choice.choiceId;
                }
                else
                {
                    AdvanceStoryState(story, chain, chainStage, currentDate);
                }
            }
            component.SetPendingLocalEvent(null, null);
            component.SetNextLocalEventDay(currentDate.AbsoluteDay + 45);
            if (!HistoricalChronicleManager.TryAddInteractiveLocalEventEntry(item.historicalEvent,
                "LocalEvent:" + eventId + ":" + record.appearances, choice.label, choice.description, out string chronicleError))
                Log.Error("[Historical RimWorld] Local event outcome was applied, but chronicle entry failed: " + chronicleError);
            Messages.Message(Translator.Translate(choice.description), MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public bool CanResolveLocalEventChoice(string eventId, int choiceIndex, out string error)
        {
            error = null;
            if (component.PendingLocalEventId != eventId) return Reject("This event is no longer pending.", out error);
            HistoricalLocalEventDef item = localEvents.FirstOrDefault(candidate => candidate.defName == eventId);
            if (item?.choices == null || choiceIndex < 0 || choiceIndex >= item.choices.Count) return Reject("The choice is unavailable.", out error);
            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date)) return Reject("The historical calendar is inactive.", out error);
            Map map = Find.CurrentMap;
            if (map == null || !map.IsPlayerHome) return Reject("Open a player home map to resolve the event.", out error);
            HistoricalLocalEventChoice choice = item.choices[choiceIndex];
            if (!EvaluateConditions(choice.conditions, CreateLocalContext(item, date, map), out error)) return false;
            if (!HasResourceCosts(map, choice, out error)) return false;
            if (choice.outcome == HistoricalLocalEventOutcome.SpendResource
                && (choice.resource == null || map.listerThings.ThingsOfDef(choice.resource).Sum(thing => thing.stackCount) < choice.amount))
                return Reject("Insufficient stock: " + choice.amount + " × " + (choice.resource?.LabelCap.ToString() ?? "resource") + ".", out error);
            if (choice.outcome == HistoricalLocalEventOutcome.CreateRelic
                && HistoricalRelicManager.Records.Any(record => record.RelicDefName == choice.relicDefName && !record.IsDestroyed))
                return Reject("The unique relic has already been recorded.", out error);
            if (choice.outcome == HistoricalLocalEventOutcome.AdoptTechnology
                && !HistoricalTechnologyManager.CanStudy(HistoricalTechnologyManager.GetPlayerCommunityId(), choice.technologyId))
                return Reject("The technology is not locally accessible or its prerequisites are unmet.", out error);
            if (choice.outcome == HistoricalLocalEventOutcome.RevealHistoricalEvent
                && !HistoricalEventManager.WasExecuted(choice.historicalEventId))
                return Reject("This history has not happened yet and cannot be revealed.", out error);
            if (choice.outcome == HistoricalLocalEventOutcome.CreateMarriage
                && !HistoricalLocalEventEligibility.HasMarriageCandidate(map, choice.minimumMarriageAge))
                return Reject("No two eligible adult, unpartnered, non-close-relative colonists are available.", out error);
            if (choice.outcome == HistoricalLocalEventOutcome.InheritRelic && FindRelicHeir(choice.relicDefName) == null)
                return Reject("No living descendant is recorded as eligible to inherit this relic.", out error);
            error = null;
            return true;
        }

        private static bool ApplyLocalOutcome(HistoricalLocalEventChoice choice, Map map, string communityId, out string error)
        {
            error = null;
            if (choice.outcome == HistoricalLocalEventOutcome.NoMaterialChange) return true;
            if (choice.outcome == HistoricalLocalEventOutcome.ChangeCommunityPosture)
                return HistoricalPoliticsManager.TrySetCommunityPosture(communityId, choice.posture,
                    HistoricalPoliticalChangeSource.PlayerDecision, out error);
            if (choice.outcome == HistoricalLocalEventOutcome.AddColonist)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 8);
                GenSpawn.Spawn(pawn, cell, map);
                return true;
            }
            if (choice.outcome == HistoricalLocalEventOutcome.RevealRegion)
                return HistoricalWorldRevealManager.TryRevealRegion(choice.regionId, HistoricalRevealSource.TravellerReport, out error);
            if (choice.outcome == HistoricalLocalEventOutcome.IncreaseCommunityCulturePresence)
                return HistoricalCultureManager.TryIncreaseCommunityPresence(communityId, choice.cultureId, choice.cultureAmount, out error);
            if (choice.outcome == HistoricalLocalEventOutcome.AdoptTechnology)
                return HistoricalTechnologyManager.TryAdoptTechnology(communityId, choice.technologyId,
                    HistoricalTechnologyAcquisitionSource.ArtisanMigration, out error);
            if (choice.outcome == HistoricalLocalEventOutcome.RevealHistoricalEvent)
                return HistoricalChronicleManager.TryRevealEventToPlayer(choice.historicalEventId, out error);
            if (choice.outcome == HistoricalLocalEventOutcome.CreateRelic)
            {
                HistoricalRelicDef relicDef = DefDatabase<HistoricalRelicDef>.GetNamedSilentFail(choice.relicDefName);
                if (relicDef?.baseThingDef == null) return Reject("The configured relic item is unavailable.", out error);
                if (HistoricalRelicManager.Records.Any(item => item.RelicDefName == choice.relicDefName && !item.IsDestroyed))
                    return Reject("This relic has already been recorded; a duplicate cannot be created.", out error);
                Thing relic = ThingMaker.MakeThing(relicDef.baseThingDef);
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 8);
                if (!GenPlace.TryPlaceThing(relic, cell, map, ThingPlaceMode.Near, out Thing placed))
                {
                    if (!relic.Destroyed) relic.Destroy(DestroyMode.Vanish);
                    return Reject("The relic could not be placed on the map.", out error);
                }
                Pawn ownerPawn = map.mapPawns.FreeColonists.OrderBy(pawn => pawn.thingIDNumber).FirstOrDefault();
                string ownerId = ownerPawn != null && HistoricalGenealogyManager.TryGetPersonId(ownerPawn, out string personId) ? personId : null;
                if (HistoricalRelicManager.TryDesignateAsRelic(placed, choice.relicDefName, ownerId, ownerId, out _, out error))
                    return ConsumeResourceCosts(map, choice, out error);
                placed.Destroy(DestroyMode.Vanish);
                return false;
            }
            if (choice.outcome == HistoricalLocalEventOutcome.CreateMarriage)
                return TryCreateMarriage(map, choice.minimumMarriageAge, out error);
            if (choice.outcome == HistoricalLocalEventOutcome.InheritRelic)
            {
                HistoricalRelicRecord relic = HistoricalRelicManager.Records.FirstOrDefault(record => record.RelicDefName == choice.relicDefName
                    && !record.IsLost && !record.IsDestroyed);
                HistoricalPersonRecord heir = FindRelicHeir(choice.relicDefName);
                if (relic == null || heir == null) return Reject("The relic or an eligible living descendant is unavailable.", out error);
                return HistoricalRelicManager.TryTransfer(relic.RelicId, heir.PersonId, HistoricalRelicEventType.Inherited, out error);
            }
            if (choice.outcome == HistoricalLocalEventOutcome.TradeResource)
            {
                List<Thing> placedGoods = new List<Thing>();
                if (!PlaceTradeOutput(choice.resource, choice.amount, map, placedGoods, out error)
                    || (choice.secondaryResource != null && !PlaceTradeOutput(choice.secondaryResource, choice.secondaryAmount, map, placedGoods, out error)))
                {
                    foreach (Thing prior in placedGoods) if (!prior.Destroyed) prior.Destroy(DestroyMode.Vanish);
                    return Reject("The trade goods could not be placed; no materials were charged.", out error);
                }
                if (ConsumeResourceCosts(map, choice, out error)) return true;
                foreach (Thing goods in placedGoods) if (!goods.Destroyed) goods.Destroy(DestroyMode.Vanish);
                return false;
            }
            if (choice.outcome == HistoricalLocalEventOutcome.SpendResource)
            {
                List<Thing> stacks = map.listerThings.ThingsOfDef(choice.resource).Where(thing => !thing.Destroyed)
                    .OrderBy(thing => thing.thingIDNumber).ToList();
                if (stacks.Sum(thing => thing.stackCount) < choice.amount)
                    return Reject("The colony does not have enough " + choice.resource.LabelCap + ".", out error);
                int remaining = choice.amount;
                foreach (Thing stack in stacks)
                {
                    int take = System.Math.Min(remaining, stack.stackCount);
                    Thing consumed = stack.SplitOff(take);
                    consumed.Destroy(DestroyMode.Vanish);
                    remaining -= take;
                    if (remaining == 0) break;
                }
                return true;
            }
            if (choice.outcome == HistoricalLocalEventOutcome.GainResource)
            {
                int remaining = choice.amount;
                while (remaining > 0)
                {
                    int count = System.Math.Min(remaining, choice.resource.stackLimit);
                    Thing goods = ThingMaker.MakeThing(choice.resource);
                    goods.stackCount = count;
                    if (!GenPlace.TryPlaceThing(goods, map.Center, map, ThingPlaceMode.Near, out Thing placed))
                    {
                        if (!goods.Destroyed) goods.Destroy(DestroyMode.Vanish);
                        return Reject("The resource could not be placed on the map.", out error);
                    }
                    remaining -= count;
                }
                return true;
            }
            return Reject("Unsupported local event outcome.", out error);
        }

        private static bool PlaceTradeOutput(ThingDef resource, int amount, Map map, List<Thing> placedGoods, out string error)
        {
            error = null;
            if (resource == null || amount < 1) return Reject("A trade output is not configured correctly.", out error);
            int remaining = amount;
            while (remaining > 0)
            {
                int count = Math.Min(remaining, resource.stackLimit);
                Thing goods = ThingMaker.MakeThing(resource);
                goods.stackCount = count;
                if (!GenPlace.TryPlaceThing(goods, map.Center, map, ThingPlaceMode.Near, out Thing placed))
                {
                    if (!goods.Destroyed) goods.Destroy(DestroyMode.Vanish);
                    return Reject("Could not place the trade output.", out error);
                }
                placedGoods.Add(placed);
                remaining -= count;
            }
            return true;
        }

        private static HistoricalEventContext CreateLocalContext(HistoricalLocalEventDef item, HistoricalDate date, Map map)
        {
            return new HistoricalEventContext(item.scenario, item.historicalEvent, date, date, map,
                HistoricalScenarioProgressManager.ActiveSetup?.startingCommunityName);
        }

        private static bool EvaluateConditions(IEnumerable<HistoricalEventCondition> conditions, HistoricalEventContext context, out string error)
        {
            foreach (HistoricalEventCondition condition in conditions ?? Enumerable.Empty<HistoricalEventCondition>())
            {
                if (condition == null) return Reject("A configured choice condition is missing.", out error);
                if (!condition.Validate(context, out error)) return false;
                if (!condition.Evaluate(context)) return Reject("A choice requirement is not currently satisfied.", out error);
            }
            error = null;
            return true;
        }

        private static bool HasResourceCosts(Map map, HistoricalLocalEventChoice choice, out string error)
        {
            foreach (HistoricalLocalEventResourceCost cost in choice.resourceCosts ?? Enumerable.Empty<HistoricalLocalEventResourceCost>())
            {
                if (cost?.resource == null || cost.amount < 1) return Reject("A configured resource cost is invalid.", out error);
                int stock = map == null ? 0 : map.listerThings.ThingsOfDef(cost.resource)
                    .Where(thing => !thing.Destroyed).Sum(thing => thing.stackCount);
                if (stock < cost.amount) return Reject("Insufficient stock: " + cost.amount + " × " + cost.resource.LabelCap + ".", out error);
            }
            error = null;
            return true;
        }

        private static bool ConsumeResourceCosts(Map map, HistoricalLocalEventChoice choice, out string error)
        {
            if (!HasResourceCosts(map, choice, out error)) return false;
            foreach (HistoricalLocalEventResourceCost cost in choice.resourceCosts ?? Enumerable.Empty<HistoricalLocalEventResourceCost>())
                if (!ConsumeResource(map, cost.resource, cost.amount, out error)) return false;
            error = null;
            return true;
        }

        private static bool ConsumeResource(Map map, ThingDef resource, int amount, out string error)
        {
            List<Thing> stacks = map.listerThings.ThingsOfDef(resource).Where(thing => !thing.Destroyed)
                .OrderBy(thing => thing.thingIDNumber).ToList();
            if (stacks.Sum(thing => thing.stackCount) < amount) return Reject("Insufficient stock: " + resource.LabelCap + ".", out error);
            int remaining = amount;
            foreach (Thing stack in stacks)
            {
                int take = Math.Min(remaining, stack.stackCount);
                Thing consumed = stack.SplitOff(take);
                consumed.Destroy(DestroyMode.Vanish);
                remaining -= take;
                if (remaining == 0) break;
            }
            error = null;
            return true;
        }

        private static bool TryCreateMarriage(Map map, int minimumAge, out string error)
        {
            error = null;
            List<Pawn> adults = map.mapPawns.FreeColonists.Where(pawn => pawn != null && !pawn.Dead && !pawn.Destroyed
                && pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYears >= minimumAge
                && pawn.relations != null && !pawn.relations.DirectRelations.Any(relation => relation.def == PawnRelationDefOf.Spouse
                    || relation.def == PawnRelationDefOf.Lover)).ToList();
            for (int i = 0; i < adults.Count; i++)
            for (int j = i + 1; j < adults.Count; j++)
            {
                Pawn first = adults[i]; Pawn second = adults[j];
                if (HistoricalGenealogyManager.TryGetPersonId(first, out string firstId)
                    && HistoricalGenealogyManager.TryGetPersonId(second, out string secondId)
                    && AreCloseRelatives(firstId, secondId)) continue;
                if (HistoricalGenealogyManager.TryGetPersonId(first, out firstId)
                    && HistoricalGenealogyManager.TryGetPersonId(second, out secondId)
                    && !HistoricalGenealogyManager.TryAddRelation(firstId, secondId, HistoricalKinshipType.Spouse, out error)) return false;
                first.relations.AddDirectRelation(PawnRelationDefOf.Spouse, second);
                return true;
            }
            return Reject("No two adult, unpartnered, non-close-relative colonists are available for this choice.", out error);
        }

        private static HistoricalPersonRecord FindRelicHeir(string relicDefName)
        {
            HistoricalRelicRecord relic = HistoricalRelicManager.Records.FirstOrDefault(item => item.RelicDefName == relicDefName && !item.IsLost && !item.IsDestroyed);
            if (relic == null || string.IsNullOrWhiteSpace(relic.CurrentOwnerPersonId)) return null;
            HashSet<string> descendants = new HashSet<string>(StringComparer.Ordinal) { relic.CurrentOwnerPersonId };
            bool changed;
            do
            {
                changed = false;
                foreach (HistoricalKinshipRelation relation in HistoricalGenealogyManager.Relations.Where(item => item.IsActive
                    && item.Type != HistoricalKinshipType.Spouse && descendants.Contains(item.PersonIdA)))
                    changed |= descendants.Add(relation.PersonIdB);
            } while (changed);
            return descendants.Where(id => id != relic.CurrentOwnerPersonId)
                .Select(HistoricalGenealogyManager.FindPerson).Where(person => person?.IsAlive == true)
                .OrderBy(person => person.BirthDate.AbsoluteDay).FirstOrDefault();
        }

        private static bool AreCloseRelatives(string firstId, string secondId)
        {
            if (string.IsNullOrWhiteSpace(firstId) || string.IsNullOrWhiteSpace(secondId)) return false;
            var firstParents = HistoricalGenealogyManager.Relations.Where(item => item.IsActive && item.Type != HistoricalKinshipType.Spouse
                && item.PersonIdB == firstId).Select(item => item.PersonIdA).ToHashSet(StringComparer.Ordinal);
            var secondParents = HistoricalGenealogyManager.Relations.Where(item => item.IsActive && item.Type != HistoricalKinshipType.Spouse
                && item.PersonIdB == secondId).Select(item => item.PersonIdA).ToHashSet(StringComparer.Ordinal);
            return firstParents.Contains(secondId) || secondParents.Contains(firstId) || firstParents.Overlaps(secondParents);
        }

        private static bool Reject(string message, out string error) { error = message; return false; }

        private void ProcessEvent(HistoricalEventDef item, HistoricalEventRuntimeState state, HistoricalDate currentDate)
        {
            long day = currentDate.AbsoluteDay;
            state.RecordAttempt(day);
            if (item.activationMode == HistoricalEventActivationMode.DevelopmentOnly && !Prefs.DevMode)
            {
                component.RecordPendingAttempt(state, day, HistoricalEventFailureKind.WaitingForPrerequisite,
                    "Waiting for developer mode; development-only event was not executed.");
                return;
            }

            HistoricalEventContext context = new HistoricalEventContext(scenario, item, currentDate,
                HistoricalEventDateResolver.ResolveScheduledDate(item.historicalDate));
            foreach (string prerequisiteId in item.prerequisiteEventIds ?? Enumerable.Empty<string>())
            {
                HistoricalEventRuntimeState prerequisite = component.FindState(prerequisiteId);
                if (prerequisite == null)
                {
                    Fail(state, HistoricalEventFailureKind.MissingDefinition, "Missing prerequisite runtime state " + prerequisiteId + ".", currentDate);
                    return;
                }
                if (prerequisite.Status == HistoricalEventExecutionStatus.Failed || prerequisite.Status == HistoricalEventExecutionStatus.Skipped)
                {
                    if (prerequisite.Status == HistoricalEventExecutionStatus.Skipped
                        && DefDatabase<HistoricalEventDef>.GetNamedSilentFail(prerequisiteId)?.activationMode == HistoricalEventActivationMode.InitialBackground) continue;
                    Fail(state, HistoricalEventFailureKind.MissingDefinition,
                        "Prerequisite " + prerequisiteId + " did not execute successfully (" + prerequisite.Status + ").", currentDate);
                    return;
                }
                if (prerequisite.Status != HistoricalEventExecutionStatus.Executed)
                {
                    component.RecordPendingAttempt(state, day, HistoricalEventFailureKind.WaitingForPrerequisite,
                        "Waiting for prerequisite " + prerequisiteId + ".");
                    return;
                }
            }

            foreach (HistoricalEventCondition condition in item.conditions ?? new List<HistoricalEventCondition>())
            {
                if (condition == null)
                {
                    Fail(state, HistoricalEventFailureKind.InvalidAction, "Event contains a null condition.", currentDate);
                    return;
                }
                if (!condition.Validate(context, out string error))
                {
                    Fail(state, HistoricalEventFailureKind.MissingDefinition, error, currentDate);
                    return;
                }
                if (!condition.Evaluate(context))
                {
                    component.RecordPendingAttempt(state, day, HistoricalEventFailureKind.ConditionNotMet,
                        "A temporary condition is not satisfied; it will be checked again on the next historical day.");
                    return;
                }
            }

            List<HistoricalEventAction> actions = (item.actions ?? new List<HistoricalEventAction>()).ToList();
            foreach (HistoricalEventAction action in actions)
            {
                if (action == null)
                {
                    Fail(state, HistoricalEventFailureKind.InvalidAction, "Event contains a null action.", currentDate);
                    return;
                }
                if (!action.Validate(context, out string actionError))
                {
                    Fail(state, HistoricalEventFailureKind.InvalidAction, actionError, currentDate);
                    return;
                }
            }
            if (actions.Count(action => action.IsStateMutation) > 1)
            {
                Fail(state, HistoricalEventFailureKind.InvalidAction, "More than one state mutation is outside the supported atomicity boundary.", currentDate);
                return;
            }

            bool actionEffectCommitted = false;
            try
            {
                if (!HistoricalChronicleManager.IsActive)
                {
                    Fail(state, HistoricalEventFailureKind.MissingDefinition, "Historical Chronicle component is unavailable.", currentDate);
                    return;
                }
                HistoricalEventAction mutation = actions.FirstOrDefault(action => action.IsStateMutation);
                if (mutation != null && !mutation.Execute(context, out string mutationError))
                {
                    Fail(state, HistoricalEventFailureKind.InvalidAction, mutationError, currentDate);
                    return;
                }
                if (mutation != null) actionEffectCommitted = true;

                foreach (HistoricalEventAction action in actions.Where(action => !action.IsStateMutation))
                {
                    if (!action.Execute(context, out string followupError))
                    {
                        Fail(state, HistoricalEventFailureKind.InvalidAction, followupError, currentDate);
                        return;
                    }
                    actionEffectCommitted = true;
                }

                bool visible = HistoricalEventManager.IsVisible(item);
                if (item.recordInChronicle && !actions.Any(action => action is Action_AddChronicleEntry))
                {
                    if (!HistoricalChronicleManager.TryAddEventEntry(item, item.historicalDate, visible, out string chronicleError))
                    {
                        Fail(state, HistoricalEventFailureKind.ExecutionException, chronicleError, currentDate);
                        return;
                    }
                    actionEffectCommitted = true;
                }

                component.CompleteState(state, HistoricalEventExecutionStatus.Executed, HistoricalEventFailureKind.None, null, currentDate);
                PublishNotification(item, visible);
            }
            catch (Exception exception)
            {
                if (actionEffectCommitted)
                {
                    string reason = "A state or chronicle action committed, but follow-up processing failed: " + exception;
                    component.CompleteState(state, HistoricalEventExecutionStatus.Executed, HistoricalEventFailureKind.None, reason, currentDate);
                    Log.Error("[Historical RimWorld] Event " + item.defName + " committed with a follow-up failure: " + exception);
                }
                else Fail(state, HistoricalEventFailureKind.ExecutionException, exception.ToString(), currentDate);
            }
        }

        private void PublishNotification(HistoricalEventDef item, bool visible)
        {
            if (!visible || item.notificationPolicy == HistoricalEventNotificationPolicy.Silent
                || item.notificationPolicy == HistoricalEventNotificationPolicy.ChronicleOnly) return;
            string title = Translator.Translate(item.label);
            string description = Translator.Translate(item.description);
            if (item.notificationPolicy == HistoricalEventNotificationPolicy.Message)
                Messages.Message(title + " — " + description, MessageTypeDefOf.PositiveEvent, false);
            else if (item.notificationPolicy == HistoricalEventNotificationPolicy.Letter && Find.LetterStack != null)
                Find.LetterStack.ReceiveLetter(title, description, LetterDefOf.PositiveEvent);
        }

        private bool PrerequisitesReady(HistoricalEventDef item)
        {
            foreach (string id in item.prerequisiteEventIds ?? Enumerable.Empty<string>())
            {
                HistoricalEventRuntimeState state = component.FindState(id);
                if (state == null) return false;
                if (state.Status == HistoricalEventExecutionStatus.Executed) continue;
                if (state.Status == HistoricalEventExecutionStatus.Skipped
                    && DefDatabase<HistoricalEventDef>.GetNamedSilentFail(id)?.activationMode == HistoricalEventActivationMode.InitialBackground) continue;
                return false;
            }
            return true;
        }

        private bool PrerequisitesSettled(HistoricalEventDef item)
        {
            foreach (string id in item.prerequisiteEventIds ?? Enumerable.Empty<string>())
            {
                HistoricalEventRuntimeState state = component.FindState(id);
                if (state == null || state.Status == HistoricalEventExecutionStatus.Pending) return false;
            }
            return true;
        }

        private void Fail(HistoricalEventRuntimeState state, HistoricalEventFailureKind kind, string reason, HistoricalDate date)
        {
            component.CompleteState(state, HistoricalEventExecutionStatus.Failed, kind, reason, date);
            Log.Error("[Historical RimWorld] Historical event failed: " + state.EventId + ": " + reason);
        }
    }
}
