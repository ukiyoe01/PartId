using ModLoader;
using UnityEngine;

namespace PartId
{
    public class Main : Mod
    {
        public override string ModNameID => "PartId";
        public override string DisplayName => "SFS Part ID";
        public override string Author => "01";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "v1.0.0";
        public override string Description => "Stable part pid and typed per-part record storage for other mods.";
        public override string IconLink => "";

        public override void Load()
        {
            Debug.Log("[PartId] Loaded. Providing pid and typed record APIs.");
            PartIdApi.Ensure();
        }
    }
}
