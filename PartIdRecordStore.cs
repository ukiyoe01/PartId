using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PartId
{
    static class PartIdRecordStore
    {
        const string Header = "# " + PartIdApi.RecordFormat + " pid owner64 key64 type payload";
        static readonly Dictionary<string, Record> records = new Dictionary<string, Record>();
        static bool loaded;

        public static string GetRecordFilePath()
        {
            string appRoot = PartIdRuntime.GetGameAppRootPath();
            if (!string.IsNullOrEmpty(appRoot))
                return Path.Combine(appRoot, "Mods", "PartId", "pid-records.tsv");

            return Path.Combine(Application.persistentDataPath, "PartId", "pid-records.tsv");
        }

        public static bool SetValue(string pid, string owner, string key, PartIdValue value)
        {
            Load();

            pid = NormalizeInputPid(pid);
            if (!IsValidPid(pid) || string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(key) ||
                value == null || !PartIdValue.IsKnownType(value.Type))
                return false;

            var record = new Record(pid, owner.Trim(), key.Trim(), value);
            records[CompositeKey(record.Pid, record.Owner, record.Key)] = record;
            return Save();
        }

        public static bool RemoveValue(string pid, string owner, string key)
        {
            Load();

            pid = NormalizeInputPid(pid);
            if (!IsValidPid(pid) || string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(key))
                return false;

            records.Remove(CompositeKey(pid, owner.Trim(), key.Trim()));
            return Save();
        }

        // Moves every record from oldPid onto newPid, across all owners. Used when a part's pid
        // changes (e.g. an old random pid is replaced by a deterministic one) so its stored data
        // follows the part instead of being orphaned.
        public static bool MigratePid(string oldPid, string newPid)
        {
            Load();

            oldPid = NormalizeInputPid(oldPid);
            newPid = NormalizeInputPid(newPid);
            if (!IsValidPid(oldPid) || !IsValidPid(newPid) || oldPid == newPid)
                return false;

            var toMove = new List<Record>();
            foreach (Record record in records.Values)
            {
                if (record.Pid == oldPid)
                    toMove.Add(record);
            }

            if (toMove.Count == 0)
                return false;

            foreach (Record record in toMove)
            {
                records.Remove(CompositeKey(record.Pid, record.Owner, record.Key));
                var moved = new Record(newPid, record.Owner, record.Key, record.Value);
                records[CompositeKey(newPid, record.Owner, record.Key)] = moved;
            }

            Debug.Log("[PartId] Migrated " + toMove.Count + " record(s) from " + oldPid + " to " + newPid + ".");
            return Save();
        }

        public static bool TryGetValue(string pid, string owner, string key, out PartIdValue value)
        {
            Load();

            value = null;
            pid = NormalizeInputPid(pid);
            if (!IsValidPid(pid) || string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(key))
                return false;

            if (records.TryGetValue(CompositeKey(pid, owner.Trim(), key.Trim()), out Record record))
            {
                value = record.Value;
                return true;
            }

            return false;
        }

        public static Dictionary<string, PartIdValue> GetRecordsForPid(string pid, string owner = null)
        {
            Load();

            pid = NormalizeInputPid(pid);
            var output = new Dictionary<string, PartIdValue>();
            foreach (Record record in records.Values)
            {
                if (record.Pid != pid)
                    continue;

                if (!string.IsNullOrEmpty(owner) && record.Owner != owner)
                    continue;

                output[record.Owner + "/" + record.Key] = record.Value;
            }

            return output;
        }

        public static List<PartIdRecord> GetRecordsForPidList(string pid, string owner = null)
        {
            Load();

            pid = NormalizeInputPid(pid);
            var output = new List<PartIdRecord>();
            if (!IsValidPid(pid))
                return output;

            foreach (Record record in SortedRecords())
            {
                if (record.Pid != pid)
                    continue;

                if (!string.IsNullOrEmpty(owner) && record.Owner != owner)
                    continue;

                output.Add(record.ToPublicRecord());
            }

            return output;
        }

        public static List<PartIdRecord> GetRecordsForOwner(string owner)
        {
            Load();

            var output = new List<PartIdRecord>();
            if (string.IsNullOrWhiteSpace(owner))
                return output;

            string normalizedOwner = owner.Trim();
            foreach (Record record in SortedRecords())
            {
                if (record.Owner == normalizedOwner)
                    output.Add(record.ToPublicRecord());
            }

            return output;
        }

        public static List<PartIdRecord> GetAllRecords()
        {
            Load();

            var output = new List<PartIdRecord>();
            foreach (Record record in SortedRecords())
                output.Add(record.ToPublicRecord());

            return output;
        }

        public static void EnsureRecordFileExists()
        {
            try
            {
                string path = GetRecordFilePath();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                if (!File.Exists(path))
                {
                    if (!TryMigrateLegacyRecordFile(path))
                        File.WriteAllText(path, Header + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[PartId] Create pid record file failed: " + ex);
            }
        }

        static bool TryMigrateLegacyRecordFile(string targetPath)
        {
            string legacyPath = GetLegacyRecordFilePath();
            if (string.IsNullOrEmpty(legacyPath) || !File.Exists(legacyPath))
                return false;

            try
            {
                string directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var lines = new List<string>();
                foreach (string line in File.ReadAllLines(legacyPath))
                {
                    if (line.StartsWith("#", StringComparison.Ordinal))
                    {
                        lines.Add(Header);
                        continue;
                    }

                    lines.Add(line);
                }

                if (lines.Count == 0 || !lines[0].StartsWith("#", StringComparison.Ordinal))
                    lines.Insert(0, Header);

                File.WriteAllLines(targetPath, lines.ToArray());
                Debug.Log("[PartId] Migrated legacy SfsPidCore records: " + legacyPath + " -> " + targetPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log("[PartId] Legacy SfsPidCore record migration failed: " + ex);
                return false;
            }
        }

        static string GetLegacyRecordFilePath()
        {
            string appRoot = PartIdRuntime.GetGameAppRootPath();
            if (!string.IsNullOrEmpty(appRoot))
                return Path.Combine(appRoot, "Mods", "SfsPidCore", "pid-records.tsv");

            return Path.Combine(Application.persistentDataPath, "SfsPidCore", "pid-records.tsv");
        }

        static void Load()
        {
            if (loaded)
                return;

            loaded = true;
            records.Clear();
            EnsureRecordFileExists();

            string path = GetRecordFilePath();
            if (!File.Exists(path))
                return;

            try
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                        continue;

                    string[] parts = line.Split('\t');
                    if (parts.Length < 6 || parts[0] != "v1")
                        continue;

                    string pid = PartIdRuntime.NormalizePid(parts[1]);
                    if (!IsValidPid(pid))
                        continue;

                    if (!PartIdValue.FromBase64(parts[2], out string owner) ||
                        !PartIdValue.FromBase64(parts[3], out string key))
                        continue;

                    string type = PartIdValue.NormalizeType(parts[4]);
                    if (!PartIdValue.IsKnownType(type))
                        continue;

                    string payload = parts[5];
                    var record = new Record(pid, owner, key, new PartIdValue(type, payload));
                    records[CompositeKey(record.Pid, record.Owner, record.Key)] = record;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[PartId] Load pid records failed: " + ex);
            }
        }

        static bool Save()
        {
            string path = GetRecordFilePath();

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var lines = new List<string> { Header };
                foreach (Record record in SortedRecords())
                {
                    string line = string.Join("\t", new[]
                    {
                        "v1",
                        record.Pid,
                        PartIdValue.ToBase64(record.Owner),
                        PartIdValue.ToBase64(record.Key),
                        PartIdValue.NormalizeType(record.Value.Type),
                        record.Value.Payload ?? ""
                    });
                    lines.Add(line);
                }

                File.WriteAllLines(path, lines.ToArray());
                Debug.Log("[PartId] Wrote typed pid records: " + path);
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log("[PartId] Save pid records failed: " + ex);
                return false;
            }
        }

        static string CompositeKey(string pid, string owner, string key)
        {
            return pid + "\n" + owner + "\n" + key;
        }

        static string NormalizeInputPid(string pid)
        {
            return PartIdRuntime.NormalizePid((pid ?? "").Trim());
        }

        static bool IsValidPid(string pid)
        {
            return !string.IsNullOrWhiteSpace(pid) && pid.StartsWith(PartIdRuntime.PidPrefix, StringComparison.Ordinal);
        }

        static List<Record> SortedRecords()
        {
            var sorted = new List<Record>(records.Values);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Pid + a.Owner + a.Key, b.Pid + b.Owner + b.Key));
            return sorted;
        }

        struct Record
        {
            public readonly string Pid;
            public readonly string Owner;
            public readonly string Key;
            public readonly PartIdValue Value;

            public Record(string pid, string owner, string key, PartIdValue value)
            {
                Pid = pid;
                Owner = owner;
                Key = key;
                Value = value;
            }

            public PartIdRecord ToPublicRecord()
            {
                return new PartIdRecord(Pid, Owner, Key, Value);
            }
        }
    }
}
