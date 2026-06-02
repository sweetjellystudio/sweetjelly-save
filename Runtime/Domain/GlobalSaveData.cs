using System;

namespace SWeetJelly.Save.Domain
{
    [Serializable]
    public sealed class GlobalSaveData
    {
        public string BuildId { get; set; } = string.Empty;

        public int LevelId { get; set; }

        public SavedDataContainer SavedData { get; set; } = new SavedDataContainer();

        public float SavedGameTime { get; set; }

        public DateTime LastExitTime { get; set; }

        [NonSerialized]
        private float currentTime;

        [NonSerialized]
        private float lastFlushTime;

        [NonSerialized]
        private bool isInitialised;

        public float GameTime => SavedGameTime + (currentTime - lastFlushTime);

        public void Initialise(float time)
        {
            currentTime = time;
            lastFlushTime = time;
            isInitialised = true;

            if (SavedData == null)
                SavedData = new SavedDataContainer();
        }

        public void SetTime(float time)
        {
            EnsureInitialised();

            currentTime = time;
        }

        public void SetSavedData(SavedDataContainer savedData)
        {
            SavedData = savedData ?? new SavedDataContainer();
        }

        public void Flush(float time, DateTime exitTime)
        {
            EnsureInitialised();

            currentTime = time;

            SavedGameTime += currentTime - lastFlushTime;
            lastFlushTime = currentTime;

            LastExitTime = exitTime;

            if (SavedData == null)
                SavedData = new SavedDataContainer();
        }

        private void EnsureInitialised()
        {
            if (!isInitialised)
                Initialise(currentTime);
        }
    }
}