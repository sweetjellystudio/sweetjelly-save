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
        private readonly SemaphoreSlim operationGate = new SemaphoreSlim(1, 1);
        private long revision;

        private readonly Dictionary<SaveKey, ISaveObject> saveObjects = new();
        private readonly Dictionary<SaveKey, SavedDataEntry> loadedEntries = new();

        private GlobalSaveData currentSave = new GlobalSaveData();


        public string SaveFileName => saveFileName;

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

        public event EventHandler Loaded;
        public event EventHandler Saved;
        public event EventHandler Deleted;

        public SaveService(
            ISerializationService serializer,
            string saveFileName,
            SerializationFormat saveFormat = SerializationFormat.Json,
            SerializationFormat entryFormat = SerializationFormat.Json)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            if (string.IsNullOrWhiteSpace(saveFileName))
                throw new ArgumentException("Save file name cannot be null or whitespace.", nameof(saveFileName));

            this.saveFileName = saveFileName;
            this.saveFormat = saveFormat;
            this.entryFormat = entryFormat;

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
                Result restoreResult;
                try { restoreResult = Restore(saveObject, entry); }
                catch (OperationCanceledException) { saveObjects.Remove(saveObject.Key); throw; }
                catch (Exception e) { restoreResult = Result.Failure(new Error("Save.RestoreFailed", e.Message)); }

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
            revision++;
            IsDirty = true;
        }

        private async Task<Result> LoadCoreAsync(CancellationToken ct)
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

            if (currentSave == null) throw new InvalidOperationException("Save document is null.");



            CacheLoadedEntries(currentSave.SavedData);
            Result restoreResult = RestoreRegisteredObjects();

            if (restoreResult.IsFailure)
                return restoreResult;

            IsLoaded = true;
            IsDirty = false;



            return Result.Success();
        }

        public Task<Result> SaveAsync(CancellationToken ct = default)
        {
            return SaveAsync(false, ct);
        }

        private async Task<Result> SaveCoreAsync(bool forceSave, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            Result loadedResult = EnsureLoaded();

            if (loadedResult.IsFailure)
                return loadedResult;

            if (!forceSave && !IsDirty)
                return Result.Success();

            long capturedRevision = revision;
            Result<SavedDataContainer> captureResult = CaptureSavedData();

            if (captureResult.IsFailure)
                return Result.Failure(captureResult.Error);

            var snapshot = new GlobalSaveData { SavedData = captureResult.Value };

            Result saveResult = await serializer.SaveToPersistentDataPathAsync(
                snapshot,
                saveFileName,
                saveFormat,
                ct);

            if (saveResult.IsFailure)
                return Result.Failure(SaveErrors.SaveFailed(saveResult.Error));

            currentSave = snapshot;
            loadedEntries.Clear();
            CacheLoadedEntries(snapshot.SavedData);
            IsDirty = revision != capturedRevision;



            return Result.Success();
        }

        private void DeleteCore()
        {
            serializer.DeleteAtPersistentDataPath(saveFileName);

            currentSave = new GlobalSaveData();
            IsLoaded = false;
            IsDirty = false;
            loadedEntries.Clear();


        }

        public Task<Result> LoadAsync(CancellationToken ct = default) => RunAsync(async () =>
        {
            IsLoaded = false;
            try { return await LoadCoreAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { return Result.Failure(new Error("Save.LoadFailed", e.Message)); }
        }, ct, () => Loaded?.Invoke(this, EventArgs.Empty));

        public Task<Result> SaveAsync(bool forceSave, CancellationToken ct = default) =>
            RunAsync(() => SaveCoreAsync(forceSave, ct), ct, () => Saved?.Invoke(this, EventArgs.Empty), () => forceSave || IsDirty);

        public Task<Result> DeleteAsync(CancellationToken ct = default) => RunAsync(() =>
        {
            DeleteCore();
            return Task.FromResult(Result.Success());
        }, ct, () => Deleted?.Invoke(this, EventArgs.Empty));

        // Service calls and registration are owned by the caller's Unity/main context.
        // Notifications run after releasing the IO gate, permitting reentrant requests.
        private async Task<Result> RunAsync(Func<Task<Result>> action, CancellationToken ct,
            Action notify = null, Func<bool> shouldNotify = null)
        {
            await operationGate.WaitAsync(ct);
            Result result;
            bool changed = shouldNotify == null || shouldNotify();
            try { ct.ThrowIfCancellationRequested(); result = await action(); }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { result = Result.Failure(new Error("Save.OperationFailed", e.Message)); }
            finally { operationGate.Release(); }
            if (result.IsSuccess && changed) notify?.Invoke();
            return result;
        }

        public Task<Result> DeleteKeyAsync(SaveKey key, CancellationToken ct = default) => RunAsync(async () =>
        {
            var loaded = EnsureLoaded();
            if (loaded.IsFailure) return loaded;
            if (key.IsEmpty) return Result.Failure(SaveErrors.InvalidKey);
            if (saveObjects.ContainsKey(key))
                return Result.Failure(new Error("Save.KeyRegistered", "Unregister the key before deleting its persisted entry."));
            if (!loadedEntries.TryGetValue(key, out var entry)) return Result.Success();
            loadedEntries.Remove(key);
            MarkDirty();
            try
            {
                var result = await SaveCoreAsync(true, ct);
                if (result.IsFailure) loadedEntries[key] = entry;
                return result;
            }
            catch { loadedEntries[key] = entry; throw; }
        }, ct);

        private Result<SavedDataContainer> CaptureSavedData()
        {
            var container = new SavedDataContainer();
            foreach (var entry in loadedEntries.Values)
                if (!saveObjects.ContainsKey(new SaveKey(entry.Key)))
                    container.Entries.Add(new SavedDataEntry(entry.Key, entry.TypeName, entry.SerializedValue));

            foreach (var pair in new List<KeyValuePair<SaveKey, ISaveObject>>(saveObjects))
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
            if (container == null || container.Entries == null || container.Version != 1)
                throw new InvalidOperationException("Unsupported or invalid save document.");

            for (int i = 0; i < container.Entries.Count; i++)
            {
                SavedDataEntry entry = container.Entries[i];

                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.SerializedValue == null)
                    throw new InvalidOperationException("Invalid saved entry.");
                if (!loadedEntries.TryAdd(new SaveKey(entry.Key), entry))
                    throw new InvalidOperationException("Duplicate saved key: " + entry.Key);
            }
        }

        private Result RestoreRegisteredObjects()
        {
            var pending = new List<KeyValuePair<ISaveObject, object>>();
            foreach (var pair in new List<KeyValuePair<SaveKey, ISaveObject>>(saveObjects))
            {
                if (!loadedEntries.TryGetValue(pair.Key, out var entry)) continue;
                var value = serializer.DeserializeFromText(entry.SerializedValue, pair.Value.ValueType, entryFormat);
                if (value.IsFailure) return Result.Failure(SaveErrors.RestoreFailed(pair.Key, value.Error));
                pending.Add(new KeyValuePair<ISaveObject, object>(pair.Value, value.Value));
            }
            foreach (var pair in pending) pair.Key.RestoreUntyped(pair.Value);
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
