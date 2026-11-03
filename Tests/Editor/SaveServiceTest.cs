namespace SWeetJelly.Save.Tests.EditMode
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using SWeetJelly.Save.App;
    using SWeetJelly.Save.Contract;
    using SWeetJelly.Save.Domain;
    using SWeetJelly.Serialization.Contract;
    using SWeetJelly.Shared.Result;

    public sealed class SaveServiceTest
    {
        [Test]
        public void LoadAsync_WhenNoSaveExists_BootstrapsNewSaveAndRaisesLoaded()
        {
            var serializer = new FakeSerializationService();
            var service = CreateService(serializer);
            var loadedCount = 0;

            service.Loaded += (_, _) => loadedCount++;

            var result = service.LoadAsync().GetAwaiter().GetResult();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(service.IsLoaded, Is.True);
            Assert.That(service.IsDirty, Is.False);
            Assert.That(service.CurrentSave, Is.Not.Null);
            Assert.That(loadedCount, Is.EqualTo(1));
        }

        [Test]
        public void Register_AfterLoad_RestoresMatchingLoadedEntry()
        {
            var serializer = new FakeSerializationService();
            var saveData = new GlobalSaveData();
            saveData.SetSavedData(new SavedDataContainer
            {
                Entries = new List<SavedDataEntry>
                {
                    new SavedDataEntry(
                        "coins",
                        typeof(int).AssemblyQualifiedName,
                        "42")
                }
            });
            serializer.StoredSave = saveData;

            var service = CreateService(serializer);
            Assert.That(service.LoadAsync().GetAwaiter().GetResult().IsSuccess, Is.True);

            var coins = new SimpleIntSave("coins", 0);

            var registerResult = service.Register(coins);

            Assert.That(registerResult.IsSuccess, Is.True);
            Assert.That(coins.Value, Is.EqualTo(42));
        }

        [Test]
        public void SaveAsync_WhenDirty_CapturesRegisteredObjectsAndRaisesSaved()
        {
            var serializer = new FakeSerializationService();
            var service = CreateService(serializer);
            var coins = new SimpleIntSave("coins", 7);
            var savedCount = 0;

            service.Saved += (_, _) => savedCount++;
            Assert.That(service.Register(coins).IsSuccess, Is.True);
            Assert.That(service.LoadAsync().GetAwaiter().GetResult().IsSuccess, Is.True);

            coins.Set(99);
            service.MarkDirty();
            var saveResult = service.SaveAsync().GetAwaiter().GetResult();

            Assert.That(saveResult.IsSuccess, Is.True);
            Assert.That(savedCount, Is.EqualTo(1));
            Assert.That(service.IsDirty, Is.False);
            Assert.That(serializer.SaveCallCount, Is.EqualTo(1));
            Assert.That(serializer.StoredSave.SavedData.Entries.Count, Is.EqualTo(1));
            Assert.That(serializer.StoredSave.SavedData.Entries[0].Key, Is.EqualTo("coins"));
            Assert.That(serializer.StoredSave.SavedData.Entries[0].SerializedValue, Is.EqualTo("99"));
        }

        [Test]
        public void SaveAsync_WhenNotDirtyAndNotForced_DoesNotWrite()
        {
            var serializer = new FakeSerializationService();
            var service = CreateService(serializer);

            Assert.That(service.LoadAsync().GetAwaiter().GetResult().IsSuccess, Is.True);
            Assert.That(service.SaveAsync().GetAwaiter().GetResult().IsSuccess, Is.True);

            Assert.That(serializer.SaveCallCount, Is.EqualTo(0));
        }

        [Test]
        public void SaveAsync_WhenForced_WritesEvenWhenNotDirty()
        {
            var serializer = new FakeSerializationService();
            var service = CreateService(serializer);

            Assert.That(service.LoadAsync().GetAwaiter().GetResult().IsSuccess, Is.True);
            Assert.That(service.SaveAsync(forceSave: true).GetAwaiter().GetResult().IsSuccess, Is.True);

            Assert.That(serializer.SaveCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Delete_RemovesSaveAndRaisesDeleted()
        {
            var serializer = new FakeSerializationService();
            var service = CreateService(serializer);
            var deletedCount = 0;

            service.Deleted += (_, _) => deletedCount++;
            Assert.That(service.LoadAsync().GetAwaiter().GetResult().IsSuccess, Is.True);
            Assert.That(service.DeleteAsync().GetAwaiter().GetResult().IsSuccess, Is.True);

            Assert.That(serializer.DeleteCallCount, Is.EqualTo(1));
            Assert.That(service.IsLoaded, Is.False);
            Assert.That(service.IsDirty, Is.False);
            Assert.That(deletedCount, Is.EqualTo(1));
        }

        [Test]
        public void Register_WithDuplicateKey_ReturnsFailure()
        {
            var service = CreateService(new FakeSerializationService());

            Assert.That(service.Register(new SimpleIntSave("coins")).IsSuccess, Is.True);

            var result = service.Register(new SimpleIntSave("coins"));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("Save.DuplicateKey"));
        }

        [Test]
        public void SaveAsync_BeforeLoad_ReturnsFailure()
        {
            var service = CreateService(new FakeSerializationService());

            var result = service.SaveAsync(forceSave: true).GetAwaiter().GetResult();

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(SaveErrors.NotLoaded));
        }

        private static SaveService CreateService(
            FakeSerializationService serializer)
        {
            return new SaveService(
                serializer,
                "save.json",
                SerializationFormat.Json,
                SerializationFormat.Json);
        }

        private static TException AssertThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
            return null;
        }

        private static TException AssertThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                action().GetAwaiter().GetResult();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
            return null;
        }
    }

    public sealed class FakeSerializationService : ISerializationService
    {
        public GlobalSaveData StoredSave { get; set; }
        public int SaveCallCount { get; private set; }
        public int DeleteCallCount { get; private set; }

        public TaskCompletionSource<bool> WriteGate;
        public bool FailWrite;
        public bool ThrowDelete;

        public async Task<Result> SaveToPersistentDataPathAsync<T>(
            T value,
            string fileName,
            SerializationFormat format,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            SaveCallCount++;
            if (WriteGate != null) await WriteGate.Task;
            ct.ThrowIfCancellationRequested();
            if (FailWrite) return Result.Failure(new Error("Fake.Write", "Failed"));
            StoredSave = value as GlobalSaveData;
            return Result.Success();
        }

        public Task<Result<T>> LoadFromPersistentDataPathAsync<T>(
            string fileName,
            SerializationFormat format,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Result<T>.Success((T)(object)StoredSave));
        }

        public Task<Result> SaveToPathAsync<T>(
            T value,
            string path,
            SerializationFormat format,
            string secureKey = "",
            CancellationToken ct = default)
        {
            return SaveToPersistentDataPathAsync(value, path, format, ct);
        }

        public Task<Result<T>> LoadFromPathAsync<T>(
            string path,
            SerializationFormat format,
            string secureKey = "",
            CancellationToken ct = default)
        {
            return LoadFromPersistentDataPathAsync<T>(path, format, ct);
        }

        public bool ExistsAtPersistentDataPath(string fileName)
        {
            return StoredSave != null;
        }

        public void DeleteAtPersistentDataPath(string fileName)
        {
            if (ThrowDelete) throw new System.IO.IOException("delete failed");
            DeleteCallCount++;
            StoredSave = null;
        }

        public Result<string> SerializeToText<T>(T value, SerializationFormat format, string secureKey = "")
        {
            return Result<string>.Success(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        }

        public Result<string> SerializeToText(
            object value,
            Type valueType,
            SerializationFormat format,
            string secureKey = "")
        {
            return Result<string>.Success(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        }

        public Result<T> DeserializeFromText<T>(
            string text,
            SerializationFormat format,
            string secureKey = "")
        {
            Result<object> result = DeserializeFromText(text, typeof(T), format, secureKey);

            if (result.IsFailure)
                return Result<T>.Failure(result.Error);

            return Result<T>.Success((T)result.Value);
        }

        public Result<object> DeserializeFromText(
            string text,
            Type valueType,
            SerializationFormat format,
            string secureKey = "")
        {
            if (valueType == typeof(int))
                return Result<object>.Success(int.Parse(text, System.Globalization.CultureInfo.InvariantCulture));

            if (valueType == typeof(float))
                return Result<object>.Success(float.Parse(text, System.Globalization.CultureInfo.InvariantCulture));

            if (valueType == typeof(bool))
                return Result<object>.Success(bool.Parse(text));

            if (valueType == typeof(string))
                return Result<object>.Success(text);

            throw new NotSupportedException(valueType.FullName);
        }
    }
}
