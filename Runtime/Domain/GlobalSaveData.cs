using System;
namespace SWeetJelly.Save.Domain
{
    [Serializable]
    public sealed class GlobalSaveData
    {
        public SavedDataContainer SavedData { get; set; } = new SavedDataContainer();
        public void SetSavedData(SavedDataContainer data) => SavedData = data ?? new SavedDataContainer();
    }
}
