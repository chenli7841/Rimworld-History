namespace HistoricalRimWorld.Events
{
    public abstract class HistoricalEventCondition
    {
        public virtual bool ValidateData(out string error) { error = null; return true; }
        public abstract bool Validate(HistoricalEventContext context, out string error);
        public abstract bool Evaluate(HistoricalEventContext context);
    }
}
