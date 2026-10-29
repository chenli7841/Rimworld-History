using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using Verse;

namespace HistoricalRimWorld.Legacy
{
    public static class HistoricalLegacyStorage
    {
        public const int MaximumArchiveBytes = 20 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static string ArchiveDirectory => Path.Combine(GenFilePaths.ConfigFolderPath, "HistoricalRimWorld", "LegacyArchives");

        public static IReadOnlyList<string> ListArchives()
        {
            try
            {
                Directory.CreateDirectory(ArchiveDirectory);
                return Directory.GetFiles(ArchiveDirectory, "*.json", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception exception) { Log.Warning("[Historical RimWorld] Cannot list legacy archives: " + exception.Message); return Array.Empty<string>(); }
        }

        public static bool TryResolveArchivePath(string fileName, out string fullPath, out string error)
        {
            fullPath = null; error = null;
            if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName)
                || !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return Fail("Archive filename is invalid.", out error);
            try
            {
                string root = Path.GetFullPath(ArchiveDirectory) + Path.DirectorySeparatorChar;
                string path = Path.GetFullPath(Path.Combine(root, fileName));
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return Fail("Archive path escapes the configured archive directory.", out error);
                fullPath = path; return true;
            }
            catch (Exception exception) { return Fail("Archive path is invalid: " + exception.Message, out error); }
        }

        public static bool TryRead(string fileName, out HistoricalLegacyArchive archive, out string error)
        {
            archive = null; error = null;
            if (!TryResolveArchivePath(fileName, out string path, out error)) return false;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) return Fail("Archive file was not found.", out error);
                if (info.Length <= 0 || info.Length > MaximumArchiveBytes) return Fail("Archive file is empty or exceeds the 20 MB safety limit.", out error);
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    archive = Deserialize(stream);
                if (!HistoricalLegacyMigratorRegistry.TryMigrate(archive, out archive, out error)) return false;
                return HistoricalLegacyArchiveValidator.TryValidate(archive, out error);
            }
            catch (Exception exception) { archive = null; return Fail("Archive JSON could not be read: " + exception.Message, out error); }
        }

        public static bool TryWriteAtomic(string archiveName, HistoricalLegacyArchive archive, out string outputPath, out string error)
        {
            outputPath = null; error = null;
            if (!HistoricalLegacyArchiveValidator.TryValidate(archive, out error)) return false;
            string stem = Regex.Replace(archiveName ?? string.Empty, "[^A-Za-z0-9_-]+", "_").Trim('_');
            if (stem.Length > 80) stem = stem.Substring(0, 80);
            if (string.IsNullOrWhiteSpace(stem)) stem = "LegacyArchive_" + archive.ArchiveId.Substring(0, 8);
            try
            {
                Directory.CreateDirectory(ArchiveDirectory);
                string target = Path.Combine(ArchiveDirectory, stem + ".json");
                string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                string backup = target + "." + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".bak";
                byte[] bytes;
                using (var memory = new MemoryStream()) { Serialize(memory, archive); bytes = memory.ToArray(); }
                if (bytes.Length == 0 || bytes.Length > MaximumArchiveBytes) return Fail("Serialized archive exceeds the 20 MB safety limit.", out error);
                try
                {
                    using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    using (var verify = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        HistoricalLegacyArchive parsed = Deserialize(verify);
                        if (!HistoricalLegacyArchiveValidator.TryValidate(parsed, out error)) throw new InvalidDataException("Temporary archive failed validation: " + error);
                    }
                    if (!File.Exists(target)) File.Move(temp, target);
                    else
                    {
                        try { File.Replace(temp, target, backup, true); }
                        catch (PlatformNotSupportedException) { ReplaceByMoves(temp, target, backup); }
                        catch (NotSupportedException) { ReplaceByMoves(temp, target, backup); }
                    }
                    outputPath = target; return true;
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            catch (Exception exception) { return Fail("Atomic archive write failed; the previous archive was preserved where the platform supports replacement: " + exception.Message, out error); }
        }

        private static void ReplaceByMoves(string temp, string target, string backup)
        {
            bool movedOriginal = false;
            try
            {
                File.Move(target, backup); movedOriginal = true;
                File.Move(temp, target);
            }
            catch
            {
                if (movedOriginal && !File.Exists(target) && File.Exists(backup)) File.Move(backup, target);
                throw;
            }
        }

        public static byte[] SerializeToBytes(HistoricalLegacyArchive archive)
        { using (var memory = new MemoryStream()) { Serialize(memory, archive); return memory.ToArray(); } }
        public static HistoricalLegacyArchive Clone(HistoricalLegacyArchive archive)
        { using (var memory = new MemoryStream(SerializeToBytes(archive))) return Deserialize(memory); }
        private static void Serialize(Stream stream, HistoricalLegacyArchive archive) => Serializer().WriteObject(stream, archive);
        private static HistoricalLegacyArchive Deserialize(Stream stream) => Serializer().ReadObject(stream) as HistoricalLegacyArchive;
        private static DataContractJsonSerializer Serializer() => new DataContractJsonSerializer(typeof(HistoricalLegacyArchive), new DataContractJsonSerializerSettings
        { MaxItemsInObjectGraph = 250000, EmitTypeInformation = System.Runtime.Serialization.EmitTypeInformation.Never, UseSimpleDictionaryFormat = true });
        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
