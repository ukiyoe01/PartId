using System;
using System.Reflection;
using PartId;
using UnityEngine;

class VerifyPartIdValues
{
    static int failures;

    static int Main()
    {
        CheckBool();
        CheckIntegers();
        CheckFloatingPoint();
        CheckText();
        CheckUnityTypes();
        CheckBytes();
        CheckKnownTypes();
        CheckCapabilities();
        CheckCommonKeys();
        CheckCommonKeySchema();
        CheckRecordModel();
        CheckBlueprintPidCleanup();

        if (failures == 0)
        {
            Console.WriteLine("PartId value format verification passed.");
            return 0;
        }

        Console.Error.WriteLine("PartId value format verification failed: " + failures);
        return 1;
    }

    static void CheckBool()
    {
        var value = PartIdValue.FromBool(true);
        Expect(value.Type == PartIdValue.Bool, "bool type");
        Expect(value.Payload == "true", "bool payload");
        Expect(value.TryAsBool(out bool read) && read, "bool read");
    }

    static void CheckIntegers()
    {
        var intValue = PartIdValue.FromInt(-12);
        Expect(intValue.Payload == "-12", "int payload");
        Expect(intValue.TryAsInt(out int i) && i == -12, "int read");
        Expect(intValue.TryAsLong(out long l) && l == -12L, "int reads as long");
        Expect(intValue.TryAsDouble(out double d) && Nearly(d, -12d), "int reads as double");

        var longValue = PartIdValue.FromLong(9007199254740991L);
        Expect(longValue.TryAsLong(out long big) && big == 9007199254740991L, "long read");
        Expect(!longValue.TryAsInt(out _), "long does not read as int");
    }

    static void CheckFloatingPoint()
    {
        var zero = PartIdValue.FromDouble(0d);
        Expect(zero.TryAsDouble(out double z) && Nearly(z, 0d), "double zero");

        var negative = PartIdValue.FromDouble(-1d);
        Expect(negative.Payload == "-1", "negative double payload");
        Expect(negative.TryAsDouble(out double n) && Nearly(n, -1d), "negative double read");

        var floatValue = PartIdValue.FromFloat(1.25f);
        Expect(floatValue.TryAsFloat(out float f) && Nearly(f, 1.25f), "float read");
        Expect(floatValue.TryAsDouble(out double d) && Nearly(d, 1.25d), "float reads as double");
        Expect(!floatValue.TryAsInt(out _), "float does not read as int");
    }

    static void CheckText()
    {
        const string text = "中文 owner/key payload";
        var value = PartIdValue.FromString(text);
        Expect(value.Type == PartIdValue.String, "string type");
        Expect(value.TryAsString(out string read) && read == text, "string UTF-8 base64 read");
        Expect(!value.TryAsJson(out _), "string does not read as json");

        const string json = "{\"mode\":\"demo\",\"mass\":-1}";
        var jsonValue = PartIdValue.FromJson(json);
        Expect(jsonValue.Type == PartIdValue.Json, "json type");
        Expect(jsonValue.TryAsJson(out string jsonRead) && jsonRead == json, "json UTF-8 base64 read");
        Expect(!jsonValue.TryAsString(out _), "json does not read as string");
    }

    static void CheckUnityTypes()
    {
        var vec2 = PartIdValue.FromVector2(new Vector2(-1.5f, 2.25f));
        Expect(vec2.Payload == "-1.5,2.25", "vec2 payload");
        Expect(vec2.TryAsVector2(out Vector2 v2) && Nearly(v2.x, -1.5f) && Nearly(v2.y, 2.25f), "vec2 read");

        var vec3 = PartIdValue.FromVector3(new Vector3(1f, 0f, -3f));
        Expect(vec3.TryAsVector3(out Vector3 v3) && Nearly(v3.x, 1f) && Nearly(v3.y, 0f) && Nearly(v3.z, -3f), "vec3 read");

        var color = PartIdValue.FromColor(new Color(0.1f, 0.2f, 0.3f, 0.4f));
        Expect(color.TryAsColor(out Color c) && Nearly(c.r, 0.1f) && Nearly(c.g, 0.2f) && Nearly(c.b, 0.3f) && Nearly(c.a, 0.4f), "color read");
    }

    static void CheckBytes()
    {
        byte[] bytes = { 0, 1, 2, 255 };
        var value = PartIdValue.FromBytes(bytes);
        Expect(value.TryAsBytes(out byte[] read) && read.Length == 4 && read[0] == 0 && read[3] == 255, "bytes read");
    }

    static void CheckKnownTypes()
    {
        Expect(PartIdValue.IsKnownType("DOUBLE"), "known type normalization");
        Expect(PartIdValue.IsKnownType(PartIdValue.Vector2), "known vec2");
        Expect(!PartIdValue.IsKnownType("quat"), "unknown type rejection");
    }

    static void CheckCapabilities()
    {
        Expect(PartIdApi.ApiVersion == 2, "api version");
        Expect(PartIdApi.CommonKeysVersion == 1, "common keys version");
        Expect(PartIdApi.RecordFormat == "PartId.TypedRecords.v1", "record format id");
        Expect(PartIdApi.SupportsValueType("json"), "supports json");
        Expect(PartIdApi.SupportsValueType("COLOR"), "supports normalized color");
        Expect(!PartIdApi.SupportsValueType("quat"), "does not support quat");

        string[] types = PartIdApi.GetSupportedValueTypes();
        Expect(types.Length == 11, "supported type count");
        Expect(Array.IndexOf(types, PartIdValue.Double) >= 0, "supported double listed");
        Expect(Array.IndexOf(types, PartIdValue.Bytes) >= 0, "supported bytes listed");
    }

