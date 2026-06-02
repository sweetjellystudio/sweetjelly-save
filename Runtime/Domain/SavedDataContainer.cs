using System;
using System.Collections.Generic;

namespace SWeetJelly.Save.Domain
{
    [Serializable]
    public sealed class SavedDataContainer
    {
        public int Version { get; set; } = 1;

        public List<SavedDataEntry> Entries { get; set; } = new();
    }
}