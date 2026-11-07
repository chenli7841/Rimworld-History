namespace HistoricalRimWorld.Technology
{
    public enum HistoricalTechnologyCategory { Agriculture, AnimalHusbandry, Metallurgy, Pottery, Woodworking, Textiles, Architecture, Military, Medicine, Writing, Transportation, WaterManagement, Commerce, Other }
    public enum HistoricalTechnologyAvailability { NotYetHistoricallyAvailable, ExistsElsewhere, LocallyAccessible }
    public enum HistoricalTechnologyAdoption { Unknown, Known, UnderStudy, Adopted }
    public enum HistoricalTechnologyAcquisitionSource { Research, Trade, ArtisanMigration, HistoricalEvent, CaptiveKnowledge, CulturalExchange, Development }
    public enum HistoricalContentPolicy { Allowed, Restricted, Forbidden }
    public enum HistoricalResearchMode { Basic, Historical }
    public enum HistoricalKnowledgeRequirement { None, Blueprint, Special }
    public enum HistoricalBlueprintStatus { NotOwned, OwnedButNotDecoded, Decoded }
    public enum HistoricalResearchAuthorizationSource { BlueprintDecoded, ScenarioStart, SpecialEvent }
    public enum HistoricalWorldTechStatus { NotEmerged, Emerged, Spreading, Widespread }
    public enum FactionTechKnowledgeLevel { Unknown, HeardOf, Acquired, Mastered }
    public enum PlayerHistoricalTechAwareness { Unknown, Rumored, Confirmed }
}
