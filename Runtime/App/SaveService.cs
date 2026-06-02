using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SWeetJelly.Save.Contract;
using SWeetJelly.Save.Domain;
using SWeetJelly.Serialization.Contract;

namespace SWeetJelly.Save.App
{
    public sealed class SaveService : ISaveService
    {
        private readonly ISerializationService serializer;
        private readonly string saveFileName;
        private readonly SerializationFormat saveFormat;
        private readonly SerializationFormat entryFormat;
        private readonly Func<DateTime> nowProvider;

        private readonly Dictionary<SaveKey, ISaveObject> saveObjects = new();
        private readonly Dictionary<SaveKey, SavedDataEntry> loadedEntries = new();

        private GlobalSaveData currentSave = new GlobalSaveData();
        private float currentTime;

        public bool IsLoaded { get; private set; }
        public bool IsDirty { get; private set; }

        public GlobalSaveData CurrentSave
        {
            get
            {
                EnsureLoaded();
                return currentSave;
            }
        }

        public int LevelId
        {
            get => CurrentSave.LevelId;
            set
            {
                CurrentSave.LevelId = value;
                MarkDirty();
            }
        }

        public float GameTime => CurrentSave.GameTime;
        public DateTime LastExitTime => CurrentSave.LastExitTime;

        public event EventHandler Loaded;
        public event EventHandler Saved;
        public event EventHandler Deleted;

        public SaveService(
            ISerializationService serializer,
            string saveFileName,
            SerializationFormat saveFormat = SerializationFormat.Json,
            SerializationFormat entryFormat = SerializationFormat.Json,
            Func<DateTime> nowProvider = null)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            if (string.IsNullOrWhiteSpace(saveFileName))
                throw new ArgumentException("Save file name cannot be null or whitespace.", nameof(saveFileName));

            this.saveFileName = saveFileName;
            this.saveFormat = saveFormat;
            this.entryFormat = entryFormat;
            this.nowProvider = nowProvider ?? (() => DateTime.Now);
        }

        public void Register(ISaveObject saveObject)
        {
            if (saveObject == null)
                throw new ArgumentNullException(nameof(saveObject));

            if (saveObject.Key.IsEmpty)
                throw new ArgumentException("Save object key cannot be empty.", nameof(saveObject));

            if (saveObjects.ContainsKey(saveObject.Key))
                throw new InvalidOperationException($"Save object already registered: {saveObject.Key}");

            saveObjects.Add(saveObject.Key, saveObject);

            if (IsLoaded && loadedEntries.TryGetValue(saveObject.Key, out var entry))
            {
                Restore(saveObject, entry);
            }
        }

        public bool Unregister(SaveKey key)
        {
            return saveObjects.Remove(key);
        }

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void UpdateTime(float time)
        {
            currentTime = time;

            if (IsLoaded)
                currentSave.SetTime(time);
        }

        public async Task LoadAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            loadedEntries.Clear();

            if (serializer.ExistsAtPersistentDataPath(saveFileName))
            {
                currentSave = await serializer.LoadFromPersistentDataPathAsync<GlobalSaveData>(
                    saveFileName,
                    saveFormat,
                    ct);
            }
            else
            {
                currentSave = new GlobalSaveData();
            }

            currentSave ??= new GlobalSaveData();

            currentSave.Initialise(currentTime);

            CacheLoadedEntries(currentSave.SavedData);
            RestoreRegisteredObjects();

            IsLoaded = true;
            IsDirty = false;

            Loaded?.Invoke(this, EventArgs.Empty);
        }

        public Task SaveAsync(CancellationToken ct = default)
        {
            return SaveAsync(false, ct);
        }

        public async Task SaveAsync(
            bool forceSave,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            EnsureLoaded();

            if (!forceSave && !IsDirty)
                return;

            currentSave.SetSavedData(CaptureSavedData());
            currentSave.Flush(currentTime, nowProvider());

            await serializer.SaveToPersistentDataPathAsync(
                currentSave,
                saveFileName,
                saveFormat,
                ct);

            IsDirty = false;

            Saved?.Invoke(this, EventArgs.Empty);
        }

        public void Delete()
        {
            serializer.DeleteAtPersistentDataPath(saveFileName);

            currentSave = new GlobalSaveData();
            IsLoaded = false;
            IsDirty = false;
            loadedEntries.Clear();

            Deleted?.Invoke(this, EventArgs.Empty);
        }

        private SavedDataContainer CaptureSavedData()
        {
            var container = new SavedDataContainer();

            foreach (var pair in saveObjects)
            {
                ISaveObject saveObject = pair.Value;

                object value = saveObject.CaptureUntyped();

                string serializedValue = serializer.SerializeToText(
                    value,
                    saveObject.ValueType,
                    entryFormat);

                container.Entries.Add(new SavedDataEntry(
                    key: saveObject.Key.Value,
                    typeName: saveObject.ValueType.AssemblyQualifiedName,
                    serializedValue: serializedValue));
            }

            return container;
        }

        private void CacheLoadedEntries(SavedDataContainer container)
        {
            if (container?.Entries == null)
                return;

            for (int i = 0; i < container.Entries.Count; i++)
            {
                SavedDataEntry entry = container.Entries[i];

                if (string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                loadedEntries[new SaveKey(entry.Key)] = entry;
            }
        }

        private void RestoreRegisteredObjects()
        {
            foreach (var pair in saveObjects)
            {
                if (loadedEntries.TryGetValue(pair.Key, out var entry))
                {
                    Restore(pair.Value, entry);
                }
            }
        }

        private void Restore(
            ISaveObject saveObject,
            SavedDataEntry entry)
        {
            if (saveObject == null)
                throw new ArgumentNullException(nameof(saveObject));

            if (entry == null)
                return;

            if (string.IsNullOrWhiteSpace(entry.SerializedValue))
                return;

            object value = serializer.DeserializeFromText(
                entry.SerializedValue,
                saveObject.ValueType,
                entryFormat);

            saveObject.RestoreUntyped(value);
        }

        private void EnsureLoaded()
        {
            if (!IsLoaded)
                throw new InvalidOperationException("SaveService has not been loaded.");
        }
    }
}