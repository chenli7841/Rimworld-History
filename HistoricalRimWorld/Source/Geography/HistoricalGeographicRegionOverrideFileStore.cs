using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Verse;

namespace HistoricalRimWorld.Geography
{
    [XmlRoot("HistoricalGeographicRegions")]
    public sealed class HistoricalGeographicRegionOverrideFile
    {
        [XmlAttribute("schemaVersion")] public int SchemaVersion = 1;
        [XmlAttribute("scenarioId")] public string ScenarioId;
        [XmlElement("Region")] public List<HistoricalGeographicRegionData> Regions = new List<HistoricalGeographicRegionData>();
    }

    /// <summary>Versioned scenario overlay for user-authored geographic definitions, outside the Mod's Def files.</summary>
    public static class HistoricalGeographicRegionOverrideFileStore
    {
        private const long MaximumFileBytes = 32L * 1024L * 1024L;
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(HistoricalGeographicRegionOverrideFile));

        public static string GetPath(string scenarioId)
        {
            string safeId = new string((scenarioId ?? string.Empty)
                .Where(character => char.IsLetterOrDigit(character) || character == '_' || character == '-').ToArray());
            if (string.IsNullOrWhiteSpace(safeId)) throw new ArgumentException("Scenario ID is invalid.", nameof(scenarioId));
            return Path.Combine(GenFilePaths.ConfigFolderPath, "HistoricalRimWorld", "GeographicRegions", safeId + ".xml");
        }

        public static bool TryLoad(HistoricalGeographicRegionWorldComponent component, out string error)
        {
            error = null;
            if (component == null || string.IsNullOrWhiteSpace(component.ScenarioId))
            { error = "Geographic region world state is not initialized."; return false; }
            try
            {
                string path = GetPath(component.ScenarioId);
                if (!File.Exists(path)) return true;
                FileInfo info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaximumFileBytes)
                    throw new InvalidDataException("File is empty or exceeds the 32 MB safety limit.");
                HistoricalGeographicRegionOverrideFile file;
                XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (XmlReader reader = XmlReader.Create(stream, settings))
                    file = (HistoricalGeographicRegionOverrideFile)Serializer.Deserialize(reader);
                if (file == null || file.SchemaVersion != 1 || file.ScenarioId != component.ScenarioId)
                    throw new InvalidDataException("File schema or scenario ID does not match.");
                Dictionary<string, HistoricalGeographicRegionData> merged = component.Regions
                    .Where(item => item != null).ToDictionary(item => item.defName, item => item.Clone(), StringComparer.Ordinal);
                foreach (HistoricalGeographicRegionData region in file.Regions ?? new List<HistoricalGeographicRegionData>())
                {
                    if (region == null || string.IsNullOrWhiteSpace(region.defName)) continue;
                    string validation = string.Join(" ", region.Validate());
                    if (!string.IsNullOrWhiteSpace(validation))
                    { Log.Warning("[Historical RimWorld] Ignored geographic region " + region.defName + ": " + validation); continue; }
                    merged[region.defName] = region.Clone();
                }
                component.ReplaceRegions(merged.Values);
                return true;
            }
            catch (Exception exception)
            { error = exception.Message; return false; }
        }

        public static bool TrySave(HistoricalGeographicRegionWorldComponent component, out string path, out string error)
        {
            path = null;
            error = null;
            if (component == null || string.IsNullOrWhiteSpace(component.ScenarioId))
            { error = "Geographic region data is not initialized for a scenario."; return false; }
            List<HistoricalGeographicRegionData> regions = component.Regions
                .Where(item => item != null).OrderBy(item => item.defName, StringComparer.Ordinal).ToList();
            if (regions.Count > 10000) { error = "Geographic region count exceeds the safety limit."; return false; }
            foreach (HistoricalGeographicRegionData region in regions)
            {
                string validation = string.Join(" ", region.Validate());
                if (!string.IsNullOrWhiteSpace(validation))
                { error = "Region " + region.defName + " is invalid: " + validation; return false; }
            }

            string temp = null;
            try
            {
                path = GetPath(component.ScenarioId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                HistoricalGeographicRegionOverrideFile file = new HistoricalGeographicRegionOverrideFile
                { SchemaVersion = 1, ScenarioId = component.ScenarioId, Regions = regions };
                XmlWriterSettings settings = new XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) };
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (XmlWriter writer = XmlWriter.Create(stream, settings))
                    Serializer.Serialize(writer, file);
                ReplaceAtomically(temp, path);
                return true;
            }
            catch (Exception exception)
            { error = "Could not save geographic regions: " + exception.Message; return false; }
            finally
            {
                if (temp != null && File.Exists(temp))
                {
                    try { File.Delete(temp); }
                    catch (Exception exception) { Log.Warning("[Historical RimWorld] Could not delete a temporary geography file: " + exception.Message); }
                }
            }
        }

        private static void ReplaceAtomically(string temp, string target)
        {
            if (!File.Exists(target)) { File.Move(temp, target); return; }
            string backup = target + "." + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".bak";
            try { File.Replace(temp, target, backup, true); }
            catch (PlatformNotSupportedException) { ReplaceByMoves(temp, target, backup); }
            catch (NotSupportedException) { ReplaceByMoves(temp, target, backup); }
        }

        private static void ReplaceByMoves(string temp, string target, string backup)
        {
            File.Move(target, backup);
            try { File.Move(temp, target); }
            catch
            {
                if (!File.Exists(target) && File.Exists(backup)) File.Move(backup, target);
                throw;
            }
        }
    }
}
