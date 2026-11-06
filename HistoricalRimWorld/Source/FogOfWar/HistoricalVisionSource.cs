using Verse;

namespace HistoricalRimWorld.FogOfWar
{
    /// <summary>Extension point for later observers such as towers or scout units.</summary>
    public interface IHRVisionSource
    {
        IntVec3 Position { get; }
        float GetVisionRadius(Map map);
        bool IsActive(Map map);
    }

    /// <summary>Reserved extension point for later illumination, separate from observer sight.</summary>
    public interface IHRIlluminationSource
    {
        IntVec3 Position { get; }
        float Radius { get; }
        bool IsActive(Map map);
    }
}
