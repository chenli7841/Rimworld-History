namespace HistoricalRimWorld.Events
{
    public enum HistoricalEventCategory { Political, Military, Diplomatic, Cultural, Economic, Migration, Disaster, Discovery, Other }
    public enum HistoricalEventScope { World, Region, LocalCommunity }
    public enum HistoricalEventActivationMode { Scheduled, InitialBackground, DevelopmentOnly, LocalTemplate }
    public enum HistoricalEventVisibility { Public, RegionKnowledgeRequired, Hidden }
    public enum HistoricalEventNotificationPolicy { Silent, ChronicleOnly, Message, Letter }
    public enum HistoricalEventProvenance { AttestedHistoricalEvent, HistoricalReconstruction, HistoricalContextSimulation, FictionalLocalEvent }
    public enum HistoricalChronologyTradition { TraditionalReceivedChronology, XinianBasedReconstruction }
    public enum HistoricalEventExecutionStatus { Pending, Executed, Skipped, Failed, Template }
    public enum HistoricalEventFailureKind { None, WaitingForPrerequisite, ConditionNotMet, MissingDefinition, InvalidAction, ExecutionException }
}
