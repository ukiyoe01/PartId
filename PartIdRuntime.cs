using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PartId
{
    public sealed class PartIdRuntime : MonoBehaviour
    {
        internal const string PidKey = "pid";
        internal const string LegacyPidKey = "amm_id";
        internal const string BadPidKey = "pid_id";
        internal const string PidPrefix = "pid_";
        const string LegacyPidPrefix = "amm_";
        const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        static PartIdRuntime instance;
        static readonly Hashtable pidByPartKey = new Hashtable();
        static readonly Hashtable pidByPartIndex = new Hashtable();
        static readonly Hashtable partKeyByPartIndex = new Hashtable();
        static readonly Hashtable ambiguousPartKeys = new Hashtable();
        static string cachedBlueprintPath;
        static DateTime cachedBlueprintWriteTimeUtc;

        float nextScan;

        public static void Ensure()
        {
            if (instance != null)
                return;

            var host = new GameObject("PartId_Runtime");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<PartIdRuntime>();
        }

        void Awake()
        {
            PartIdRecordStore.EnsureRecordFileExists();
            EnsureBlueprintPids(true);
        }

        void Update()
        {
            if (Time.realtimeSinceStartup < nextScan)
                return;

            nextScan = Time.realtimeSinceStartup + 2f;
            EnsureBlueprintPids(false);
        }

        internal static bool EnsureBlueprintPids(bool force)
        {
            string path = FindCurrentBlueprintPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                if (force)
                    Debug.Log("[PartId] No blueprint path found under game Saving/Worlds.");
                return false;
            }

            bool result = EnsureBlueprintFilePids(path, true, force);
            if (result)
                EnsureSavedBlueprintPids();

            return result;
        }

        internal static bool TryGetPidForPart(object part, out string pid, out string partKey)
        {
            pid = null;
            partKey = null;

            TryBuildPartKey(part, out partKey);
            EnsureBlueprintPids(false);

            // Prefer matching by the part's own name+position key: it is intrinsic to the part
            // and stays correct even when other parts are added/removed and shift the live
            // scene's array order out of sync with the order cached from the last file parse.
            if (!string.IsNullOrEmpty(partKey))
            {
                pid = ambiguousPartKeys[partKey] == null ? pidByPartKey[partKey] as string : null;
                if (!string.IsNullOrEmpty(pid))
                    return true;
            }

            int runtimeIndex = -1;
            bool hasRuntimeIndex = TryGetRuntimePartIndex(part, out runtimeIndex);
            if (hasRuntimeIndex && TryGetIndexMatch(runtimeIndex, partKey, out pid, out partKey))
                return true;

            if (TryGetOnlyPid(out pid, out string onlyPartKey))
            {
                partKey = onlyPartKey;
                return true;
            }

            EnsureBlueprintPids(true);

            if (!string.IsNullOrEmpty(partKey) && ambiguousPartKeys[partKey] == null)
                pid = pidByPartKey[partKey] as string;

            if (string.IsNullOrEmpty(pid) && hasRuntimeIndex && TryGetIndexMatch(runtimeIndex, partKey, out pid, out partKey))
                return true;

            if (string.IsNullOrEmpty(pid) && TryGetOnlyPid(out pid, out onlyPartKey))
                partKey = onlyPartKey;

            return !string.IsNullOrEmpty(pid);
        }

        // Only trust an index-based match if it actually agrees with the part's own key (or we
        // have no key to compare against). A bare index match against a different part's key
        // means the live scene order has drifted from the cached file-parse order, and using it
        // would silently steal another part's pid and records.
        static bool TryGetIndexMatch(int runtimeIndex, string expectedPartKey, out string pid, out string partKey)
        {
            pid = pidByPartIndex[runtimeIndex] as string;
            string indexPartKey = partKeyByPartIndex[runtimeIndex] as string;
            partKey = expectedPartKey;

            if (string.IsNullOrEmpty(pid))
                return false;

            if (!string.IsNullOrEmpty(expectedPartKey) && expectedPartKey != indexPartKey)
            {
                pid = null;
                return false;
            }

            partKey = indexPartKey ?? expectedPartKey;
            return true;
        }

        internal static bool TryBuildPartKey(object part, out string key)
        {
            key = null;
            if (!TryGetPartName(part, out string name) || !TryGetPartPosition(part, out Vector2 position))
                return false;

            key = BuildPartKey(name, position);
            return true;
        }

        internal static string NormalizePid(string pid)
        {
            if (string.IsNullOrEmpty(pid))
                return pid;

            return pid.StartsWith(LegacyPidPrefix, StringComparison.Ordinal)
                ? PidPrefix + pid.Substring(LegacyPidPrefix.Length)
                : pid;
        }

        internal static string GetGameAppRootPath()
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(Application.dataPath);
                while (dir != null)
                {
                    string savingPath = Path.Combine(dir.FullName, "Saving", "Worlds");
                    string modsPath = Path.Combine(dir.FullName, "Mods");
                    if (Directory.Exists(savingPath) && Directory.Exists(modsPath))
                        return dir.FullName;

                    if (dir.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                        return dir.FullName;

                    dir = dir.Parent;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        static bool TryGetOnlyPid(out string pid, out string partKey)
        {
            pid = null;
            partKey = null;

            if (pidByPartIndex.Count != 1)
                return false;

            foreach (DictionaryEntry entry in pidByPartIndex)
            {
                pid = entry.Value as string;
                partKey = partKeyByPartIndex[entry.Key] as string ?? "index:" + Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                return !string.IsNullOrEmpty(pid);
            }

            return false;
        }

        static bool EnsureBlueprintFilePids(string path, bool updateRuntimeCache, bool force)
        {
            DateTime writeTime;
            try
            {
                writeTime = File.GetLastWriteTimeUtc(path);
            }
            catch
            {
                return false;
            }

            if (updateRuntimeCache && !force && path == cachedBlueprintPath && writeTime == cachedBlueprintWriteTimeUtc && pidByPartKey.Count > 0)
                return true;

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Debug.Log("[PartId] Read blueprint failed: " + ex);
                return false;
            }

            List<BlueprintPartRecord> records = ParseBlueprintParts(text);
            if (records.Count == 0)
            {
                if (force)
                    Debug.Log("[PartId] Blueprint parsed with 0 parts: " + path);
                return false;
            }

            bool changed = false;
            var replacements = new List<BlueprintReplacement>();
            var usedPids = new Hashtable();

            // Snapshot the previous run's pid maps before clearing, so that if the blueprint
            // file lost its "pid" fields (e.g. the game's own save routine rewrote the file
            // without knowing about our custom field), we can recover the same pid for the
            // same part instead of minting a brand-new one and orphaning its records.
            Hashtable previousPidByPartIndex = updateRuntimeCache ? (Hashtable)pidByPartIndex.Clone() : null;
            Hashtable previousPidByPartKey = updateRuntimeCache ? (Hashtable)pidByPartKey.Clone() : null;

            if (updateRuntimeCache)
            {
                pidByPartKey.Clear();
                pidByPartIndex.Clear();
                partKeyByPartIndex.Clear();
                ambiguousPartKeys.Clear();
            }

            foreach (BlueprintPartRecord record in records)
            {
                if (string.IsNullOrEmpty(record.PartKey))
                    continue;

                string pid = NormalizePid(record.Pid);
                bool needsRewrite = record.NeedsPidRewrite;

                if (string.IsNullOrEmpty(pid))
                {
                    pid = previousPidByPartIndex?[record.Index] as string;
                    if (string.IsNullOrEmpty(pid))
                        pid = previousPidByPartKey?[record.PartKey] as string;
                    if (string.IsNullOrEmpty(pid))
                        pid = NewPid();

                    needsRewrite = true;
                }

                if (usedPids[pid] != null)
                {
                    pid = NewPid();
                    needsRewrite = true;
                }

                usedPids[pid] = true;

                string updatedJson = needsRewrite ? AddOrReplacePid(CleanPidFields(record.Json), pid) : CleanLegacyPidFields(record.Json);
                if (updatedJson != record.Json)
                {
                    replacements.Add(new BlueprintReplacement(record.Start, record.Length, updatedJson));
                    changed = true;
                }

                if (updateRuntimeCache)
                {
                    if (pidByPartKey[record.PartKey] != null && (string)pidByPartKey[record.PartKey] != pid)
                        ambiguousPartKeys[record.PartKey] = true;
                    else if (ambiguousPartKeys[record.PartKey] == null)
                        pidByPartKey[record.PartKey] = pid;

                    pidByPartIndex[record.Index] = pid;
                    partKeyByPartIndex[record.Index] = record.PartKey;
                }
            }

            if (changed && replacements.Count > 0)
            {
                replacements.Sort((a, b) => b.Start.CompareTo(a.Start));
                var builder = new StringBuilder(text);
                foreach (BlueprintReplacement replacement in replacements)
                {
                    builder.Remove(replacement.Start, replacement.Length);
                    builder.Insert(replacement.Start, replacement.Text);
                }

                try
                {
                    File.WriteAllText(path, builder.ToString());
                    writeTime = File.GetLastWriteTimeUtc(path);
                    Debug.Log("[PartId] Wrote blueprint pid codes: " + path);
                }
                catch (Exception ex)
                {
                    Debug.Log("[PartId] Write blueprint pid codes failed: " + ex);
                    return false;
                }
            }

            if (updateRuntimeCache)
            {
                cachedBlueprintPath = path;
                cachedBlueprintWriteTimeUtc = writeTime;
            }

            return true;
        }

        static void EnsureSavedBlueprintPids()
        {
            string appRoot = GetGameAppRootPath();
            if (string.IsNullOrEmpty(appRoot))
                return;

            string blueprintsRoot = Path.Combine(appRoot, "Saving", "Blueprints");
            if (!Directory.Exists(blueprintsRoot))
                return;

            try
            {
                foreach (string blueprintPath in Directory.GetFiles(blueprintsRoot, "Blueprint.txt", SearchOption.AllDirectories))
                    EnsureBlueprintFilePids(blueprintPath, false, false);
            }
            catch (Exception ex)
            {
                Debug.Log("[PartId] Scan saved blueprints failed: " + ex);
            }
        }

        static string FindCurrentBlueprintPath()
        {
            string appRoot = GetGameAppRootPath();
            if (string.IsNullOrEmpty(appRoot))
                return null;

            string worlds = Path.Combine(appRoot, "Saving", "Worlds");
            if (!Directory.Exists(worlds))
                return null;

            string best = null;
            DateTime bestTime = DateTime.MinValue;

            try
            {
                foreach (string world in Directory.GetDirectories(worlds))
                {
                    string candidate = Path.Combine(world, "BlueprintPersistent", "Blueprint.txt");
                    if (!File.Exists(candidate))
                        continue;

                    DateTime time = File.GetLastWriteTimeUtc(candidate);
                    if (time > bestTime)
                    {
                        bestTime = time;
                        best = candidate;
                    }
                }
            }
            catch
            {
            }

            return best;
        }

        static List<BlueprintPartRecord> ParseBlueprintParts(string text)
        {
            var records = new List<BlueprintPartRecord>();
            int partsName = text.IndexOf("\"parts\"", StringComparison.Ordinal);
            if (partsName < 0)
                return records;

            int arrayStart = text.IndexOf('[', partsName);
            if (arrayStart < 0)
                return records;

            int arrayEnd = FindMatching(text, arrayStart, '[', ']');
            if (arrayEnd < 0)
                return records;

            int index = arrayStart + 1;
            while (index < arrayEnd)
            {
                int objectStart = text.IndexOf('{', index);
                if (objectStart < 0 || objectStart >= arrayEnd)
                    break;

                int objectEnd = FindMatching(text, objectStart, '{', '}');
                if (objectEnd < 0 || objectEnd > arrayEnd)
                    break;

                string json = text.Substring(objectStart, objectEnd - objectStart + 1);
                if (TryParseBlueprintPart(json, out BlueprintPartRecord record))
                {
                    record.Index = records.Count;
                    record.Start = objectStart;
                    record.Length = objectEnd - objectStart + 1;
                    record.Json = json;
                    records.Add(record);
                }

                index = objectEnd + 1;
            }

            return records;
        }

        static bool TryParseBlueprintPart(string json, out BlueprintPartRecord record)
        {
            record = new BlueprintPartRecord();

            Match nameMatch = Regex.Match(json, "\"n\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            Match positionMatch = Regex.Match(json, "\"p\"\\s*:\\s*\\{(?<body>.*?)\\}", RegexOptions.Singleline);
            if (!nameMatch.Success || !positionMatch.Success)
                return false;

            string name = UnescapeJsonString(nameMatch.Groups[1].Value);
            string positionBody = positionMatch.Groups["body"].Value;
            if (!TryReadJsonNumber(positionBody, "x", out float x) || !TryReadJsonNumber(positionBody, "y", out float y))
                return false;

            MatchCollection pidMatches = Regex.Matches(json, "\"" + PidKey + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            Match pidMatch = pidMatches.Count > 0 ? pidMatches[0] : Match.Empty;
            Match legacyPidMatch = Regex.Match(json, "\"" + LegacyPidKey + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            Match badPidMatch = Regex.Match(json, "\"" + BadPidKey + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");

            record.Name = name;
            record.Position = new Vector2(x, y);
            record.PartKey = BuildPartKey(name, record.Position);
            record.Pid = pidMatch.Success
                ? UnescapeJsonString(pidMatch.Groups[1].Value)
                : legacyPidMatch.Success
                    ? UnescapeJsonString(legacyPidMatch.Groups[1].Value)
                    : badPidMatch.Success ? UnescapeJsonString(badPidMatch.Groups[1].Value) : "";
            record.NeedsPidRewrite = pidMatches.Count != 1 ||
                                      legacyPidMatch.Success ||
                                      badPidMatch.Success ||
                                      record.Pid.StartsWith(LegacyPidPrefix, StringComparison.Ordinal);
            return true;
        }

        static int FindMatching(string text, int start, char open, char close)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;

            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        inString = false;

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == open)
                    depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        static string AddOrReplacePid(string json, string pid)
        {
            json = CleanPidFields(json);
            string escapedPid = EscapeJsonString(pid);
            int close = json.LastIndexOf('}');
            if (close < 0)
                return json;

            string prefix = json.Substring(0, close).TrimEnd();
            return prefix + ",\n      \"" + PidKey + "\": \"" + escapedPid + "\"\n    }";
        }

        static string CleanPidFields(string json)
        {
            json = RemoveStringField(json, PidKey);

            return CleanLegacyPidFields(json);
        }

        static string CleanLegacyPidFields(string json)
        {
            json = RemoveStringField(json, LegacyPidKey);

            return RemoveStringField(json, BadPidKey);
        }

        static string RemoveStringField(string json, string key)
        {
            return Regex.Replace(
                json,
                "(?<leading>\\s*,\\s*)?\\s*\"" + Regex.Escape(key) + "\"\\s*:\\s*\"(?:\\\\.|[^\"\\\\])*\"(?<trailing>\\s*,\\s*)?",
                match =>
                {
                    bool hasLeadingComma = match.Groups["leading"].Success;
                    bool hasTrailingComma = match.Groups["trailing"].Success;
                    return hasLeadingComma && hasTrailingComma ? match.Groups["leading"].Value : "";
                },
                RegexOptions.Singleline);
        }

        static bool TryReadJsonNumber(string json, string name, out float value)
        {
            value = 0f;
            Match match = Regex.Match(json, "\"" + name + "\"\\s*:\\s*(\"?)([-+0-9.Ee]+)\\1");
            return match.Success && float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        static string NewPid()
        {
            return PidPrefix + Guid.NewGuid().ToString("N");
        }

        static string BuildPartKey(string name, Vector2 position)
        {
            return name + "|" + RoundKey(position.x) + "|" + RoundKey(position.y);
        }

        static string RoundKey(float value)
        {
            return Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
        }

        static bool TryGetPartName(object part, out string name)
        {
            name = "";
            if (!IsUsablePart(part))
                return false;

            name = Convert.ToString(GetMemberValue(part.GetType(), part, "Name"), CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(name) && part is Component component && component != null)
            {
                try
                {
                    name = component.gameObject != null ? component.gameObject.name : "";
                }
                catch
                {
                    name = "";
                }
            }

            return !string.IsNullOrEmpty(name);
        }

        static bool TryGetPartPosition(object part, out Vector2 position)
        {
            position = Vector2.zero;

            object value = GetMemberValue(part.GetType(), part, "Position");
            if (TryExtractVector2(value, out position))
                return true;

            if (part is Component component)
            {
                try
                {
                    Vector3 local = component.transform.localPosition;
                    position = new Vector2(local.x, local.y);
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        static bool TryExtractVector2(object value, out Vector2 vector)
        {
            vector = Vector2.zero;
            if (value == null)
                return false;

            if (value is Vector2 direct)
            {
                vector = direct;
                return true;
            }

            object x = GetMemberValue(value.GetType(), value, "x");
            object y = GetMemberValue(value.GetType(), value, "y");
            if (TryExtractNumber(x, out double xValue) && TryExtractNumber(y, out double yValue))
            {
                vector = new Vector2((float)xValue, (float)yValue);
                return true;
            }

            return false;
        }

        static bool TryGetRuntimePartIndex(object part, out int index)
        {
            index = -1;
            if (!IsUsablePart(part))
                return false;

            Type buildGridType = FindType("SFS.Builds.BuildGrid");
            if (buildGridType == null)
                return false;

            foreach (object grid in GetBuildGridCandidates(buildGridType))
            {
                object activeGrid = GetMemberValue(grid.GetType(), grid, "activeGrid");
                if (TryGetPartIndexFromPartGrid(activeGrid, part, out index))
                    return true;

                object inactiveGrid = GetMemberValue(grid.GetType(), grid, "inactiveGrid");
                if (TryGetPartIndexFromPartGrid(inactiveGrid, part, out index))
                    return true;
            }

            return false;
        }

        static object[] GetBuildGridCandidates(Type buildGridType)
        {
            ArrayList list = new ArrayList();

            foreach (object grid in GetSingletonsAndInstances(buildGridType))
                AddUnique(list, grid);

            foreach (string ownerName in new[] { "SFS.Builds.BuildManager", "SFS.Builds.BuildState" })
            {
                Type ownerType = FindType(ownerName);
                if (ownerType == null)
                    continue;

                foreach (object owner in GetSingletonsAndInstances(ownerType))
                {
                    object grid = GetMemberValue(ownerType, owner, "buildGrid");
                    if (grid != null)
                        AddUnique(list, grid);
                }
            }

            return list.Cast<object>().ToArray();
        }

        static bool TryGetPartIndexFromPartGrid(object partGrid, object part, out int index)
        {
            index = -1;
            if (partGrid == null)
                return false;

            object partsHolder = GetMemberValue(partGrid.GetType(), partGrid, "partsHolder");
            if (TryGetPartIndexFromCollection(partsHolder, part, out index))
                return true;

            return TryGetPartIndexFromCollection(partGrid, part, out index);
        }

        static bool TryGetPartIndexFromCollection(object source, object part, out int index)
        {
            index = -1;
            if (source == null)
                return false;

            object parts = null;
            MethodInfo getArray = source.GetType().GetMethod("GetArray", AllMembers, null, Type.EmptyTypes, null);
            if (getArray != null)
            {
                try
                {
                    parts = getArray.Invoke(source, null);
                }
                catch
                {
                }
            }

            if (parts == null)
                parts = GetMemberValue(source.GetType(), source, "parts");

            if (!(parts is IEnumerable enumerable) || parts is string)
                return false;

            int currentIndex = 0;
            foreach (object candidate in enumerable)
            {
                if (SameUnityObject(candidate, part))
                {
                    index = currentIndex;
                    return true;
                }

                currentIndex++;
            }

            return false;
        }

        static bool SameUnityObject(object a, object b)
        {
            if (ReferenceEquals(a, b))
                return true;

            try
            {
                if (a is UnityEngine.Object unityA && b is UnityEngine.Object unityB)
                    return unityA == unityB;
            }
            catch
            {
            }

            return false;
        }

        static object[] GetSingletonsAndInstances(Type type)
        {
            ArrayList list = new ArrayList();

            foreach (string name in new[] { "main", "Main", "instance", "Instance" })
            {
                object value = GetMemberValue(type, null, name);
                if (value != null)
                    list.Add(value);
            }

            try
            {
#pragma warning disable CS0618
                foreach (object obj in UnityEngine.Object.FindObjectsOfType(type))
#pragma warning restore CS0618
                    if (obj != null)
                        list.Add(obj);
            }
            catch
            {
            }

            return list.Cast<object>().Distinct().ToArray();
        }

        static bool IsUsablePart(object part)
        {
            if (part == null)
                return false;

            try
            {
                if (part is UnityEngine.Object unityObject && unityObject == null)
                    return false;
            }
            catch
            {
                return false;
            }

            return part.GetType().FullName == "SFS.Parts.Part";
        }

        static void AddUnique(ArrayList list, object value)
        {
            if (value != null && !list.Contains(value))
                list.Add(value);
        }

        static bool TryExtractNumber(object value, out double number)
        {
            if (value == null)
            {
                number = 0d;
                return false;
            }

            Type type = value.GetType();
            if (IsPrimitiveNumber(type))
            {
                number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            }

            foreach (string name in new[] { "Value", "value", "Input", "input", "current", "Current" })
            {
                object nested = GetMemberValue(type, value, name);
                if (nested != null && TryExtractNumber(nested, out number))
                    return true;
            }

            MethodInfo getResult = type.GetMethod("GetResult", AllMembers, null, new[] { typeof(bool) }, null);
            if (getResult != null)
            {
                try
                {
                    object result = getResult.Invoke(value, new object[] { true });
                    if (result != null && TryExtractNumber(result, out number))
                        return true;
                }
                catch
                {
                }
            }

            number = 0d;
            return false;
        }

        static bool IsPrimitiveNumber(Type type)
        {
            return type == typeof(double) || type == typeof(float) || type == typeof(int) ||
                   type == typeof(long) || type == typeof(short) || type == typeof(decimal);
        }

        static object GetMemberValue(Type type, object instance, string name)
        {
            if (type == null)
                return null;

            try
            {
                FieldInfo field = type.GetField(name, AllMembers);
                if (field != null)
                    return GetMemberValue(field, instance);

                PropertyInfo property = type.GetProperty(name, AllMembers);
                if (property != null)
                    return GetMemberValue(property, instance);

                MethodInfo method = type.GetMethod(name, AllMembers, null, Type.EmptyTypes, null);
                if (method != null)
                    return GetMemberValue(method, instance);
            }
            catch (AmbiguousMatchException)
            {
                // A derived type hides a base member of the same name (e.g. via `new`), so the
                // unqualified lookup across the whole hierarchy is ambiguous. Fall back to the
                // most-derived declaration instead of aborting the caller.
                for (Type current = type; current != null; current = current.BaseType)
                {
                    FieldInfo field = current.GetField(name, AllMembers | BindingFlags.DeclaredOnly);
                    if (field != null)
                        return GetMemberValue(field, instance);

                    PropertyInfo property = current.GetProperty(name, AllMembers | BindingFlags.DeclaredOnly);
                    if (property != null)
                        return GetMemberValue(property, instance);

                    MethodInfo method = current.GetMethod(name, AllMembers | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                    if (method != null)
                        return GetMemberValue(method, instance);
                }
            }

            return null;
        }

        static object GetMemberValue(MemberInfo member, object instance)
        {
            try
            {
                if (member is FieldInfo field)
                    return field.GetValue(field.IsStatic ? null : instance);

                if (member is PropertyInfo property)
                {
                    if (property.GetIndexParameters().Length != 0)
                        return null;

                    MethodInfo getter = property.GetGetMethod(true);
                    if (getter == null)
                        return null;

                    return property.GetValue(getter.IsStatic ? null : instance, null);
                }

                if (member is MethodInfo method)
                {
                    if (method.GetParameters().Length != 0)
                        return null;

                    return method.Invoke(method.IsStatic ? null : instance, null);
                }
            }
            catch
            {
            }

            return null;
        }

        static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        static string EscapeJsonString(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static string UnescapeJsonString(string value)
        {
            return (value ?? "").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        class BlueprintPartRecord
        {
            public int Index;
            public int Start;
            public int Length;
            public string Json;
            public string Name;
            public Vector2 Position;
            public string PartKey;
            public string Pid;
            public bool NeedsPidRewrite;
        }

        class BlueprintReplacement
        {
            public readonly int Start;
            public readonly int Length;
            public readonly string Text;

            public BlueprintReplacement(int start, int length, string text)
            {
                Start = start;
                Length = length;
                Text = text;
            }
        }
    }
}
