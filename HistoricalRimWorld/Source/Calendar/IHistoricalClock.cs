namespace HistoricalRimWorld.Calendar
{
    public interface IHistoricalClock
    {
        HistoricalDate CurrentDate { get; }
        long CurrentAbsoluteDay { get; }
        int TickOfDay { get; }
        HistoricalDate DateAtTick(int gameTick);
        long DaysSince(HistoricalDate date);
    }
}
