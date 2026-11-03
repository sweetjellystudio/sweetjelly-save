namespace SWeetJelly.Save.Tests.PlayMode
{
    using System.Collections;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using SWeetJelly.Save.App;
    using SWeetJelly.Save.Domain;
    using SWeetJelly.Serialization.Contract;
    using SWeetJelly.Shared.Result;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class SaveBehaviourFlowTest
    {
        [UnityTest]
        public IEnumerator ForceSaveFromDriverAsync_WhenLoaded_ForcesSave()
        {
            var holder = new GameObject("Save Behaviour Test Holder");
            var behaviour = holder.AddComponent<SWeetJelly.Save.Framework.Unity.SaveBehaviour>();
            var serializer = new FakeSerializationService();
            var service = new SaveService(
                serializer,
                "save.json",
                SerializationFormat.Json,
                SerializationFormat.Json);

            try
            {
                behaviour.Configure(
                    useAutoSave: false,
                    autoSaveInterval: 0f,
                    saveOnApplicationPause: false,
                    saveOnApplicationQuit: false);
                behaviour.Construct(service);

                var loadResult = service.LoadAsync().GetAwaiter().GetResult();

                Assert.That(loadResult.IsSuccess, Is.True);

                var task = behaviour.ForceSaveFromDriverAsync();

                while (!task.IsCompleted)
                    yield return null;

                Assert.That(task.Exception, Is.Null);
                Assert.That(serializer.SaveCallCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.Destroy(holder);
            }
        }

        [UnityTest]
        public IEnumerator BeginLoad_IsIdempotent_AndShutdownAllowsReconstruction()
        {
            var holder = new GameObject("lifetime");
            var driver = holder.AddComponent<SWeetJelly.Save.Framework.Unity.SaveBehaviour>();
            var fake = new FakeSerializationService(); var service = new SaveService(fake, "test");
            int loaded = 0; service.Loaded += (_,__) => loaded++;
            try
            {
                driver.Configure(false, 0, false, false); driver.Construct(service);
                driver.BeginLoad(); driver.BeginLoad(); yield return null;
                Assert.That(loaded, Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(() => driver.Construct(service));
                driver.Shutdown(); Assert.That(driver.IsConstructed, Is.False);
                driver.Construct(new SaveService(fake,"test")); driver.BeginLoad();
                var saved=driver.ForceSaveFromDriverAsync(); while(!saved.IsCompleted) yield return null;
                Assert.That(saved.Result.IsSuccess, Is.True);
                driver.Shutdown(); driver.Shutdown();
            }
            finally { UnityEngine.Object.Destroy(holder); }
        }

        [UnityTest]
        public IEnumerator ForceSave_ReportsWriteFailureInsteadOfFalseSuccess()
        {
            var holder=new GameObject("failure");
            var driver=holder.AddComponent<SWeetJelly.Save.Framework.Unity.SaveBehaviour>();
            var fake=new FakeSerializationService { FailWrite = true }; var service=new SaveService(fake,"test");
            try
            {
                driver.Configure(false,0,false,false);driver.Construct(service);driver.BeginLoad();
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Fake.Write"));
                var task=driver.ForceSaveFromDriverAsync();while(!task.IsCompleted)yield return null;
                Assert.That(task.Result.IsFailure,Is.True);
                fake.FailWrite=false;
                task=driver.ForceSaveFromDriverAsync();while(!task.IsCompleted)yield return null;
                Assert.That(task.Result.IsSuccess,Is.True);
            }
            finally { UnityEngine.Object.Destroy(holder); }
        }

        private sealed class FakeSerializationService : ISerializationService
        {
            public bool FailWrite;
            public int SaveCallCount { get; private set; }
            private GlobalSaveData storedSave;

            public Task<Result> SaveToPersistentDataPathAsync<T>(
                T value,
                string fileName,
                SerializationFormat format,
                CancellationToken ct = default)
            {
                ct.ThrowIfCancellationRequested();
                SaveCallCount++;
                if (FailWrite) return Task.FromResult(Result.Failure(new Error("Fake.Write", "Failed")));
                storedSave = value as GlobalSaveData;
                return Task.FromResult(Result.Success());
            }

            public Task<Result<T>> LoadFromPersistentDataPathAsync<T>(
                string fileName,
                SerializationFormat format,
                CancellationToken ct = default)
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(Result<T>.Success((T)(object)storedSave));
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
                return storedSave != null;
            }

            public void DeleteAtPersistentDataPath(string fileName)
            {
                storedSave = null;
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
}
