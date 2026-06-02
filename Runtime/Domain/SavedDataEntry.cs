using System;

namespace SWeetJelly.Save.Domain
{
    [Serializable]
    public sealed class SavedDataEntry
    {
        public string Key { get; set; }

        public string TypeName { get; set; }

        public string SerializedValue { get; set; }

        public SavedDataEntry()
        {
        }

        public SavedDataEntry(
            string key,
            string typeName,
            string serializedValue)
        {
            Key = key;
            TypeName = typeName;
            SerializedValue = serializedValue;
        }
    }
}