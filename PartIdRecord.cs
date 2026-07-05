namespace PartId
{
    public sealed class PartIdRecord
    {
        public string Pid { get; private set; }
        public string Owner { get; private set; }
        public string Key { get; private set; }
        public PartIdValue Value { get; private set; }

        public string CompositeName => Owner + "/" + Key;

        public PartIdRecord(string pid, string owner, string key, PartIdValue value)
        {
            Pid = pid ?? "";
            Owner = owner ?? "";
            Key = key ?? "";
            Value = value;
        }
    }
}
