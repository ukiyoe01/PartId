// Example only. Do not compile this file into PartId.dll.
// Copy the pattern into another mod that references PartId.dll.

using ModLoader;
using PartId;
using UnityEngine;

namespace ExamplePartIdConsumer
{
    public class Main : Mod
    {
        public override string ModNameID => "ExamplePartIdConsumer";
        public override string DisplayName => "Example PartId Consumer";
        public override string Author => "01";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "v0.1.0";
        public override string Description => "Small example showing how another mod can use PartId.";
        public override string IconLink => "";

        const string Owner = "ExamplePartIdConsumer";

        public override void Load()
        {
            PartIdApi.Ensure();
            Debug.Log("[ExamplePartIdConsumer] Loaded.");
        }

        public static void SaveExampleValues(object part)
        {
            if (!PartIdApi.TryGetPid(part, out string pid))
            {
                Debug.Log("[ExamplePartIdConsumer] Selected object has no PartId pid yet.");
                return;
            }

            PartIdApi.SetBool(pid, Owner, PartIdKeys.Common.Enabled, true);
            PartIdApi.SetDouble(pid, Owner, PartIdKeys.Physics.Mass, 1.25);
            PartIdApi.SetColor(pid, Owner, PartIdKeys.Visual.Color, Color.red);
            PartIdApi.SetJson(pid, Owner, PartIdKeys.Common.Settings, "{\"mode\":\"demo\",\"level\":2}");

            Debug.Log("[ExamplePartIdConsumer] Saved example records for " + pid + ".");
        }

        public static void ReadExampleValues(object part)
        {
            if (PartIdApi.TryGetBool(part, Owner, PartIdKeys.Common.Enabled, out bool enabled))
                Debug.Log("[ExamplePartIdConsumer] enabled = " + enabled);

            if (PartIdApi.TryGetDouble(part, Owner, PartIdKeys.Physics.Mass, out double mass))
                Debug.Log("[ExamplePartIdConsumer] mass = " + mass);

            if (PartIdApi.TryGetColor(part, Owner, PartIdKeys.Visual.Color, out Color color))
                Debug.Log("[ExamplePartIdConsumer] color = " + color);

            if (PartIdApi.TryGetJson(part, Owner, PartIdKeys.Common.Settings, out string json))
                Debug.Log("[ExamplePartIdConsumer] settings = " + json);
        }

        public static void ClearExampleValues(object part)
        {
            PartIdApi.RemoveValue(part, Owner, PartIdKeys.Common.Enabled);
            PartIdApi.RemoveValue(part, Owner, PartIdKeys.Physics.Mass);
            PartIdApi.RemoveValue(part, Owner, PartIdKeys.Visual.Color);
            PartIdApi.RemoveValue(part, Owner, PartIdKeys.Common.Settings);
        }
    }
}
