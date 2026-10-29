using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Legacy;
using HistoricalRimWorld.Legacy.Import;
using HistoricalRimWorld.Legacy.Simulation;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_HistoricalLegacy : MainTabWindow
    {
        private Vector2 archiveScroll;
        private Vector2 familyScroll;
        private Vector2 candidateScroll;
        private Vector2 relicScroll;
        private string archiveName = "HistoricalLegacy";
        private string seedText = "1";
        private string selectedFile;
        private string sourceFamilyId;
        private string selectedFamilyId;
        private string status;
        private HistoricalLegacyArchive archive;
        private LegacySimulationResult simulation;
        private readonly HashSet<string> selectedPeople = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> selectedRelics = new HashSet<string>(StringComparer.Ordinal);
        private List<LegacyPersonData> familyPreviewPeople = new List<LegacyPersonData>();
        private List<LegacyKinshipData> familyPreviewRelations = new List<LegacyKinshipData>();
        private Dictionary<string, LegacyPersonData> familyPreviewById = new Dictionary<string, LegacyPersonData>(StringComparer.Ordinal);

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), Translate("HR_Legacy_Title"));
            if (!HistoricalScenarioManager.IsScenarioActive || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate now))
            {
                Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 40f), Translate("HR_Legacy_Inactive"));
                return;
            }

            float top = inRect.y + 36f;
            float colGap = 8f;
            float leftW = inRect.width * 0.25f;
            float middleW = inRect.width * 0.25f;
            Rect left = new Rect(inRect.x, top, leftW, inRect.height - 40f);
            Rect middle = new Rect(left.xMax + colGap, top, middleW, inRect.height - 40f);
            Rect right = new Rect(middle.xMax + colGap, top, inRect.xMax - middle.xMax - colGap, inRect.height - 40f);
            DrawArchives(left);
            DrawFamilies(middle);
            DrawDetails(right, now);
        }

        private void DrawArchives(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 26f), Translate("HR_Legacy_Archives"));
            List<string> files = HistoricalLegacyStorage.ListArchives().ToList();
            Rect list = new Rect(rect.x, rect.y + 28f, rect.width, rect.height * 0.52f);
            float contentHeight = Math.Max(list.height, files.Count * 28f + 8f);
            Widgets.BeginScrollView(list, ref archiveScroll, new Rect(0f, 0f, list.width - 16f, contentHeight));
            float y = 4f;
            foreach (string file in files)
            {
                if (Widgets.ButtonText(new Rect(0f, y, list.width - 22f, 26f), file)) LoadArchive(file);
                y += 28f;
            }
            Widgets.EndScrollView();

            float exportY = list.yMax + 8f;
            Widgets.Label(new Rect(rect.x, exportY, rect.width, 26f), Translate("HR_Legacy_Export"));
            archiveName = Widgets.TextField(new Rect(rect.x, exportY + 27f, rect.width, 26f), archiveName);
            List<HistoricalFamilyRecord> sourceFamilies = HistoricalGenealogyManager.Families
                .Where(item => item != null).OrderBy(item => item.FamilyName, StringComparer.Ordinal).ToList();
            if (sourceFamilies.Count > 0)
            {
                if (sourceFamilyId == null || sourceFamilies.All(item => item.FamilyId != sourceFamilyId)) sourceFamilyId = sourceFamilies[0].FamilyId;
                HistoricalFamilyRecord source = sourceFamilies.FirstOrDefault(item => item.FamilyId == sourceFamilyId);
                if (Widgets.ButtonText(new Rect(rect.x, exportY + 58f, rect.width, 30f), Translate("HR_Legacy_ExportFamily") + ": " + (source?.FamilyName ?? "")))
                {
                    int index = sourceFamilies.FindIndex(item => item.FamilyId == sourceFamilyId);
                    sourceFamilyId = sourceFamilies[(index + 1) % sourceFamilies.Count].FamilyId;
                }
                if (Widgets.ButtonText(new Rect(rect.x, exportY + 92f, rect.width, 30f), Translate("HR_Legacy_ExportSelectedFamily")))
                    ExportFamily();
            }
            if (Widgets.ButtonText(new Rect(rect.x, exportY + 126f, rect.width, 30f), Translate("HR_Legacy_ExportAll"))) ExportAll();
            if (!string.IsNullOrEmpty(status)) Widgets.Label(new Rect(rect.x, exportY + 162f, rect.width, rect.height - (exportY - rect.y) - 162f), status);
        }

        private void DrawFamilies(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 26f), Translate("HR_Legacy_Families"));
            List<LegacyFamilyData> families = archive?.Families?.OrderBy(item => item.FamilyName, StringComparer.Ordinal).ToList()
                ?? new List<LegacyFamilyData>();
            if (selectedFamilyId == null || families.All(item => item.FamilyId != selectedFamilyId)) selectedFamilyId = families.FirstOrDefault()?.FamilyId;
            float contentHeight = Math.Max(rect.height - 32f, families.Count * 30f + familyPreviewPeople.Count * 22f
                + familyPreviewRelations.Count * 22f + 68f);
            Rect viewport = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
            Widgets.BeginScrollView(viewport, ref familyScroll, new Rect(0f, 0f, viewport.width - 16f, contentHeight));
            float y = 4f;
            foreach (LegacyFamilyData family in families)
            {
                string label = (family.FamilyName ?? family.FamilyId) + " (" + archive.Persons.Count(item => item.FamilyId == family.FamilyId) + ")";
                if (Widgets.ButtonText(new Rect(0f, y, viewport.width - 22f, 28f), label + (selectedFamilyId == family.FamilyId ? "  ✓" : "")))
                { selectedFamilyId = family.FamilyId; simulation = null; selectedPeople.Clear(); selectedRelics.Clear(); RefreshFamilyPreview(); }
                y += 30f;
            }
            y += 5f;
            Widgets.Label(new Rect(0f, y, viewport.width - 22f, 22f), Translate("HR_Legacy_FamilyMembers"));
            y += 23f;
            foreach (LegacyPersonData person in familyPreviewPeople)
            {
                string state = person.IsAliveAtExport ? Translate("HR_Legacy_Alive") : Translate("HR_Legacy_Deceased");
                Widgets.Label(new Rect(2f, y, viewport.width - 26f, 21f), (person.DisplayName ?? person.PersonId)
                    + " · " + person.BirthAstronomicalYear + " · " + state);
                y += 22f;
            }
            Widgets.Label(new Rect(0f, y, viewport.width - 22f, 22f), Translate("HR_Legacy_Relationships"));
            y += 23f;
            foreach (LegacyKinshipData relation in familyPreviewRelations)
            {
                string a = familyPreviewById.TryGetValue(relation.PersonIdA, out LegacyPersonData personA) ? personA.DisplayName : relation.PersonIdA;
                string b = familyPreviewById.TryGetValue(relation.PersonIdB, out LegacyPersonData personB) ? personB.DisplayName : relation.PersonIdB;
                string type = Translate("HR_Legacy_Relation_" + relation.Type);
                if (relation.Type == HistoricalKinshipType.Spouse)
                    Widgets.Label(new Rect(2f, y, viewport.width - 26f, 21f), a + " ↔ " + type + " ↔ " + b);
                else Widgets.Label(new Rect(2f, y, viewport.width - 26f, 21f), a + " → " + type + " → " + b);
                y += 22f;
            }
            Widgets.EndScrollView();
        }

        private void DrawDetails(Rect rect, HistoricalDate now)
        {
            float y = rect.y;
            if (archive == null)
            {
                Widgets.Label(new Rect(rect.x, y, rect.width, 55f), Translate("HR_Legacy_SelectArchive"));
                return;
            }
            HistoricalDate sourceEnd = new HistoricalDate(archive.SourceEndAstronomicalYear, archive.SourceEndMonth, archive.SourceEndDay);
            Widgets.Label(new Rect(rect.x, y, rect.width, 40f), string.Format(Translate("HR_Legacy_SourceSummary"),
                archive.ArchiveName, archive.SchemaVersion, archive.SourceScenarioId, sourceEnd.ToString(),
                archive.Families.Count, archive.Persons.Count, archive.Relics.Count, archive.Migrations.Count));
            y += 41f;
            if (selectedFamilyId != null)
            {
                HashSet<string> familyPeople = new HashSet<string>(archive.Persons.Where(item => item.FamilyId == selectedFamilyId).Select(item => item.PersonId), StringComparer.Ordinal);
                int deceased = archive.Persons.Count(item => familyPeople.Contains(item.PersonId) && !item.IsAliveAtExport);
                int relations = archive.Kinships.Count(item => familyPeople.Contains(item.PersonIdA) || familyPeople.Contains(item.PersonIdB));
                Widgets.Label(new Rect(rect.x, y, rect.width, 34f), string.Format(Translate("HR_Legacy_GenealogySummary"), familyPeople.Count, deceased, relations));
                y += 34f;
            }
            Widgets.Label(new Rect(rect.x, y, rect.width * 0.48f, 24f), string.Format(Translate("HR_Legacy_TargetDate"), now.ToString()));
            seedText = Widgets.TextField(new Rect(rect.x + rect.width * 0.52f, y, 60f, 24f), seedText);
            if (Widgets.ButtonText(new Rect(rect.x + rect.width * 0.52f + 66f, y, rect.width * 0.48f - 66f, 24f), Translate("HR_Legacy_Preview"))) RunPreview(now);
            y += 28f;
            if (now < sourceEnd) Widgets.Label(new Rect(rect.x, y, rect.width, 22f), Translate("HR_Legacy_TimeReversal"));
            else
            {
                long elapsedDays = HistoricalLegacyTimeUtility.GetElapsedHistoricalDays(sourceEnd, now);
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f), string.Format(Translate("HR_Legacy_Interval"),
                    elapsedDays, elapsedDays / (float)HistoricalCalendarConstants.DaysPerYear));
            }
            y += 22f;
            if (simulation == null)
            {
                Widgets.Label(new Rect(rect.x, y, rect.width, 46f), Translate("HR_Legacy_PreviewRequired"));
                y += 48f;
            }
            else
            {
                Widgets.Label(new Rect(rect.x, y, rect.width, 42f), string.Format(Translate("HR_Legacy_PreviewSummary"),
                    simulation.GeneratedPersons.Count, simulation.Candidates.Count(item => item.IsEligibleForStartingPawn),
                    simulation.GeneratedChronicle.Count, simulation.SimulationSeed));
                y += 44f;
                int attested = archive.Chronicle.Count(item => item.Origin == LegacyChronicleOrigin.AttestedWorldHistory);
                int recorded = archive.Chronicle.Count(item => item.Origin == LegacyChronicleOrigin.RecordedGameplay);
                int simulated = archive.Chronicle.Count(item => item.Origin == LegacyChronicleOrigin.SimulatedInterlude);
                Widgets.Label(new Rect(rect.x, y, rect.width, 24f), string.Format(Translate("HR_Legacy_ChronicleSummary"), attested, recorded, simulated));
                y += 24f;
                List<LegacyDescendantCandidate> candidates = simulation.Candidates.Where(item => item.FamilyId == selectedFamilyId)
                    .OrderBy(item => item.DisplayName, StringComparer.Ordinal).ToList();
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f), Translate("HR_Legacy_Candidates") + " (" + selectedPeople.Count + "/" + HistoricalLegacyImporter.MaximumStartingPawns + ")");
                y += 22f;
                float candidateViewportHeight = Math.Min(155f, Math.Max(60f, rect.yMax - y - 130f));
                Rect candidateRect = new Rect(rect.x, y, rect.width, candidateViewportHeight);
                float candidateHeight = Math.Max(candidateRect.height, candidates.Count * 25f + 4f);
                Widgets.BeginScrollView(candidateRect, ref candidateScroll, new Rect(0f, 0f, candidateRect.width - 16f, candidateHeight));
                float cy = 2f;
                foreach (LegacyDescendantCandidate candidate in candidates)
                {
                    bool isSelected = selectedPeople.Contains(candidate.PersonId);
                    string label = (candidate.IsEligibleForStartingPawn ? (isSelected ? "☑ " : "☐ ") : "— ")
                        + candidate.DisplayName + "  " + candidate.HistoricalAgeYears + "  " + (candidate.PrimaryCultureId ?? "-");
                    if (Widgets.ButtonText(new Rect(0f, cy, candidateRect.width - 22f, 23f), label) && candidate.IsEligibleForStartingPawn)
                    {
                        if (isSelected) selectedPeople.Remove(candidate.PersonId);
                        else if (selectedPeople.Count < HistoricalLegacyImporter.MaximumStartingPawns) selectedPeople.Add(candidate.PersonId);
                    }
                    cy += 25f;
                }
                Widgets.EndScrollView();
                y = candidateRect.yMax + 4f;
                List<LegacyRelicData> relics = archive.Relics.Where(item => !item.IsLost && !item.IsDestroyed
                    && (HistoricalLegacyImporter.IsFamilyInLineage(selectedFamilyId, item.CurrentFamilyId, archive.Families)
                        || selectedPeople.Contains(item.CurrentOwnerPersonId))).ToList();
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f), Translate("HR_Legacy_Relics")); y += 22f;
                Rect relicRect = new Rect(rect.x, y, rect.width, Math.Min(88f, Math.Max(40f, rect.yMax - y - 42f)));
                float relicContentHeight = Math.Max(relicRect.height, relics.Count * 23f + 4f);
                Widgets.BeginScrollView(relicRect, ref relicScroll, new Rect(0f, 0f, relicRect.width - 16f, relicContentHeight));
                float ry = 2f;
                foreach (LegacyRelicData relic in relics)
                {
                    bool checkedRelic = selectedRelics.Contains(relic.RelicId);
                    if (Widgets.ButtonText(new Rect(0f, ry, relicRect.width - 22f, 22f), (checkedRelic ? "☑ " : "☐ ") + (relic.DisplayName ?? relic.RelicDefName)))
                    { if (checkedRelic) selectedRelics.Remove(relic.RelicId); else selectedRelics.Add(relic.RelicId); }
                    ry += 23f;
                }
                Widgets.EndScrollView();
                y = relicRect.yMax + 3f;
                if (Widgets.ButtonText(new Rect(rect.x, y, rect.width, 34f), Translate("HR_Legacy_ConfirmImport"))) ConfirmImport();
            }
        }

        private void LoadArchive(string file)
        {
            selectedFile = file;
            simulation = null;
            selectedPeople.Clear(); selectedRelics.Clear();
            if (!HistoricalLegacyStorage.TryRead(file, out archive, out string error)) { archive = null; status = error; return; }
            selectedFamilyId = archive.Families.FirstOrDefault()?.FamilyId;
            RefreshFamilyPreview();
            seedText = "1";
            status = Translate("HR_Legacy_ArchiveLoaded") + " " + selectedFile;
        }

        private void RunPreview(HistoricalDate now)
        {
            if (!int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed)) { status = Translate("HR_Legacy_InvalidSeed"); return; }
            LegacySimulationOptions options = new LegacySimulationOptions { Seed = seed, TargetScenarioId = HistoricalScenarioManager.ActiveScenarioDefName };
            if (!HistoricalDescendantSimulator.TrySimulate(archive, now, options, out simulation, out string error))
            { simulation = null; status = error; return; }
            selectedPeople.Clear(); selectedRelics.Clear();
            status = Translate("HR_Legacy_PreviewReady");
        }

        private void ConfirmImport()
        {
            if (!HistoricalLegacyImporter.TryCreatePlan(archive, simulation, selectedFamilyId, selectedPeople, selectedRelics,
                out HistoricalLegacyImportPlan plan, out string error)) { status = error; return; }
            if (!HistoricalLegacyImporter.TryCommit(plan, out error)) { status = error; return; }
            status = Translate("HR_Legacy_ImportComplete");
            Messages.Message(status, MessageTypeDefOf.PositiveEvent, false);
        }

        private void ExportAll()
        {
            if (!HistoricalLegacyExporter.TryExport(archiveName, out string path, out string error)) { status = error; return; }
            status = Translate("HR_Legacy_Exported") + " " + path;
        }

        private void RefreshFamilyPreview()
        {
            familyPreviewPeople = new List<LegacyPersonData>();
            familyPreviewRelations = new List<LegacyKinshipData>();
            familyPreviewById = new Dictionary<string, LegacyPersonData>(StringComparer.Ordinal);
            if (archive == null || string.IsNullOrWhiteSpace(selectedFamilyId)) return;
            var byId = archive.Persons.Where(item => item != null).ToDictionary(item => item.PersonId, StringComparer.Ordinal);
            var links = archive.Kinships.Where(item => item != null).GroupBy(item => item.PersonIdA, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(item => item.PersonIdB).ToList(), StringComparer.Ordinal);
            foreach (LegacyKinshipData relation in archive.Kinships.Where(item => item != null))
            {
                if (!links.TryGetValue(relation.PersonIdB, out List<string> reverse)) links[relation.PersonIdB] = reverse = new List<string>();
                reverse.Add(relation.PersonIdA);
            }
            var visited = new HashSet<string>(archive.Persons.Where(item => item.FamilyId == selectedFamilyId).Select(item => item.PersonId), StringComparer.Ordinal);
            var pending = new Queue<string>(visited);
            while (pending.Count > 0 && visited.Count <= HistoricalLegacyArchiveValidator.MaximumPersons)
            {
                string current = pending.Dequeue();
                if (!links.TryGetValue(current, out List<string> related)) continue;
                foreach (string personId in related) if (visited.Add(personId)) pending.Enqueue(personId);
            }
            familyPreviewPeople = visited.Where(byId.ContainsKey).Select(id => byId[id])
                .OrderBy(item => item.BirthAstronomicalYear).ThenBy(item => item.DisplayName, StringComparer.Ordinal).ToList();
            familyPreviewById = familyPreviewPeople.ToDictionary(item => item.PersonId, StringComparer.Ordinal);
            familyPreviewRelations = archive.Kinships.Where(item => item != null && visited.Contains(item.PersonIdA)
                && visited.Contains(item.PersonIdB)).OrderBy(item => item.StartAstronomicalYear)
                .ThenBy(item => item.PersonIdA, StringComparer.Ordinal).ThenBy(item => item.PersonIdB, StringComparer.Ordinal).ToList();
        }

        private void ExportFamily()
        {
            if (string.IsNullOrWhiteSpace(sourceFamilyId)) { status = Translate("HR_Legacy_NoSourceFamily"); return; }
            if (!HistoricalLegacyExporter.TryExportFamily(archiveName, sourceFamilyId, out string path, out string error))
            { status = error; return; }
            status = Translate("HR_Legacy_Exported") + " " + path;
        }

        private static string Translate(string key) => Translator.Translate(key);
    }
}
