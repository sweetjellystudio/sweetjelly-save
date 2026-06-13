using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SWeetJelly.Save.Contract;
using SWeetJelly.Save.Domain;
using SWeetJelly.Serialization.Contract;
using SWeetJelly.Shared.Result;

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
                Result loadedResult = EnsureLoaded();

                if (loadedResult.IsFailure)
                    throw new InvalidOperationException(loadedResult.Error.ToString());

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

        public Result Register(ISaveObject saveObject)
        {
            if (saveObject == null)
                return Result.Failure(SaveErrors.InvalidSaveObject);

            if (saveObject.Key.IsEmpty)
                return Result.Failure(SaveErrors.InvalidKey);

            if (saveObjects.ContainsKey(saveObject.Key))
                return Result.Failure(SaveErrors.DuplicateKey(saveObject.Key));

            saveObjects.Add(saveObject.Key, saveObject);

            if (IsLoaded && loadedEntries.TryGetValue(saveObject.Key, out var entry))
            {
                Result restoreResult = Restore(saveObject, entry);

                if (restoreResult.IsFailure)
                {
                    saveObjects.Remove(saveObject.Key);
                    return restoreResult;
                }
            }

            return Result.Success();
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

        public async Task<Result> LoadAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            loadedEntries.Clear();

            if (serializer.ExistsAtPersistentDataPath(saveFileName))
            {
                Result<GlobalSaveData> loadResult = await serializer.LoadFromPersistentDataPathAsync<GlobalSaveData>(
                    saveFileName,
                    saveFormat,
                    ct);

                if (loadResult.IsFailure)
                    return Result.Failure(SaveErrors.LoadFailed(loadResult.Error));

                currentSave = loadResult.Value;
            }
            else
            {
                currentSave = new GlobalSaveData();
            }

            currentSave ??= new GlobalSaveData();

            currentSave.Initialise(currentTime);

            CacheLoadedEntries(currentSave.SavedData);
            Result restoreResult = RestoreRegisteredObjects();

            if (restoreResult.IsFailure)
                return restoreResult;

            IsLoaded = true;
            IsDirty = false;

            Loaded?.Invoke(this, EventArgs.Empty);

            return Result.Success();
        }

        public Task<Result> SaveAsync(CancellationToken ct = default)
        {
            return SaveAsync(false, ct);
        }

        public async Task<Result> SaveAsync(
            bool forceSave,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            Result loadedResult = EnsureLoaded();

            if (loadedResult.IsFailure)
                return loadedResult;

            if (!forceSave && !IsDirty)
                return Result.Success();

            Result<SavedDataContainer> captureResult = CaptureSavedData();

            if (captureResult.IsFailure)
                return Result.Failure(captureResult.Error);

            currentSave.SetSavedData(captureResult.Value);
            currentSave.Flush(currentTime, nowProvider());

            Result saveResult = await serializer.SaveToPersistentDataPathAsync(
                currentSave,
                saveFileName,
                saveFormat,
                ct);

            if (saveResult.IsFailure)
                return Result.Failure(SaveErrors.SaveFailed(saveResult.Error));

            IsDirty = false;

            Saved?.Invoke(this, EventArgs.Empty);

            return Result.Success();
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

        private Result<SavedDataContainer> CaptureSavedData()
        {
            var container = new SavedDataContainer();

            foreach (var pair in saveObjects)
            {
                ISaveObject saveObject = pair.Value;

                object value = saveObject.CaptureUntyped();

                Result<string> serializeResult = serializer.SerializeToText(
                    value,
                    saveObject.ValueType,
                    entryFormat);

                if (serializeResult.IsFailure)
                    return Result<SavedDataContainer>.Failure(
                        SaveErrors.CaptureFailed(saveObject.Key, serializeResult.Error));

                container.Entries.Add(new SavedDataEntry(
                    key: saveObject.Key.Value,
                    typeName: saveObject.ValueType.AssemblyQualifiedName,
                    serializedValue: serializeResult.Value));
            }

            return Result<SavedDataContainer>.Success(container);
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

        private Result RestoreRegisteredObjects()
        {
            foreach (var pair in saveObjects)
            {
                if (loadedEntries.TryGetValue(pair.Key, out var entry))
                {
                    Result restoreResult = Restore(pair.Value, entry);

                    if (restoreResult.IsFailure)
                        return restoreResult;
                }
            }

            return Result.Success();
        }

        private Result Restore(
            ISaveObject saveObject,
            SavedDataEntry entry)
        {
            if (saveObject == null)
                return Result.Failure(SaveErrors.InvalidSaveObject);

            if (entry == null)
                return Result.Success();

            if (string.IsNullOrWhiteSpace(entry.SerializedValue))
                return Result.Success();

            Result<object> deserializeResult = serializer.DeserializeFromText(
                entry.SerializedValue,
                saveObject.ValueType,
                entryFormat);

            if (deserializeResult.IsFailure)
                return Result.Failure(SaveErrors.RestoreFailed(saveObject.Key, deserializeResult.Error));

            saveObject.RestoreUntyped(deserializeResult.Value);

            return Result.Success();
        }

        private Result EnsureLoaded()
        {
            if (!IsLoaded)
                return Result.Failure(SaveErrors.NotLoaded);

            return Result.Success();
        }
    }
}
