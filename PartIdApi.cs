using System.Collections.Generic;
using UnityEngine;

namespace PartId
{
    public static class PartIdApi
    {
        public const int ApiVersion = 2;
        public const int CommonKeysVersion = 1;
        public const string PidField = "pid";
        public const string RecordFormat = "PartId.TypedRecords.v1";

        public static void Ensure()
        {
            PartIdRuntime.Ensure();
        }

        public static bool EnsureBlueprintPids(bool force = false)
        {
            Ensure();
            return PartIdRuntime.EnsureBlueprintPids(force);
        }

        public static bool TryGetPid(object part, out string pid)
        {
            Ensure();
            return PartIdRuntime.TryGetPidForPart(part, out pid, out _);
        }

        public static bool TryGetPidForPart(object part, out string pid, out string partKey)
        {
            Ensure();
            return PartIdRuntime.TryGetPidForPart(part, out pid, out partKey);
        }

        public static bool TryGetPartKey(object part, out string partKey)
        {
            Ensure();
            return PartIdRuntime.TryBuildPartKey(part, out partKey);
        }

        public static string GetRecordFilePath()
        {
            return PartIdRecordStore.GetRecordFilePath();
        }

        public static string[] GetSupportedValueTypes()
        {
            return PartIdValue.GetKnownTypes();
        }

        public static bool SupportsValueType(string type)
        {
            return PartIdValue.IsKnownType(type);
        }

        public static PartIdKeyDefinition[] GetCommonKeyDefinitions()
        {
            return PartIdCommonKeySchema.GetDefinitions();
        }

        public static PartIdKeyDefinition[] GetCommonKeyDefinitions(string category)
        {
            return PartIdCommonKeySchema.GetDefinitions(category);
        }

        public static bool TryGetCommonKeyDefinition(string key, out PartIdKeyDefinition definition)
        {
            return PartIdCommonKeySchema.TryGetDefinition(key, out definition);
        }

        public static string[] GetCommonKeyValueTypes(string key)
        {
            return PartIdCommonKeySchema.GetValueTypes(key);
        }

        public static bool SetValue(object part, string owner, string key, PartIdValue value)
        {
            Ensure();
            if (!PartIdRuntime.TryGetPidForPart(part, out string pid, out _))
                return false;

            return SetValue(pid, owner, key, value);
        }

        public static bool SetValue(string pid, string owner, string key, PartIdValue value)
        {
            return PartIdRecordStore.SetValue(pid, owner, key, value);
        }

        public static bool TryGetValue(object part, string owner, string key, out PartIdValue value)
        {
            Ensure();
            if (!PartIdRuntime.TryGetPidForPart(part, out string pid, out _))
            {
                value = null;
                return false;
            }

            return TryGetValue(pid, owner, key, out value);
        }

        public static bool TryGetValue(string pid, string owner, string key, out PartIdValue value)
        {
            return PartIdRecordStore.TryGetValue(pid, owner, key, out value);
        }

        public static bool RemoveValue(object part, string owner, string key)
        {
            Ensure();
            if (!PartIdRuntime.TryGetPidForPart(part, out string pid, out _))
                return false;

            return RemoveValue(pid, owner, key);
        }

        public static bool RemoveValue(string pid, string owner, string key)
        {
            return PartIdRecordStore.RemoveValue(pid, owner, key);
        }

        public static bool SetBool(object part, string owner, string key, bool value)
        {
            return SetValue(part, owner, key, PartIdValue.FromBool(value));
        }

        public static bool SetBool(string pid, string owner, string key, bool value)
        {
            return SetValue(pid, owner, key, PartIdValue.FromBool(value));
        }

        public static bool TryGetBool(object part, string owner, string key, out bool value)
        {
            if (TryGetValue(part, owner, key, out PartIdValue record) && record.TryAsBool(out value))
                return true;

            value = false;
            return false;
        }

        public static bool TryGetBool(string pid, string owner, string key, out bool value)
        {
            if (TryGetValue(pid, owner, key, out PartIdValue record) && record.TryAsBool(out value))
                return true;

            value = false;
            return false;
        }

        public static bool SetInt(object part, string owner, string key, int value)
        {
            return SetValue(part, owner, key, PartIdValue.FromInt(value));
        }

