using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public struct HistoricalTechnologyNodePosition
    {
        public readonly float X;
        public readonly float Y;
        public readonly bool HasHistoricalDate;
        public readonly int? StartYear;
        public readonly int? EndYear;

        public HistoricalTechnologyNodePosition(float x, float y, bool hasHistoricalDate, int? startYear, int? endYear)
        {
            X = x;
            Y = y;
            HasHistoricalDate = hasHistoricalDate;
            StartYear = startYear;
            EndYear = endYear;
        }
    }

    public static class HistoricalTechnologyTimelineLayout
    {
        private const string LayoutDefName = "HR_TechnologyTimeline_EasternZhou";
        private static HistoricalTechnologyTimelineLayoutDef cachedDef;
        private static Dictionary<ResearchProjectDef, HistoricalTechnologyNodePosition> positions;
        private static int cachedZoomPercent = -1;

        private static HistoricalTechnologyTimelineLayoutDef LayoutDef => cachedDef
            ?? (cachedDef = DefDatabase<HistoricalTechnologyTimelineLayoutDef>.GetNamedSilentFail(LayoutDefName));

        public static void Invalidate()
        {
            positions = null;
            cachedZoomPercent = -1;
        }

        public static int CategoryCount => HistoricalTechnologyCategoryGroups.All.Count;
        public static float UndatedX => LayoutDef?.undatedX ?? 14.5f;

        public static float YearToTimelineX(int astronomicalYear)
        {
            List<HistoricalTimelineSegment> segments = LayoutDef?.segments;
            if (segments == null || segments.Count == 0) return (astronomicalYear + 10000f) / 1000f;

            HistoricalTimelineSegment segment = segments.FirstOrDefault(item => astronomicalYear >= item.startAstronomicalYear
                && astronomicalYear <= item.endAstronomicalYear);
            if (segment == null)
                segment = astronomicalYear < segments[0].startAstronomicalYear ? segments[0] : segments[segments.Count - 1];

            float yearFraction = (astronomicalYear - segment.startAstronomicalYear)
                / (float)(segment.endAstronomicalYear - segment.startAstronomicalYear);
            return segment.xStart + yearFraction * (segment.xEnd - segment.xStart);
        }

        public static int TimelineXToYear(float x)
        {
            List<HistoricalTimelineSegment> segments = LayoutDef?.segments;
            if (segments == null || segments.Count == 0) return (int)Math.Round(x * 1000f - 10000f);

            HistoricalTimelineSegment segment = segments.FirstOrDefault(item => x >= item.xStart && x <= item.xEnd);
            if (segment == null) segment = x < segments[0].xStart ? segments[0] : segments[segments.Count - 1];
            float xFraction = (x - segment.xStart) / (segment.xEnd - segment.xStart);
            return (int)Math.Round(segment.startAstronomicalYear
                + xFraction * (segment.endAstronomicalYear - segment.startAstronomicalYear));
        }

        public static bool TryGetPosition(ResearchProjectDef project, out HistoricalTechnologyNodePosition position)
        {
            EnsureLayout();
            if (positions != null && project != null && positions.TryGetValue(project, out position)) return true;
            position = default(HistoricalTechnologyNodePosition);
            return false;
        }

        public static IEnumerable<ResearchProjectDef> HistoricalProjects
        {
            get
            {
                EnsureLayout();
                return positions == null ? Enumerable.Empty<ResearchProjectDef>() : positions.Keys;
            }
        }

        public static HistoricalTechExtension GetExtension(ResearchProjectDef project)
        {
            if (project == null) return null;
            HistoricalTechExtension extension = project.GetModExtension<HistoricalTechExtension>();
            return extension != null && extension.IsHistoricalTechnology ? extension : null;
        }

        public static HistoricalTechnologyDef GetTechnology(ResearchProjectDef project)
        {
            HistoricalTechExtension extension = GetExtension(project);
            if (extension == null) return null;
            return extension.technology ?? HistoricalTechnologyManager.Technologies
                .FirstOrDefault(technology => technology.researchProject == project);
        }

        public static HistoricalTechnologyCategoryGroup GetCategoryGroup(ResearchProjectDef project)
        {
            HistoricalTechExtension extension = GetExtension(project);
            if (extension != null && !string.IsNullOrWhiteSpace(extension.HistoricalCategory)
                && Enum.TryParse(extension.HistoricalCategory, true, out HistoricalTechnologyCategory parsed))
                return HistoricalTechnologyCategoryGroups.For(parsed);

            return HistoricalTechnologyCategoryGroups.For(GetTechnology(project)?.category ?? HistoricalTechnologyCategory.Other);
        }

        public static int? GetSpreadStartAstronomicalYear(ResearchProjectDef project)
        {
            HistoricalTechExtension extension = GetExtension(project);
            if (extension?.SpreadStartYear != null) return ConvertHistoricalYearToAstronomical(extension.SpreadStartYear.Value);
            if (extension?.EarliestEvidenceYear != null) return ConvertHistoricalYearToAstronomical(extension.EarliestEvidenceYear.Value);
            HistoricalTechnologyDef technology = GetTechnology(project);
            return technology?.earliestPossibleYear != null ? (int?)ConvertHistoricalYearToAstronomical(technology.earliestPossibleYear.Value) : null;
        }

        public static int? GetSpreadEndAstronomicalYear(ResearchProjectDef project)
        {
            HistoricalTechExtension extension = GetExtension(project);
            if (extension?.SpreadEndYear != null) return ConvertHistoricalYearToAstronomical(extension.SpreadEndYear.Value);
            HistoricalTechnologyDef technology = GetTechnology(project);
            return technology?.latestAttestedYear != null ? (int?)ConvertHistoricalYearToAstronomical(technology.latestAttestedYear.Value) : null;
        }

        public static bool IsHistoricallyAvailable(ResearchProjectDef project, int astronomicalYear)
        {
            int? startYear = GetSpreadStartAstronomicalYear(project);
            return !startYear.HasValue || astronomicalYear >= startYear.Value;
        }

        public static string HistoricalDetails(ResearchProjectDef project)
        {
            HistoricalTechnologyDef technology = GetTechnology(project);
            HistoricalTechExtension extension = GetExtension(project);
            if (technology == null || extension == null) return string.Empty;

            List<string> details = new List<string>();
            HistoricalTechnologyNodePosition position;
            if (TryGetPosition(project, out position))
            {
                if (position.StartYear.HasValue && position.EndYear.HasValue)
                    details.Add(string.Format(Translator.Translate("HR_Timeline_DateRange"),
                        FormatYear(position.StartYear.Value), FormatYear(position.EndYear.Value)));
                else if (position.StartYear.HasValue)
                    details.Add(string.Format(Translator.Translate("HR_Timeline_DateFrom"), FormatYear(position.StartYear.Value)));
                else details.Add(Translator.Translate("HR_Timeline_DateUndated"));
            }

            details.Add(string.Format(Translator.Translate("HR_Timeline_Category"),
                Translator.Translate(HistoricalTechnologyCategoryGroups.TranslationKey(GetCategoryGroup(project)))));
            string historicalDescription = !string.IsNullOrWhiteSpace(extension.HistoricalDescription)
                ? extension.HistoricalDescription
                : technology.description;
            if (!string.IsNullOrWhiteSpace(historicalDescription)) details.Add(historicalDescription);
            string evidenceLevel = !string.IsNullOrWhiteSpace(extension.HistoricalEvidenceLevel)
                ? extension.HistoricalEvidenceLevel
                : technology.evidenceLevel.ToString();
            details.Add(string.Format(Translator.Translate("HR_Timeline_EvidenceLevel"),
                Translator.Translate("HR_Timeline_Evidence_" + evidenceLevel)));
            string evidence = !string.IsNullOrWhiteSpace(extension.HistoricalEvidence)
                ? extension.HistoricalEvidence
                : technology.sourceNotes;
            if (!string.IsNullOrWhiteSpace(evidence)) details.Add(string.Format(Translator.Translate("HR_Timeline_EvidenceNotes"), evidence));
            return string.Join("\n\n", details);
        }

        public static int CategoryIndex(HistoricalTechnologyCategoryGroup group) => HistoricalTechnologyCategoryGroups.All.IndexOf(group);

        public static string FormatYear(int astronomicalYear)
        {
            int year;
            bool isBce;
            HistoricalYearConverter.FromAstronomicalYear(astronomicalYear, out year, out isBce);
            return string.Format(Translator.Translate(isBce ? "HR_Timeline_YearBCE" : "HR_Timeline_YearCE"), year);
        }

        public static float Zoom
        {
            get
            {
                float zoom = HistoricalRimWorldMod.CurrentSettings?.HistoricalTimelineZoom ?? 1f;
                return Math.Max(0.5f, Math.Min(2f, zoom));
            }
        }

        public static float ZoomedX(float x) => x * Zoom;

        public static int ConvertHistoricalYearToAstronomical(int year)
        {
            if (year >= 0) return year;
            long traditionalBceYear = -(long)year;
            if (traditionalBceYear > int.MaxValue) return int.MinValue;
            return HistoricalYearConverter.ToAstronomicalYear((int)traditionalBceYear, true);
        }

        private static void EnsureLayout()
        {
            int zoomPercent = (int)Math.Round(Zoom * 100f);
            if (positions != null && cachedZoomPercent == zoomPercent) return;
            cachedZoomPercent = zoomPercent;
            positions = new Dictionary<ResearchProjectDef, HistoricalTechnologyNodePosition>();

            List<ProjectLayoutSeed> seeds = DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Where(project => GetExtension(project) != null && GetTechnology(project) != null)
                .Select(project => new ProjectLayoutSeed(project,
                    GetSpreadStartAstronomicalYear(project), GetSpreadEndAstronomicalYear(project), GetCategoryGroup(project)))
                .OrderBy(seed => seed.Category)
                .ThenBy(seed => seed.StartYear ?? seed.EndYear ?? int.MaxValue)
                .ThenBy(seed => seed.Project.defName)
                .ToList();

            float categoryY = 0f;
            foreach (HistoricalTechnologyCategoryGroup category in HistoricalTechnologyCategoryGroups.All)
            {
                List<float> laneRightEdges = new List<float>();
                foreach (ProjectLayoutSeed seed in seeds.Where(seed => seed.Category == category)
                    .OrderBy(seed => seed.X).ThenBy(seed => seed.Project.defName))
                {
                    int lane = laneRightEdges.FindIndex(rightEdge => seed.X >= rightEdge + (LayoutDef?.minimumNodeGap ?? 1.25f));
                    if (lane < 0)
                    {
                        lane = laneRightEdges.Count;
                        laneRightEdges.Add(float.NegativeInfinity);
                    }
                    laneRightEdges[lane] = seed.X;
                    float y = categoryY + lane * (LayoutDef?.laneSpacing ?? 0.85f);
                    positions[seed.Project] = new HistoricalTechnologyNodePosition(ZoomedX(seed.X), y,
                        seed.StartYear.HasValue || seed.EndYear.HasValue, seed.StartYear, seed.EndYear);
                }
                float laneSpacing = LayoutDef?.laneSpacing ?? 0.85f;
                float minimumCategorySpacing = laneRightEdges.Count * laneSpacing + 0.8f;
                categoryY += Math.Max(LayoutDef?.categorySpacing ?? 2f, minimumCategorySpacing);
            }
        }

        private sealed class ProjectLayoutSeed
        {
            public ResearchProjectDef Project { get; }
            public int? StartYear { get; }
            public int? EndYear { get; }
            public HistoricalTechnologyCategoryGroup Category { get; }
            public float X { get; }

            public ProjectLayoutSeed(ResearchProjectDef project, int? startYear, int? endYear, HistoricalTechnologyCategoryGroup category)
            {
                Project = project;
                StartYear = startYear;
                EndYear = endYear;
                Category = category;
                if (startYear.HasValue && endYear.HasValue)
                    X = YearToTimelineX((int)Math.Round((startYear.Value + (double)endYear.Value) / 2d));
                else if (startYear.HasValue) X = YearToTimelineX(startYear.Value);
                else if (endYear.HasValue) X = YearToTimelineX(endYear.Value);
                else X = LayoutDef?.undatedX ?? 14.5f;
            }
        }
    }

    public enum HistoricalTechnologyCategoryGroup
    {
        Agriculture,
        Metallurgy,
        Military,
        Engineering,
        Crafts,
        Exchange,
        Knowledge,
        Medicine,
        Other,
        All
    }

    public static class HistoricalTechnologyCategoryGroups
    {
        public static readonly List<HistoricalTechnologyCategoryGroup> All = new List<HistoricalTechnologyCategoryGroup>
        {
            HistoricalTechnologyCategoryGroup.Agriculture,
            HistoricalTechnologyCategoryGroup.Metallurgy,
            HistoricalTechnologyCategoryGroup.Military,
            HistoricalTechnologyCategoryGroup.Engineering,
            HistoricalTechnologyCategoryGroup.Crafts,
            HistoricalTechnologyCategoryGroup.Exchange,
            HistoricalTechnologyCategoryGroup.Knowledge,
            HistoricalTechnologyCategoryGroup.Medicine,
            HistoricalTechnologyCategoryGroup.Other
        };

        public static HistoricalTechnologyCategoryGroup For(HistoricalTechnologyCategory category)
        {
            switch (category)
            {
                case HistoricalTechnologyCategory.Agriculture:
                case HistoricalTechnologyCategory.AnimalHusbandry: return HistoricalTechnologyCategoryGroup.Agriculture;
                case HistoricalTechnologyCategory.Metallurgy: return HistoricalTechnologyCategoryGroup.Metallurgy;
                case HistoricalTechnologyCategory.Military: return HistoricalTechnologyCategoryGroup.Military;
                case HistoricalTechnologyCategory.Architecture:
                case HistoricalTechnologyCategory.WaterManagement: return HistoricalTechnologyCategoryGroup.Engineering;
                case HistoricalTechnologyCategory.Pottery:
                case HistoricalTechnologyCategory.Woodworking:
                case HistoricalTechnologyCategory.Textiles: return HistoricalTechnologyCategoryGroup.Crafts;
                case HistoricalTechnologyCategory.Transportation:
                case HistoricalTechnologyCategory.Commerce: return HistoricalTechnologyCategoryGroup.Exchange;
                case HistoricalTechnologyCategory.Writing: return HistoricalTechnologyCategoryGroup.Knowledge;
                case HistoricalTechnologyCategory.Medicine: return HistoricalTechnologyCategoryGroup.Medicine;
                default: return HistoricalTechnologyCategoryGroup.Other;
            }
        }

        public static string TranslationKey(HistoricalTechnologyCategoryGroup group) => "HR_Timeline_Category_" + group;
    }
}
