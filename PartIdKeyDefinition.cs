namespace PartId
{
    public sealed class PartIdKeyDefinition
    {
        readonly string[] valueTypes;

        public string Category { get; private set; }
        public string Key { get; private set; }
        public string[] ValueTypes => (string[])valueTypes.Clone();
        public string Meaning { get; private set; }

        public string PrimaryType => valueTypes.Length > 0 ? valueTypes[0] : "";

        public PartIdKeyDefinition(string category, string key, string[] valueTypes, string meaning)
        {
            Category = category ?? "";
            Key = key ?? "";
            this.valueTypes = valueTypes != null ? (string[])valueTypes.Clone() : new string[0];
            Meaning = meaning ?? "";
        }

        public bool SupportsType(string type)
        {
            string normalized = PartIdValue.NormalizeType(type);
            foreach (string valueType in valueTypes)
            {
                if (PartIdValue.NormalizeType(valueType) == normalized)
                    return true;
            }

            return false;
        }
    }
}