        public static bool SetInt(string pid, string owner, string key, int value)
        {
            return SetValue(pid, owner, key, PartIdValue.FromInt(value));
        }

        public static bool TryGetInt(object part, string owner, string key, out int value)
        {
            if (TryGetValue(part, owner, key, out PartIdValue record) && record.TryAsInt(out value))
                return true;

            value = 0;
            return false;
        }

        public static bool TryGetInt(string pid, string owner, string key, out int value)
        {
            if (TryGetValue(pid, owner, key, out PartIdValue record) && record.TryAsInt(out value))
                return true;

            value = 0;
            return false;
        }

        public static bool SetLong(object part, string owner, string key, long value)
        {
            return SetValue(part, owner, key, PartIdValue.FromLong(value));
        }

        public static bool SetLong(string pid, string owner, string key, long value)
        {
            return SetValue(pid, owner, key, PartIdValue.FromLong(value));
        }

        public static bool TryGetLong(object part, string owner, string key, out long value)
        {
            if (TryGetValue(part, owner, key, out PartIdValue record) && record.TryAsLong(out value))
                return true;

            value = 0L;
            return false;
        }

        public static bool TryGetLong(string pid, string owner, string key, out long value)
        {
            if (TryGetValue(pid, owner, key, out PartIdValue record) && record.TryAsLong(out value))
                return true;

            value = 0L;
            return false;
        }

        public static bool SetFloat(object part, string owner, string key, float value)
        {
            return SetValue(part, owner, key, PartIdValue.FromFloat(value));
        }

        public static bool SetFloat(string pid, string owner, string key, float value)
        {
            return SetValue(pid, owner, key, PartIdValue.FromFloat(value));
        }

        public static bool TryGetFloat(object part, string owner, string key, out float value)
        {
            if (TryGetValue(part, owner, key, out PartIdValue record) && record.TryAsFloat(out value))
                return true;

            value = 0f;
            return false;
        }

        public static bool TryGetFloat(string pid, string owner, string key, out float value)
        {
            if (TryGetValue(pid, owner, key, out PartIdValue record) && record.TryAsFloat(out value))
                return true;

            value = 0f;
            return false;
        }

        public static bool SetDouble(object part, string owner, string key, double value)
        {
            return SetValue(part, owner, key, PartIdValue.FromDouble(value));
        }

        public static bool SetDouble(string pid, string owner, string key, double value)
        {
            return SetValue(pid, owner, key, PartIdValue.FromDouble(value));
        }

        public static bool TryGetDouble(object part, string owner, string key, out double value)
        {
            if (TryGetValue(part, owner, key, out PartIdValue record) && record.TryAsDouble(out value))
                return true;

            value = 0d;
            return false;
        }

        public static bool TryGetDouble(string pid, string owner, string key, out double value)
        {
            if (TryGetValue(pid, owner, key, out PartIdValue record) && record.TryAsDouble(out value))
                return true;

            value = 0d;
            return false;
        }

        public static bool SetString(object part, string owner, string key, string value)
        {
            return SetValue(part, owner, key, PartIdValue.FromString(value));
        }

        public static bool SetString(string pid, string owner, string key, string value)
        {
            return SetValue(pid, owner, key, PartIdValue.FromString(value));
        }

        public static bool TryGetString(object part, string owner, string key, out string value)
        {
            if (TryGetValue(part, owner, key, out PartIdValue record) && record.TryAsString(out value))
                return true;

            value = "";
            return false;
        }

        public static bool TryGetString(string pid, string owner, string key, out string value)
        {
            if (TryGetValue(pid, owner, key, out PartIdValue record) && record.TryAsString(out value))
                return true;

            value = "";
            return false;
        }

        public static bool SetJson(object part, string owner, string key, string json)
        {
            return SetValue(part, owner, key, PartIdValue.FromJson(json));
        }

        public static bool SetJson(string pid, string owner, string key, string json)
        {
            return SetValue(pid, owner, key, PartIdValue.FromJson(json));
        }

        public static bool TryGetJson(object part, string owner, string key, out string json)
        {
            if (TryGetValue(part, owner, key, out PartIdValue record) && record.TryAsJson(out json))
                return true;

            json = "";
            return false;
        }

