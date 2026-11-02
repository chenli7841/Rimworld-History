using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Verse;

namespace HistoricalRimWorld.WorldFeatures
{
    [XmlRoot("HistoricalWorldFeatureOverrides")]
    public sealed class HistoricalWorldFeatureOverrideFile
    {
        [XmlAttribute("schemaVersion")] public int schemaVersion = 1;
        [XmlAttribute("scenarioId")] public string scenarioId;
        [XmlElement("Feature")] public List<HistoricalWorldFeatureNameRecord> features = new List<HistoricalWorldFeatureNameRecord>();
    }

    public static class HistoricalWorldFeatureOverrideFileStore
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(HistoricalWorldFeatureOverrideFile));

        public static string GetPath(string scenarioId)
        {
            string safe = new string((scenarioId ?? string.Empty)
                .Where(character => char.IsLetterOrDigit(character) || character == '_' || character == '-').ToArray());
            if (string.IsNullOrWhiteSpace(safe)) throw new ArgumentException("Scenario ID is invalid.", nameof(scenarioId));
            return Path.Combine(GenFilePaths.ConfigFolderPath, "HistoricalRimWorld", "WorldFeatures", safe + ".xml");
        }

        public static bool TryLoad(HistoricalWorldFeatureOverrideWorldComponent component, out string error)
        {
            error = null;
            if (component == null || string.IsNullOrWhiteSpace(component.ScenarioId))
            { error = "Feature overrides are not initialized for a scenario."; return false; }
            try
            {
                string path = GetPath(component.ScenarioId);
                if (!File.Exists(path)) return true;
                XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                HistoricalWorldFeatureOverrideFile file;
                using (FileStream stream = File.OpenRead(path))
                using (XmlReader reader = XmlReader.Create(stream, settings))
                    file = (HistoricalWorldFeatureOverrideFile)Serializer.Deserialize(reader);
                if (file == null || file.schemaVersion != 1 || file.scenarioId != component.ScenarioId)
                    throw new InvalidDataException("Override XML version or scenario ID does not match.");
                component.ReplaceManualOverrides(file.features);
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        public static bool TrySave(HistoricalWorldFeatureOverrideWorldComponent component,
            out string path, out string error)
        {
            path = null; error = null;
            if (component == null || string.IsNullOrWhiteSpace(component.ScenarioId))
            { error = "Feature overrides are not initialized for a scenario."; return false; }
            string temp = null;
            try
            {
                path = GetPath(component.ScenarioId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                HistoricalWorldFeatureOverrideFile file = new HistoricalWorldFeatureOverrideFile
                { scenarioId = component.ScenarioId, features = component.ManualOverrides.Select(item => item.Clone()).ToList() };
                XmlWriterSettings settings = new XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) };
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (XmlWriter writer = XmlWriter.Create(stream, settings)) Serializer.Serialize(writer, file);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception exception) { error = "Could not save native World Feature overrides: " + exception.Message; return false; }
            finally { if (temp != null && File.Exists(temp)) try { File.Delete(temp); } catch { } }
        }
    }
}
