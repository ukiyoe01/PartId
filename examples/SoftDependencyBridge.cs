// Example only. Do not compile this file into PartId.dll.
// Copy this into a consumer mod when you want to call PartId without a hard
// assembly reference to PartId.dll.

using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ExamplePartIdConsumer
{
    static class PartIdSoftBridge
    {
        static Assembly assembly;
        static Type apiType;

        public static bool Available => LoadApi();

        public static int ApiVersion => GetPublicConstInt("ApiVersion");

        public static int CommonKeysVersion => GetPublicConstInt("CommonKeysVersion");

        public static bool Ensure()
        {
            return InvokeBool("Ensure", Type.EmptyTypes);
        }

        public static bool EnsureBlueprintPids(bool force)
        {
            return InvokeBool("EnsureBlueprintPids", new[] { typeof(bool) }, force);
        }

        public static bool SupportsValueType(string type)
        {
            return InvokeBool("SupportsValueType", new[] { typeof(string) }, type);
        }

        public static string[] GetSupportedValueTypes()
        {
            if (!LoadApi())
                return new string[0];

            try
            {
                MethodInfo method = apiType.GetMethod("GetSupportedValueTypes", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                return method?.Invoke(null, null) as string[] ?? new string[0];
            }
            catch (Exception ex)
            {
                Debug.Log("[ExamplePartIdConsumer] PartId.GetSupportedValueTypes failed: " + ex);
                return new string[0];
            }
        }

        public static string[] GetCommonKeyValueTypes(string key)
        {
            if (!LoadApi())
                return new string[0];

            try
            {
                MethodInfo method = apiType.GetMethod("GetCommonKeyValueTypes", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                return method?.Invoke(null, new object[] { key }) as string[] ?? new string[0];
            }
            catch (Exception ex)
            {
                Debug.Log("[ExamplePartIdConsumer] PartId.GetCommonKeyValueTypes failed: " + ex);
                return new string[0];
            }
        }

        public static bool SupportsCommonKeyType(string key, string type)
        {
            string normalized = (type ?? "").Trim().ToLowerInvariant();
            foreach (string valueType in GetCommonKeyValueTypes(key))
            {
                if ((valueType ?? "").Trim().ToLowerInvariant() == normalized)
                    return true;
            }

            return false;
        }

        public static bool TryGetPidForPart(object part, out string pid, out string partKey)
        {
            pid = null;
            partKey = null;

            if (!LoadApi())
                return false;

            try
            {
                MethodInfo method = apiType.GetMethod(
                    "TryGetPidForPart",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(object), typeof(string).MakeByRefType(), typeof(string).MakeByRefType() },
                    null);

                object[] args = { part, null, null };
                object result = method?.Invoke(null, args);
                pid = args[1] as string;
                partKey = args[2] as string;
                return result is bool ok && ok;
            }
            catch (Exception ex)
            {
                Debug.Log("[ExamplePartIdConsumer] PartId.TryGetPidForPart failed: " + ex);
                return false;
            }
        }

        public static bool SetBool(string pid, string owner, string key, bool value)
        {
            return InvokeBool("SetBool", new[] { typeof(string), typeof(string), typeof(string), typeof(bool) }, pid, owner, key, value);
        }

        public static bool TryGetBool(string pid, string owner, string key, out bool value)
        {
            object[] args = { pid, owner, key, false };
            bool ok = InvokeBoolWithArgs("TryGetBool", new[] { typeof(string), typeof(string), typeof(string), typeof(bool).MakeByRefType() }, args);
            value = args[3] is bool typed && typed;
            return ok;
        }

        public static bool SetDouble(string pid, string owner, string key, double value)
        {
            return InvokeBool("SetDouble", new[] { typeof(string), typeof(string), typeof(string), typeof(double) }, pid, owner, key, value);
        }

        public static bool TryGetDouble(string pid, string owner, string key, out double value)
        {
            object[] args = { pid, owner, key, 0d };
            bool ok = InvokeBoolWithArgs("TryGetDouble", new[] { typeof(string), typeof(string), typeof(string), typeof(double).MakeByRefType() }, args);
            value = args[3] is double typed ? typed : 0d;
            return ok;
        }

        public static bool SetJson(string pid, string owner, string key, string json)
        {
            return InvokeBool("SetJson", new[] { typeof(string), typeof(string), typeof(string), typeof(string) }, pid, owner, key, json);
        }

        public static bool TryGetJson(string pid, string owner, string key, out string json)
        {
            object[] args = { pid, owner, key, "" };
            bool ok = InvokeBoolWithArgs("TryGetJson", new[] { typeof(string), typeof(string), typeof(string), typeof(string).MakeByRefType() }, args);
            json = args[3] as string ?? "";
            return ok;
        }

        static bool InvokeBool(string methodName, Type[] parameterTypes, params object[] args)
        {
            return InvokeBoolWithArgs(methodName, parameterTypes, args);
        }

        static bool InvokeBoolWithArgs(string methodName, Type[] parameterTypes, object[] args)
        {
            if (!LoadApi())
                return false;

            try
            {
                MethodInfo method = apiType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
                object result = method?.Invoke(null, args);

                if (method?.ReturnType == typeof(void))
                    return true;

                return result is bool ok && ok;
            }
            catch (Exception ex)
            {
                Debug.Log("[ExamplePartIdConsumer] PartId." + methodName + " failed: " + ex);
                return false;
            }
        }

        static int GetPublicConstInt(string name)
        {
            if (!LoadApi())
                return 0;

            try
            {
                FieldInfo field = apiType.GetField(name, BindingFlags.Public | BindingFlags.Static);
                object value = field?.GetRawConstantValue();
                return value is int typed ? typed : 0;
            }
            catch
            {
                return 0;
            }
        }

        static bool LoadApi()
        {
            if (apiType != null)
                return true;

            foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (loaded.GetName().Name != "PartId")
                    continue;

                assembly = loaded;
                apiType = assembly.GetType("PartId.PartIdApi", false);
                if (apiType != null)
                    return true;
            }

            string path = FindPartIdDll();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.Log("[ExamplePartIdConsumer] PartId.dll was not found. Expected Mods/PartId/PartId.dll.");
                return false;
            }

            try
            {
                assembly = Assembly.LoadFrom(path);
                apiType = assembly.GetType("PartId.PartIdApi", false);
                return apiType != null;
            }
            catch (Exception ex)
            {
                Debug.Log("[ExamplePartIdConsumer] Could not load PartId.dll: " + ex);
                return false;
            }
        }

        static string FindPartIdDll()
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(Application.dataPath);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, "Mods", "PartId", "PartId.dll");
                    if (File.Exists(candidate))
                        return candidate;

                    if (dir.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                        return candidate;

                    dir = dir.Parent;
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
