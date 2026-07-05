using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace PartId
{
    public sealed class PartIdValue
    {
        public const string Bool = "bool";
        public const string Int = "int";
        public const string Long = "long";
        public const string Float = "float";
        public const string Double = "double";
        public const string String = "string";
        public const string Json = "json";
        public const string Vector2 = "vec2";
        public const string Vector3 = "vec3";
        public const string Color = "color";
        public const string Bytes = "bytes";

        public string Type { get; private set; }
        public string Payload { get; private set; }

        public PartIdValue(string type, string payload)
        {
            Type = NormalizeType(type);
            Payload = payload ?? "";
        }

        public static PartIdValue FromBool(bool value) => new PartIdValue(Bool, value ? "true" : "false");
        public static PartIdValue FromInt(int value) => new PartIdValue(Int, value.ToString(CultureInfo.InvariantCulture));
        public static PartIdValue FromLong(long value) => new PartIdValue(Long, value.ToString(CultureInfo.InvariantCulture));
        public static PartIdValue FromFloat(float value) => new PartIdValue(Float, value.ToString("R", CultureInfo.InvariantCulture));
        public static PartIdValue FromDouble(double value) => new PartIdValue(Double, value.ToString("R", CultureInfo.InvariantCulture));
        public static PartIdValue FromString(string value) => new PartIdValue(String, ToBase64(value ?? ""));
        public static PartIdValue FromJson(string json) => new PartIdValue(Json, ToBase64(json ?? ""));
        public static PartIdValue FromBytes(byte[] bytes) => new PartIdValue(Bytes, Convert.ToBase64String(bytes ?? new byte[0]));

        public static PartIdValue FromVector2(Vector2 value)
        {
            return new PartIdValue(Vector2, Join(value.x, value.y));
        }

        public static PartIdValue FromVector3(Vector3 value)
        {
            return new PartIdValue(Vector3, Join(value.x, value.y, value.z));
        }

        public static PartIdValue FromColor(Color value)
        {
            return new PartIdValue(Color, Join(value.r, value.g, value.b, value.a));
        }

        public bool TryAsBool(out bool value)
        {
            if (string.Equals(Type, Bool, StringComparison.OrdinalIgnoreCase) && bool.TryParse(Payload, out value))
                return true;

            value = false;
            return false;
        }

        public bool TryAsInt(out int value)
        {
            if (Type == Int && int.TryParse(Payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            value = 0;
            return false;
        }

        public bool TryAsLong(out long value)
        {
            if ((Type == Long || Type == Int) &&
                long.TryParse(Payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            value = 0L;
            return false;
        }

        public bool TryAsFloat(out float value)
        {
            if ((Type == Float || Type == Double || Type == Int || Type == Long) &&
                float.TryParse(Payload, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            value = 0f;
            return false;
        }

        public bool TryAsDouble(out double value)
        {
            if ((Type == Double || Type == Float || Type == Int || Type == Long) &&
                double.TryParse(Payload, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            value = 0d;
            return false;
        }

        public bool TryAsString(out string value)
        {
            if (Type == String)
                return FromBase64(Payload, out value);

            value = "";
            return false;
        }

        public bool TryAsJson(out string value)
        {
            if (Type == Json)
                return FromBase64(Payload, out value);

            value = "";
            return false;
        }

        public bool TryAsVector2(out Vector2 value)
        {
            if (Type == Vector2 && TryReadFloats(Payload, 2, out float[] values))
            {
                value = new Vector2(values[0], values[1]);
                return true;
            }

            value = UnityEngine.Vector2.zero;
            return false;
        }

        public bool TryAsVector3(out Vector3 value)
        {
            if (Type == Vector3 && TryReadFloats(Payload, 3, out float[] values))
            {
                value = new Vector3(values[0], values[1], values[2]);
                return true;
            }

            value = UnityEngine.Vector3.zero;
            return false;
        }

        public bool TryAsColor(out Color value)
        {
            if (Type == Color && TryReadFloats(Payload, 4, out float[] values))
            {
                value = new Color(values[0], values[1], values[2], values[3]);
                return true;
            }

            value = UnityEngine.Color.white;
            return false;
        }

        public bool TryAsBytes(out byte[] bytes)
        {
            if (Type != Bytes)
            {
                bytes = new byte[0];
                return false;
            }

            try
            {
                bytes = Convert.FromBase64String(Payload);
                return true;
            }
            catch
            {
                bytes = new byte[0];
                return false;
            }
        }

        public static bool IsKnownType(string type)
        {
            string normalized = NormalizeType(type);
            return normalized == Bool || normalized == Int || normalized == Long ||
                   normalized == Float || normalized == Double || normalized == String ||
                   normalized == Json || normalized == Vector2 || normalized == Vector3 ||
                   normalized == Color || normalized == Bytes;
        }

        public static string[] GetKnownTypes()
        {
            return new[]
            {
                Bool,
                Int,
                Long,
                Float,
                Double,
                String,
                Json,
                Vector2,
                Vector3,
                Color,
                Bytes
            };
        }

        public static string NormalizeType(string type)
        {
            return string.IsNullOrWhiteSpace(type)
                ? String
                : type.Trim().ToLowerInvariant();
        }

        static string Join(params float[] values)
        {
            string[] pieces = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                pieces[i] = values[i].ToString("R", CultureInfo.InvariantCulture);

            return string.Join(",", pieces);
        }

        static bool TryReadFloats(string payload, int count, out float[] values)
        {
            values = new float[count];
            string[] pieces = (payload ?? "").Split(',');
            if (pieces.Length != count)
                return false;

            for (int i = 0; i < pieces.Length; i++)
            {
                if (!float.TryParse(pieces[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    return false;
            }

            return true;
        }

        public static string ToBase64(string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? ""));
        }

        public static bool FromBase64(string text, out string value)
        {
            try
            {
                value = Encoding.UTF8.GetString(Convert.FromBase64String(text ?? ""));
                return true;
            }
            catch
            {
                value = "";
                return false;
            }
        }
    }
}
