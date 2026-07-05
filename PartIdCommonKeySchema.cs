using System;
using System.Collections.Generic;

namespace PartId
{
    static class PartIdCommonKeySchema
    {
        static readonly PartIdKeyDefinition[] definitions =
        {
            Def("Common", PartIdKeys.Common.Enabled, PartIdValue.Bool, "Whether this mod's behavior is enabled for the part."),
            Def("Common", PartIdKeys.Common.Label, PartIdValue.String, "Human-facing short name."),
            Def("Common", PartIdKeys.Common.Notes, PartIdValue.String, "Human-facing longer note."),
            Def("Common", PartIdKeys.Common.Settings, PartIdValue.Json, "Small structured settings owned by one mod."),
            Def("Common", PartIdKeys.Common.Version, PartIdValue.Int, "Schema version for one mod's per-part data."),

            Def("Physics", PartIdKeys.Physics.Mass, PartIdValue.Double, "Override or target mass in the same unit the game mass field uses."),
            Def("Physics", PartIdKeys.Physics.DryMass, PartIdValue.Double, "Mass without fuel or consumables."),
            Def("Physics", PartIdKeys.Physics.WetMass, PartIdValue.Double, "Mass with fuel or consumables."),
            Def("Physics", PartIdKeys.Physics.Thrust, PartIdValue.Double, "Thrust value in the same unit the game exposes for the target part."),
            Def("Physics", PartIdKeys.Physics.Isp, PartIdValue.Double, "Specific impulse or efficiency value."),
            Def("Physics", PartIdKeys.Physics.FuelCapacity, PartIdValue.Double, "Fuel capacity in the same unit the target fuel module uses."),
            Def("Physics", PartIdKeys.Physics.DragMultiplier, PartIdValue.Double, "Multiplier applied to stock drag. 1 means no change."),
            Def("Physics", PartIdKeys.Physics.Torque, PartIdValue.Double, "Torque strength in the same unit the target part uses."),
            Def("Physics", PartIdKeys.Physics.CollisionEnabled, PartIdValue.Bool, "Whether the part should participate in collision."),

            Def("Transform", PartIdKeys.Transform.Offset, new[] { PartIdValue.Vector2, PartIdValue.Vector3 }, "Offset relative to the part's normal local position."),
            Def("Transform", PartIdKeys.Transform.Position, new[] { PartIdValue.Vector2, PartIdValue.Vector3 }, "Absolute or replacement local position, if a mod defines one."),
            Def("Transform", PartIdKeys.Transform.Scale, new[] { PartIdValue.Vector2, PartIdValue.Vector3 }, "Visual or logical scale."),
            Def("Transform", PartIdKeys.Transform.Rotation, new[] { PartIdValue.Float, PartIdValue.Vector3 }, "2D angle or 3D Euler-style rotation."),
            Def("Transform", PartIdKeys.Transform.Size, new[] { PartIdValue.Vector2, PartIdValue.Vector3 }, "Bounds, panel size, or generated geometry size."),
            Def("Transform", PartIdKeys.Transform.Radius, PartIdValue.Float, "Radius for circular geometry or influence."),
            Def("Transform", PartIdKeys.Transform.Polygon, PartIdValue.Json, "Polygon, collider, or custom point list."),

            Def("Visual", PartIdKeys.Visual.Visible, PartIdValue.Bool, "Whether this mod should render the part or overlay."),
            Def("Visual", PartIdKeys.Visual.Color, PartIdValue.Color, "Primary RGBA color."),
            Def("Visual", PartIdKeys.Visual.SecondaryColor, PartIdValue.Color, "Secondary RGBA color."),
            Def("Visual", PartIdKeys.Visual.Alpha, PartIdValue.Float, "Opacity multiplier. 1 means fully opaque."),
            Def("Visual", PartIdKeys.Visual.Texture, PartIdValue.String, "Texture id, path, or asset key."),
            Def("Visual", PartIdKeys.Visual.Sprite, PartIdValue.String, "Sprite id, path, or asset key."),
            Def("Visual", PartIdKeys.Visual.RenderOrder, PartIdValue.Int, "Ordering hint for visual layering."),

            Def("Build", PartIdKeys.Build.Category, PartIdValue.String, "Build menu or mod category."),
            Def("Build", PartIdKeys.Build.Variant, PartIdValue.String, "Variant id selected for this part."),
            Def("Build", PartIdKeys.Build.Stage, PartIdValue.Int, "Stage, sequence, or ordering value."),
            Def("Build", PartIdKeys.Build.Group, PartIdValue.String, "Mod-defined group name."),
            Def("Build", PartIdKeys.Build.Tags, PartIdValue.Json, "Small list or object of tags."),
            Def("Build", PartIdKeys.Build.AttachEnabled, PartIdValue.Bool, "Whether custom attachment logic is enabled."),
            Def("Build", PartIdKeys.Build.SymmetryGroup, PartIdValue.String, "Identifier linking mirrored or repeated parts.")
        };

        public static PartIdKeyDefinition[] GetDefinitions()
        {
            return (PartIdKeyDefinition[])definitions.Clone();
        }

        public static PartIdKeyDefinition[] GetDefinitions(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return GetDefinitions();

            var output = new List<PartIdKeyDefinition>();
            foreach (PartIdKeyDefinition definition in definitions)
            {
                if (string.Equals(definition.Category, category.Trim(), StringComparison.OrdinalIgnoreCase))
                    output.Add(definition);
            }

            return output.ToArray();
        }

        public static bool TryGetDefinition(string key, out PartIdKeyDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            string normalizedKey = key.Trim();
            foreach (PartIdKeyDefinition candidate in definitions)
            {
                if (string.Equals(candidate.Key, normalizedKey, StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }

        public static string[] GetValueTypes(string key)
        {
            return TryGetDefinition(key, out PartIdKeyDefinition definition)
                ? (string[])definition.ValueTypes.Clone()
                : new string[0];
        }

        static PartIdKeyDefinition Def(string category, string key, string valueType, string meaning)
        {
            return new PartIdKeyDefinition(category, key, new[] { valueType }, meaning);
        }

        static PartIdKeyDefinition Def(string category, string key, string[] valueTypes, string meaning)
        {
            return new PartIdKeyDefinition(category, key, valueTypes, meaning);
        }
    }
}
