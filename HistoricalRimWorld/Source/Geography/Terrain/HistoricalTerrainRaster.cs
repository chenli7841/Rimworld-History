using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace HistoricalRimWorld.Geography.Terrain
{
    /// <summary>Small immutable, compressed, geographic elevation raster used offline.</summary>
    public sealed class HistoricalTerrainRaster
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HRDEM001");
        private readonly float[] elevationMeters;

        public int Width { get; }
        public int Height { get; }
        public double West { get; }
        public double South { get; }
        public double LongitudeStep { get; }
        public double LatitudeStep { get; }
        public float NoData { get; }
        public float MinimumElevationMeters { get; }
        public float MaximumElevationMeters { get; }

        private HistoricalTerrainRaster(int width, int height, double west, double south,
            double longitudeStep, double latitudeStep, float noData, float[] elevations)
        {
            Width = width;
            Height = height;
            West = west;
            South = south;
            LongitudeStep = longitudeStep;
            LatitudeStep = latitudeStep;
            NoData = noData;
            elevationMeters = elevations;
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            foreach (float value in elevations)
            {
                if (!ValidValue(value, noData)) continue;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
            MinimumElevationMeters = min;
            MaximumElevationMeters = max;
        }

        public static HistoricalTerrainRaster Load(string path)
        {
            using (FileStream file = File.OpenRead(path))
            using (DeflateStream deflate = new DeflateStream(file, CompressionMode.Decompress))
            using (BinaryReader reader = new BinaryReader(deflate, Encoding.UTF8))
            {
                byte[] magic = reader.ReadBytes(Magic.Length);
                if (magic.Length != Magic.Length) throw new InvalidDataException("Cache header is truncated.");
                for (int i = 0; i < Magic.Length; i++)
                    if (magic[i] != Magic[i]) throw new InvalidDataException("Unknown HRDEM cache signature.");

                int version = reader.ReadInt32();
                if (version != 1) throw new InvalidDataException("Unsupported HRDEM cache version " + version + ".");
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();
                double west = reader.ReadDouble();
                double south = reader.ReadDouble();
                double longitudeStep = reader.ReadDouble();
                double latitudeStep = reader.ReadDouble();
                float noData = (float)reader.ReadDouble();
                if (width <= 0 || height <= 0 || (long)width * height > 100000000L)
                    throw new InvalidDataException("Raster dimensions are invalid or exceed the safe loading limit.");
                if (longitudeStep <= 0d || latitudeStep <= 0d
                    || west < -180d || south < -90d || west + width * longitudeStep > 180.01d
                    || south + height * latitudeStep > 90.01d)
                    throw new InvalidDataException("Raster geographic bounds are invalid.");

                int count = checked(width * height);
                float[] elevations = new float[count];
                for (int i = 0; i < count; i++) elevations[i] = reader.ReadSingle();
                return new HistoricalTerrainRaster(width, height, west, south,
                    longitudeStep, latitudeStep, noData, elevations);
            }
        }

        public bool Contains(double longitude, double latitude)
        {
            return longitude >= West && longitude <= West + Width * LongitudeStep
                && latitude >= South && latitude <= South + Height * LatitudeStep;
        }

        public bool TrySample(double longitude, double latitude, out float elevation)
        {
            elevation = 0f;
            if (!Contains(longitude, latitude)) return false;
            // Cache samples represent cell averages; sample their centers bilinearly.
            double x = (longitude - West) / LongitudeStep - 0.5d;
            double y = (South + Height * LatitudeStep - latitude) / LatitudeStep - 0.5d;
            int x0 = Clamp((int)Math.Floor(x), 0, Width - 1);
            int y0 = Clamp((int)Math.Floor(y), 0, Height - 1);
            int x1 = Clamp(x0 + 1, 0, Width - 1);
            int y1 = Clamp(y0 + 1, 0, Height - 1);
            double tx = Math.Max(0d, Math.Min(1d, x - Math.Floor(x)));
            double ty = Math.Max(0d, Math.Min(1d, y - Math.Floor(y)));
            float a = elevationMeters[y0 * Width + x0];
            float b = elevationMeters[y0 * Width + x1];
            float c = elevationMeters[y1 * Width + x0];
            float d = elevationMeters[y1 * Width + x1];
            if (!Valid(a) || !Valid(b) || !Valid(c) || !Valid(d))
            {
                int nearestX = Clamp((int)Math.Round(x), 0, Width - 1);
                int nearestY = Clamp((int)Math.Round(y), 0, Height - 1);
                float nearest = elevationMeters[nearestY * Width + nearestX];
                if (!Valid(nearest)) return false;
                elevation = nearest;
                return true;
            }
            double top = a + (b - a) * tx;
            double bottom = c + (d - c) * tx;
            elevation = (float)(top + (bottom - top) * ty);
            return true;
        }

        public bool TryGetSlope(double longitude, double latitude, out float slopeDegrees)
        {
            slopeDegrees = 0f;
            if (!TrySample(longitude - LongitudeStep, latitude, out float west)
                || !TrySample(longitude + LongitudeStep, latitude, out float east)
                || !TrySample(longitude, latitude - LatitudeStep, out float south)
                || !TrySample(longitude, latitude + LatitudeStep, out float north)) return false;

            double latRadians = latitude * Math.PI / 180d;
            double xMeters = 111320d * Math.Max(0.01d, Math.Cos(latRadians)) * LongitudeStep * 2d;
            double yMeters = 111320d * LatitudeStep * 2d;
            double gradientX = (east - west) / xMeters;
            double gradientY = (north - south) / yMeters;
            slopeDegrees = (float)(Math.Atan(Math.Sqrt(gradientX * gradientX + gradientY * gradientY)) * 180d / Math.PI);
            return true;
        }

        private bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value)
            && Math.Abs(value - NoData) > 0.01f;

        private static bool ValidValue(float value, float noData) => !float.IsNaN(value) && !float.IsInfinity(value)
            && Math.Abs(value - noData) > 0.01f;

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
