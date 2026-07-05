namespace PartId
{
    public static class PartIdKeys
    {
        public static class Common
        {
            public const string Enabled = "enabled";
            public const string Label = "label";
            public const string Notes = "notes";
            public const string Settings = "settings";
            public const string Version = "version";
        }

        public static class Physics
        {
            public const string Mass = "mass";
            public const string DryMass = "dry_mass";
            public const string WetMass = "wet_mass";
            public const string Thrust = "thrust";
            public const string Isp = "isp";
            public const string FuelCapacity = "fuel_capacity";
            public const string DragMultiplier = "drag_multiplier";
            public const string Torque = "torque";
            public const string CollisionEnabled = "collision_enabled";
        }

        public static class Transform
        {
            public const string Offset = "offset";
            public const string Position = "position";
            public const string Scale = "scale";
            public const string Rotation = "rotation";
            public const string Size = "size";
            public const string Radius = "radius";
            public const string Polygon = "polygon";
        }

        public static class Visual
        {
            public const string Visible = "visible";
            public const string Color = "color";
            public const string SecondaryColor = "secondary_color";
            public const string Alpha = "alpha";
            public const string Texture = "texture";
            public const string Sprite = "sprite";
            public const string RenderOrder = "render_order";
        }

        public static class Build
        {
            public const string Category = "category";
            public const string Variant = "variant";
            public const string Stage = "stage";
            public const string Group = "group";
            public const string Tags = "tags";
            public const string AttachEnabled = "attach_enabled";
            public const string SymmetryGroup = "symmetry_group";
        }
    }
}