        public static bool TryGetJson(string pid, string owner, string key, out string json)
        {
            if (TryGetValue(pid, owner, key, out PartIdValue record) && record.TryAsJson(out json))
                return true;

            json = "";
            return false;
        }

        public static bool SetVector2(object part, string owner, string key, Vector2 value)
        {
            return SetValue(part, owner, key, PartIdValue.FromVector2(value));
        }

        public static bool SetVector2(string pid, string owner, string key, Vector2 value)
        {
            return SetValue(pid, owner, key, PartIdValue.FromVector2(value));
        }

        public static bool TryGetVector2(object part, string owner, string key, out Vector2 value)
        {
            if (TryGetValue(part, owner, key, out PartIdValue record) && record.TryAsVector2(out value))
                return true;

            value = Vector2.zero;
            return false;
        }

        public static bool TryGetVector2(string pid, string owner, string key, out Vector2 value)
        {
            if (TryGetValue(pid, owner, key, out PartIdValue record) && record.TryAsVector2(out value))
                return true;

            value = Vector2.zero;
            return false;
        }

        public static bool SetVector3(object part, string owner, string key, Vector3 value)
        {
            return SetValue(part, owner, key, PartIdValue.FromVector3(value));
        }

        public static bool SetVector3(string pid, string owner, string key, Vector3 value)
        {
            return SetValue(pid, owner, key, PartIdValue.FromVector3(value));
        }

        public static bool TryGetVector3(object part, string owner, string key, out Vector3 value)
        {
            if (TryGetValue(part, owner, key, out PartIdValue record) && record.TryAsVector3(out value))
                return true;

            value = Vector3.zero;
            return false;
        }

        public static bool TryGetVector3(string pid, string owner, string key, out Vector3 value)
        {
            if (TryGetValue(pid, owner, key, out PartIdValue record) && record.TryAsVector3(out value))
                return true;

            value = Vector3.zero;
            return false;
        }

        public static bool SetColor(object part, string owner, string key, Color value)
        {
            return SetValue(part, owner, key, PartIdValue.FromColor(value));
        }

        public static bool SetColor(string pid, string owner, string key, Color value)
        {
            return SetValue(pid, owner, key, PartIdValue.FromColor(value));
        }

        public static bool TryGetColor(object part, string owner, string key, out Color value)
        {
            if (TryGetValue(part, owner, key, out PartIdValue record) && record.TryAsColor(out value))
                return true;

            value = Color.white;
            return false;
        }

        public static bool TryGetColor(string pid, string owner, string key, out Color value)
        {
            if (TryGetValue(pid, owner, key, out PartIdValue record) && record.TryAsColor(out value))
                return true;

            value = Color.white;
            return false;
        }

        public static bool SetBytes(object part, string owner, string key, byte[] value)
        {
            return SetValue(part, owner, key, PartIdValue.FromBytes(value));
        }

        public static bool SetBytes(string pid, string owner, string key, byte[] value)
        {
            return SetValue(pid, owner, key, PartIdValue.FromBytes(value));
        }

        public static bool TryGetBytes(object part, string owner, string key, out byte[] value)
        {
            if (TryGetValue(part, owner, key, out PartIdValue record) && record.TryAsBytes(out value))
                return true;

            value = new byte[0];
            return false;
        }

        public static bool TryGetBytes(string pid, string owner, string key, out byte[] value)
        {
            if (TryGetValue(pid, owner, key, out PartIdValue record) && record.TryAsBytes(out value))
                return true;

            value = new byte[0];
            return false;
        }

        public static Dictionary<string, PartIdValue> GetRecordsForPid(string pid, string owner = null)
        {
            return PartIdRecordStore.GetRecordsForPid(pid, owner);
        }

        public static List<PartIdRecord> GetRecordsForPidList(string pid, string owner = null)
        {
            return PartIdRecordStore.GetRecordsForPidList(pid, owner);
        }

        public static List<PartIdRecord> GetRecordsForOwner(string owner)
        {
            return PartIdRecordStore.GetRecordsForOwner(owner);
        }

        public static List<PartIdRecord> GetAllRecords()
        {
            return PartIdRecordStore.GetAllRecords();
        }
    }
}