    static void CheckCommonKeys()
    {
        Expect(PartIdKeys.Common.Enabled == "enabled", "common enabled key");
        Expect(PartIdKeys.Physics.Mass == "mass", "physics mass key");
        Expect(PartIdKeys.Transform.Polygon == "polygon", "geometry polygon key");
        Expect(PartIdKeys.Visual.RenderOrder == "render_order", "visual render order key");
        Expect(PartIdKeys.Build.SymmetryGroup == "symmetry_group", "build symmetry group key");
    }

    static void CheckCommonKeySchema()
    {
        PartIdKeyDefinition[] all = PartIdApi.GetCommonKeyDefinitions();
        Expect(all.Length == 35, "common key definition count");

        PartIdKeyDefinition[] physics = PartIdApi.GetCommonKeyDefinitions("physics");
        Expect(physics.Length == 9, "physics key definition count");

        Expect(PartIdApi.TryGetCommonKeyDefinition(PartIdKeys.Physics.Mass, out PartIdKeyDefinition mass), "mass definition exists");
        Expect(mass != null && mass.Category == "Physics", "mass definition category");
        Expect(mass != null && mass.PrimaryType == PartIdValue.Double, "mass primary type");
        Expect(mass != null && mass.SupportsType("DOUBLE"), "mass supports normalized double");
        Expect(mass != null && !mass.SupportsType(PartIdValue.Color), "mass rejects color");
        string[] massTypes = mass.ValueTypes;
        massTypes[0] = PartIdValue.String;
        Expect(mass.PrimaryType == PartIdValue.Double, "definition value types are copied");

        string[] offsetTypes = PartIdApi.GetCommonKeyValueTypes(PartIdKeys.Transform.Offset);
        Expect(offsetTypes.Length == 2, "offset type count");
        Expect(Array.IndexOf(offsetTypes, PartIdValue.Vector2) >= 0, "offset supports vec2");
        Expect(Array.IndexOf(offsetTypes, PartIdValue.Vector3) >= 0, "offset supports vec3");

        Expect(!PartIdApi.TryGetCommonKeyDefinition("unknown_key", out _), "unknown common key missing");
    }

    static void CheckRecordModel()
    {
        var record = new PartIdRecord("pid_abc", "MyMod", "mass", PartIdValue.FromDouble(-1d));
        Expect(record.Pid == "pid_abc", "record pid");
        Expect(record.Owner == "MyMod", "record owner");
        Expect(record.Key == "mass", "record key");
        Expect(record.CompositeName == "MyMod/mass", "record composite name");
        Expect(record.Value.TryAsDouble(out double value) && Nearly(value, -1d), "record value");

        Expect(typeof(PartIdApi).GetMethod("GetRecordsForPidList") != null, "GetRecordsForPidList API");
        Expect(typeof(PartIdApi).GetMethod("GetRecordsForOwner") != null, "GetRecordsForOwner API");
        Expect(typeof(PartIdApi).GetMethod("GetAllRecords") != null, "GetAllRecords API");
    }

    static void CheckBlueprintPidCleanup()
    {
        Type runtime = typeof(PartIdRuntime);
        MethodInfo clean = runtime.GetMethod("CleanPidFields", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo add = runtime.GetMethod("AddOrReplacePid", BindingFlags.NonPublic | BindingFlags.Static);

        string first = (string)clean.Invoke(null, new object[] { "{\"pid\":\"pid_old\",\"n\":\"Capsule\",\"p\":{\"x\":0,\"y\":0}}" });
        Expect(!first.Contains("{,"), "clean pid when first field");
        Expect(!first.Contains("\"pid\""), "clean removes pid field");

        string middle = (string)clean.Invoke(null, new object[] { "{\"n\":\"Capsule\",\"pid\":\"pid_old\",\"p\":{\"x\":0,\"y\":0}}" });
        Expect(!middle.Contains(",,") && !middle.Contains(",}"), "clean pid when middle field");

        string legacy = (string)clean.Invoke(null, new object[] { "{\"n\":\"Capsule\",\"amm_id\":\"amm_old\",\"pid_id\":\"pid_bad\",\"p\":{\"x\":0,\"y\":0}}" });
        Expect(!legacy.Contains("amm_id") && !legacy.Contains("pid_id"), "clean legacy pid fields");

        string added = (string)add.Invoke(null, new object[] { "{\"pid\":\"pid_old\",\"n\":\"Capsule\",\"p\":{\"x\":0,\"y\":0}}", "pid_new" });
        Expect(added.Contains("\"pid\": \"pid_new\""), "add replacement pid");
        Expect(!added.Contains("pid_old"), "old pid removed before replacement");
    }

    static bool Nearly(double left, double right)
    {
        return Math.Abs(left - right) < 0.00001d;
    }

    static void Expect(bool condition, string name)
    {
        if (condition)
            return;

        failures++;
        Console.Error.WriteLine("FAIL: " + name);
    }
}
