namespace HistoricalRimWorld.Events
{
    public abstract class HistoricalEventAction
    {
        public virtual bool IsStateMutation => true;
        public virtual bool ValidateData(out string error) { error = null; return true; }
        public abstract bool Validate(HistoricalEventContext context, out string error);
        public abstract bool Execute(HistoricalEventContext context, out string error);
    }
}
