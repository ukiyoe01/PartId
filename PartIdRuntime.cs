using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PartId
{
    public sealed class PartIdRuntime : MonoBehaviour
    {
        internal const string PidKey = "pid";
        internal const string PidPrefix = "pid_";
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
            // Wrapped so a failure can never hang or crash the game during mod load; the periodic
            // Update() retries, so the worst case is pids simply aren't assigned yet.
            try
            {
                PartIdRecordStore.EnsureRecordFileExists();
            }
            catch (Exception ex)
            {
                Debug.Log("[PartId] EnsureRecordFileExists failed: " + ex);
            }

            try
            {
                EnsureBlueprintPids(true);
            }
            catch (Exception ex)
            {
                Debug.Log("[PartId] EnsureBlueprintPids (Awake) failed: " + ex);
            }
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

            // Only the CURRENT world's blueprint is processed. We deliberately do NOT scan and
            // rewrite every saved blueprint: with deterministic pids, a saved blueprint resolves
            // to the same pids whenever it is actually loaded, so touching them all up front is
            // unnecessary — and doing it during mod load hung the game for players with hundreds
            // of saved blueprints (each one parsed and rewritten synchronously on the main thread).
            return EnsureBlueprintFilePids(path, true, force);
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

            // Match by the part's own key only. We deliberately do NOT fall back to scanning the
            // build grid for this part's index: that is an O(N) walk of every part, and calling it
            // once per part during a bulk restore (every part on scene load) is O(N²) — that was
            // stalling large craft for many seconds. Same-key-collision parts are rare and simply
            // aren't individually resolvable without that scan.

            if (TryGetOnlyPid(out pid, out string onlyPartKey))
            {
                partKey = onlyPartKey;
                return true;
            }

            // Not found. We do NOT force a full blueprint re-parse here: EnsureBlueprintPids(false)
            // above already refreshed the cache whenever the file changed, so re-parsing the same
            // file would produce the same result — and doing it once per queried part turned bulk
            // lookups (e.g. a consumer restoring every part on scene load) into O(N²) work that
            // froze large craft. If it isn't in the fresh cache, it simply isn't resolvable now.
            return false;
        }

        internal static bool TryBuildPartKey(object part, out string key)
        {
            key = null;
            if (!TryGetPartName(part, out string name) || !TryGetPartPosition(part, out Vector2 position))
                return false;

            key = BuildPartKey(name, position);
            return true;
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
                // Open with FileShare.ReadWrite so we can read the blueprint even while the game
                // still holds it open — e.g. right after it saves on a scene change. A plain
                // File.ReadAllText requests FileShare.Read, which collides with the game's own
                // open handle on Windows and throws "Sharing violation". That read failure left
                // the pid cache empty, so consumer mods couldn't resolve parts and per-part data
                // (such as a saved mass) appeared to reset. Sharing the handle avoids the clash.
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                    text = reader.ReadToEnd();
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

            var usedPids = new Hashtable();
            var partKeyCounts = new Dictionary<string, int>();

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

                // Deterministic pid: derived from the part's identity (name + rounded position)
                // and its ordinal among same-key parts, in blueprint file order. Any client
                // reading the same blueprint computes the same pid, so a shared blueprint/save
                // yields identical pids on every machine — no more random per-client divergence.
                int ordinal = partKeyCounts.TryGetValue(record.PartKey, out int seen) ? seen : 0;
                partKeyCounts[record.PartKey] = ordinal + 1;

                string pid = DeterministicPid(record.PartKey, ordinal);

                // Collision guard (practically never triggers, since (partKey, ordinal) is
                // unique): keep salting deterministically until the pid is unused here. Hard cap
                // the retries so a pathological case can never spin forever and hang load.
                int salt = 0;
                while (usedPids[pid] != null && salt < 10000)
                    pid = DeterministicPid(record.PartKey, ordinal, ++salt);

                // If the part still carries an older pid (e.g. a random id written by an early
                // PartId version), move its stored records onto the new deterministic pid so
                // existing data is preserved rather than orphaned.
                string oldPid = record.Pid;
                if (!string.IsNullOrEmpty(oldPid) && oldPid != pid)
                    PartIdRecordStore.MigratePid(oldPid, pid);

                usedPids[pid] = true;

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

            // NOTE: PartId never writes pids back into the blueprint file. With deterministic pids
            // every part's id is recomputed on demand, so there is no need to modify the player's
            // blueprint/save files at all — they are treated as strictly read-only. This removes
            // any risk of corrupting or "polluting" the player's builds, and a shared file still
            // resolves to identical pids on every machine (the values are computed, not stored).

            if (updateRuntimeCache)
            {
                cachedBlueprintPath = path;
                cachedBlueprintWriteTimeUtc = writeTime;
            }

            return true;
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

            Match pidMatch = Regex.Match(json, "\"" + PidKey + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");

            record.Name = name;
            record.Position = new Vector2(x, y);
            record.PartKey = BuildPartKey(name, record.Position);
            record.Pid = pidMatch.Success ? UnescapeJsonString(pidMatch.Groups[1].Value) : "";
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

        static bool TryReadJsonNumber(string json, string name, out float value)
        {
            value = 0f;
            Match match = Regex.Match(json, "\"" + name + "\"\\s*:\\s*(\"?)([-+0-9.Ee]+)\\1");
            return match.Success && float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        static string DeterministicPid(string partKey, int ordinal)
        {
            return DeterministicPid(partKey, ordinal, 0);
        }

        // Produces a stable pid_<32 hex> from the part's key and its ordinal among same-key
        // parts. Same inputs -> same pid on every machine, so a shared blueprint/save resolves
        // to identical pids everywhere (letting records travel with the file). `salt` only
        // changes the output if a collision ever needs to be broken, still deterministically.
        //
        // Uses a self-contained FNV-1a hash rather than System.Security.Cryptography (MD5/SHA):
        // that crypto assembly can be stripped from the game's managed build on some platforms
        // (notably Windows), where calling it would fail during mod load. Plain integer math is
        // always available and identical across platforms, which is exactly what we need.
        static string DeterministicPid(string partKey, int ordinal, int salt)
        {
            string input = (partKey ?? "") + "#" + ordinal.ToString(CultureInfo.InvariantCulture);
            if (salt > 0)
                input += "!" + salt.ToString(CultureInfo.InvariantCulture);

            byte[] bytes = Encoding.UTF8.GetBytes(input);
            // Two 64-bit FNV-1a passes with different seeds give 128 bits -> 32 hex chars.
            ulong high = Fnv1a64(bytes, 14695981039346656037UL);
            ulong low = Fnv1a64(bytes, 14695981039346656037UL ^ 0x9E3779B97F4A7C15UL);

            return PidPrefix + high.ToString("x16", CultureInfo.InvariantCulture)
                             + low.ToString("x16", CultureInfo.InvariantCulture);
        }

        static ulong Fnv1a64(byte[] data, ulong seed)
        {
            const ulong prime = 1099511628211UL;
            ulong hash = seed;
            foreach (byte b in data)
            {
                hash ^= b;
                hash *= prime;
            }

            return hash;
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

        static string UnescapeJsonString(string value)
        {
            return (value ?? "").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        class BlueprintPartRecord
        {
            public int Index;
            public string Name;
            public Vector2 Position;
            public string PartKey;
            public string Pid;
        }
    }
}
