using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Research
{
    public enum HR_ResearchCategory
    {
        General,
        Agriculture,
        AnimalHusbandry,
        Construction,
        Crafting,
        Metallurgy,
        Medicine,
        Military,
        History,
        Administration
    }

    public enum HR_ResearchFacilityType
    {
        ArtisanWorkbench,
        WritingDesk
    }

    public sealed class HR_ResearchSkillWeight
    {
        public SkillDef skill;
        public float weight;
    }

    public sealed class HR_ResearchProjectExtension : DefModExtension
    {
        public HR_ResearchCategory category = HR_ResearchCategory.General;
        public List<HR_ResearchFacilityType> allowedFacilities = new List<HR_ResearchFacilityType>();
        public List<HR_ResearchSkillWeight> skillWeights = new List<HR_ResearchSkillWeight>();
    }

    public sealed class HR_ResearchFacilityExtension : DefModExtension
    {
        public HR_ResearchFacilityType facilityType;
        public float researchSpeedMultiplier = 1f;
        public bool historicalScenarioOnly = true;
    }
}
