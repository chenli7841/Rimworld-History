using System.Globalization;
using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace HistoricalRimWorld.World
{
    /// <summary>Enterable map parent reserved for named historical landmarks.</summary>
    public sealed class HistoricalLandmarkMapParent : MapParent, IHistoricalMapLocation
    {
        private string historicalLabel;
        private string historicalDescription;
        private string historicalLandmarkDefName;
        private double historicalLatitude;
        private double historicalLongitude;
        private double gameLatitude;
        private double gameLongitude;
        private double mapScaleFactor = 1d;
        private bool hasHistoricalCoordinates;
        private Color historicalFactionColor = Color.white;
        private bool hasHistoricalFactionColor;

        public string HistoricalLandmarkDefName => historicalLandmarkDefName;
        public override string Label => string.IsNullOrWhiteSpace(historicalLabel) ? base.Label : historicalLabel;
        public override string LabelShort => Label;
        public override bool HasName => !string.IsNullOrWhiteSpace(historicalLabel) || base.HasName;
        public override Color ExpandingIconColor => hasHistoricalFactionColor ? historicalFactionColor : base.ExpandingIconColor;

        public void SetHistoricalLabel(string label) => historicalLabel = label;
        public void SetHistoricalDescription(string description) => historicalDescription = description;
        public void SetHistoricalLandmark(string landmarkDefName) => historicalLandmarkDefName = landmarkDefName;
        public void SetHistoricalGeoCoordinates(double latitude, double longitude,
            double projectedLatitude, double projectedLongitude, double scaleFactor)
        {
            historicalLatitude = latitude;
            historicalLongitude = longitude;
            gameLatitude = projectedLatitude;
            gameLongitude = projectedLongitude;
            mapScaleFactor = scaleFactor;
            hasHistoricalCoordinates = true;
        }
        public void SetHistoricalFactionColor(Color color)
        {
            historicalFactionColor = color;
            hasHistoricalFactionColor = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref historicalLabel, "historicalLabel");
            Scribe_Values.Look(ref historicalDescription, "historicalDescription");
            Scribe_Values.Look(ref historicalLandmarkDefName, "historicalLandmarkDefName");
            Scribe_Values.Look(ref historicalLatitude, "historicalLatitude");
            Scribe_Values.Look(ref historicalLongitude, "historicalLongitude");
            Scribe_Values.Look(ref gameLatitude, "gameLatitude");
            Scribe_Values.Look(ref gameLongitude, "gameLongitude");
            Scribe_Values.Look(ref mapScaleFactor, "mapScaleFactor", 1d);
            Scribe_Values.Look(ref hasHistoricalCoordinates, "hasHistoricalCoordinates", false);
            Scribe_Values.Look(ref historicalFactionColor, "historicalFactionColor", Color.white);
            Scribe_Values.Look(ref hasHistoricalFactionColor, "hasHistoricalFactionColor", false);
        }

        public override string GetInspectString()
        {
            string baseString = base.GetInspectString();
            string description = BuildHistoricalDescription();
            return string.IsNullOrWhiteSpace(description) ? baseString
                : string.IsNullOrWhiteSpace(baseString) ? description : baseString + "\n" + description;
        }

        public override string GetDescription() => string.IsNullOrWhiteSpace(BuildHistoricalDescription())
            ? base.GetDescription() : BuildHistoricalDescription();

        private string BuildHistoricalDescription()
        {
            if (!hasHistoricalCoordinates) return historicalDescription;
            string coordinates = "历史坐标：" + historicalLatitude.ToString("0.####", CultureInfo.InvariantCulture)
                + "°N, " + historicalLongitude.ToString("0.####", CultureInfo.InvariantCulture) + "°E"
                + "\n游戏球面坐标（非真实经纬度）：" + gameLatitude.ToString("0.####", CultureInfo.InvariantCulture)
                + "°, " + gameLongitude.ToString("0.####", CultureInfo.InvariantCulture) + "°"
                + "\nTile ID：" + (int)Tile
                + "\n历史地图比例尺：" + mapScaleFactor.ToString("0.##", CultureInfo.InvariantCulture) + "×";
            return string.IsNullOrWhiteSpace(historicalDescription) ? coordinates : historicalDescription + "\n" + coordinates;
        }
    }
}
