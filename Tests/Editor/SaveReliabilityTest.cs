using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SWeetJelly.Save.App;
using SWeetJelly.Save.Domain;
using SWeetJelly.Save.Framework.Unity;
using SWeetJelly.Serialization.App;
using SWeetJelly.Serialization.Contract;
using SWeetJelly.Serialization.Framework.Serialization;

namespace SWeetJelly.Save.Tests.EditMode
{
    public sealed class SaveReliabilityTest
    {
        private static GlobalSaveData Document(params SavedDataEntry[] entries) => new GlobalSaveData {
            SavedData = new SavedDataContainer { Entries = new System.Collections.Generic.List<SavedDataEntry>(entries) }
        };
        private static SavedDataEntry Entry(string key, string value) => new SavedDataEntry(key, typeof(int).AssemblyQualifiedName, value);

        [Test]
        public async Task UnknownEntryAndUnregisteredEntry_SurviveSave()
        {
            var fake = new FakeSerializationService { StoredSave = Document(Entry("account.other", "123"), Entry("coins", "3")) };
            var service = new SaveService(fake, "test");
            var coins = new SimpleIntSave("coins");
            service.Register(coins);
            Assert.That((await service.LoadAsync()).IsSuccess, Is.True);
            Assert.That(service.Unregister(coins.Key), Is.True);
            Assert.That((await service.SaveAsync(true)).IsSuccess, Is.True);
            Assert.That(fake.StoredSave.SavedData.Entries.Count, Is.EqualTo(2));
            Assert.That(fake.StoredSave.SavedData.Entries[0].SerializedValue, Is.EqualTo("123"));
        }

        [Test]
        public async Task DirtyDuringWrite_RemainsDirty_AndQueuedForceActuallyWrites()
        {
            var fake = new FakeSerializationService();
            var service = new SaveService(fake, "test");
            var coins = new SimpleIntSave("coins", 1);
            service.Register(coins); await service.LoadAsync();
            service.MarkDirty();
            fake.WriteGate = new TaskCompletionSource<bool>();
            var first = service.SaveAsync();
            coins.Set(2); service.MarkDirty();
            Assert.That(fake.SaveCallCount, Is.EqualTo(1));
            fake.WriteGate.SetResult(true);
            Assert.That((await first).IsSuccess, Is.True);
            Assert.That(service.IsDirty, Is.True);
            fake.WriteGate = new TaskCompletionSource<bool>();
            var second = service.SaveAsync(true);
            var third = service.SaveAsync(true);
            Assert.That(third.IsCompleted, Is.False);
            Assert.That(fake.SaveCallCount, Is.EqualTo(2));
            fake.WriteGate.SetResult(true);
            await second; await third;
            Assert.That(fake.SaveCallCount, Is.EqualTo(3));
            Assert.That(fake.StoredSave.SavedData.Entries[0].SerializedValue, Is.EqualTo("2"));
            Assert.That(service.IsDirty, Is.False);
        }

        [Test]
        public async Task Delete_IsQueuedAfterWrite()
        {
            var fake = new FakeSerializationService(); var service = new SaveService(fake,"test");
            await service.LoadAsync(); fake.WriteGate = new TaskCompletionSource<bool>();
            var save = service.SaveAsync(true); var delete = service.DeleteAsync();
            Assert.That(delete.IsCompleted, Is.False);
            fake.WriteGate.SetResult(true); await save; await delete;
            Assert.That(fake.StoredSave, Is.Null); Assert.That(service.IsLoaded, Is.False);
        }

        [Test]
        public async Task FailedWrite_PreservesSnapshotAndDirty_AndCanRetry()
        {
            var original = Document(Entry("coins","1"));
            var fake = new FakeSerializationService { StoredSave = original, FailWrite = true };
            var service = new SaveService(fake,"test"); var coins = new SimpleIntSave("coins");
            service.Register(coins); await service.LoadAsync(); coins.Set(2); service.MarkDirty();
            Assert.That((await service.SaveAsync()).IsFailure, Is.True);
            Assert.That(service.CurrentSave, Is.SameAs(original)); Assert.That(service.IsDirty, Is.True);
            fake.FailWrite = false; Assert.That((await service.SaveAsync()).IsSuccess, Is.True);
        }

        [Test]
        public async Task CancelledQueuedSave_DoesNotWriteOrBlockLaterRequests()
        {
            var fake = new FakeSerializationService(); var service = new SaveService(fake,"test"); await service.LoadAsync();
            fake.WriteGate = new TaskCompletionSource<bool>(); var save = service.SaveAsync(true);
            using var ct = new CancellationTokenSource(); var cancelled = service.SaveAsync(true,ct.Token); ct.Cancel();
            try { await cancelled; Assert.Fail("Cancellation expected"); } catch (OperationCanceledException) { }
            fake.WriteGate.SetResult(true); await save;
            Assert.That(fake.SaveCallCount, Is.EqualTo(1));
            Assert.That((await service.DeleteAsync()).IsSuccess, Is.True);
        }

        [TestCase("duplicate")]
        [TestCase("empty")]
        [TestCase("null")]
        [TestCase("version")]
        [TestCase("payload")]
        public async Task InvalidDocument_RestoresNothing_AndBlocksSave(string kind)
        {
            var document = Document(Entry("first","1"),Entry("second","2"));
            if (kind == "duplicate") document.SavedData.Entries.Add(Entry("first","3"));
            if (kind == "empty") document.SavedData.Entries.Add(Entry("", "3"));
            if (kind == "null") document.SavedData.Entries.Add(null);
            if (kind == "version") document.SavedData.Version = 999;
            if (kind == "payload") document.SavedData.Entries[1].SerializedValue = "invalid-int";
            var fake = new FakeSerializationService { StoredSave = document }; var service = new SaveService(fake,"test");
            var first = new CounterSave("first"); service.Register(first); service.Register(new SimpleIntSave("second"));
            Assert.That((await service.LoadAsync()).IsFailure, Is.True);
            Assert.That(first.Restores, Is.EqualTo(0)); Assert.That(service.IsLoaded, Is.False);
            Assert.That((await service.SaveAsync(true)).IsFailure, Is.True); Assert.That(fake.SaveCallCount, Is.Zero);
        }

        [Test]
        public async Task RestoreException_BlocksSaveUntilSuccessfulReload()
        {
            var fake = new FakeSerializationService { StoredSave = Document(Entry("first","1")) };
            var service = new SaveService(fake,"test"); var first = new CounterSave("first") { Throw = true }; service.Register(first);
            Assert.That((await service.LoadAsync()).IsFailure, Is.True);
            Assert.That((await service.SaveAsync(true)).IsFailure, Is.True);
            first.Throw = false; Assert.That((await service.LoadAsync()).IsSuccess, Is.True);
        }

        [Test]
        public async Task ExplicitKeyDeletion_RejectsActiveKey_AndPersistsRemoval()
        {
            var fake = new FakeSerializationService { StoredSave = Document(Entry("coins","1"),Entry("other","2")) };
            var service = new SaveService(fake,"test"); service.Register(new SimpleIntSave("coins")); await service.LoadAsync();
            Assert.That((await service.DeleteKeyAsync("coins")).IsFailure, Is.True);
            service.Unregister("coins"); Assert.That((await service.DeleteKeyAsync("coins")).IsSuccess, Is.True);
            Assert.That(fake.StoredSave.SavedData.Entries.Count, Is.EqualTo(1));
            Assert.That(fake.StoredSave.SavedData.Entries[0].Key, Is.EqualTo("other"));
        }

        [Test]
        public async Task Notification_CanStartAnotherOperationWithoutGateDeadlock()
        {
            var service = new SaveService(new FakeSerializationService(),"test");
            Task<SWeetJelly.Shared.Result.Result> nested = null;
            service.Loaded += (_,__) => nested = service.SaveAsync(true);
            await service.LoadAsync();
            Assert.That(nested.IsCompleted, Is.True); Assert.That((await nested).IsSuccess, Is.True);
        }

        [TestCase(SerializationFormat.Json)]
        [TestCase(SerializationFormat.Xml)]
        public async Task LegacyDocument_IgnoresGameMetadata_PreservesPayloadAndDoesNotRewriteOnRead(SerializationFormat format)
        {
            string root = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var paths = new Paths(root); var storage = new AtomicFileStorage();
                var codec = new SerializationService(storage,paths,new JsonDataSerializer(),new XmlDataSerializer());
                string raw = format == SerializationFormat.Json
                    ? "{\"BuildId\":\"legacy\",\"LevelId\":9,\"SavedGameTime\":100,\"LastExitTime\":\"2026-01-01T00:00:00Z\",\"SavedData\":{\"Version\":1,\"Entries\":[{\"Key\":\"progress.local\",\"TypeName\":\"game.Progress\",\"SerializedValue\":\"{schema:2,revision:7}\"}]}}"
                    : "<GlobalSaveData><BuildId>legacy</BuildId><LevelId>9</LevelId><SavedGameTime>100</SavedGameTime><SavedData><Version>1</Version><Entries><SavedDataEntry><Key>progress.local</Key><TypeName>game.Progress</TypeName><SerializedValue>{schema:2,revision:7}</SerializedValue></SavedDataEntry></Entries></SavedData></GlobalSaveData>";
                var path=Path.Combine(root,"legacy"); File.WriteAllText(path,raw);
                var service=new SaveService(new AtomicSaveSerializer(codec,paths,storage),"legacy",format);
                Assert.That((await service.LoadAsync()).IsSuccess, Is.True);
                Assert.That(File.ReadAllText(path), Is.EqualTo(raw));
                Assert.That((await service.SaveAsync(true)).IsSuccess, Is.True);
                string written=File.ReadAllText(path);
                Assert.That(written, Does.Not.Contain("BuildId")); Assert.That(written, Does.Not.Contain("SavedGameTime"));
                Assert.That(service.CurrentSave.SavedData.Entries[0].SerializedValue, Is.EqualTo("{schema:2,revision:7}"));
            }
            finally { Directory.Delete(root,true); }
        }

        [Test]
        public async Task AtomicWrite_ReplaceCancellationAndFailure_PreserveOriginalAndCleanTemporaryFiles()
        {
            string root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string path=Path.Combine(root,"save"); var storage=new AtomicFileStorage();
            try
            {
                await storage.WriteBytesAsync(path,Encoding.UTF8.GetBytes("original"));
                using var ct=new CancellationTokenSource(); ct.Cancel();
                try { await storage.WriteBytesAsync(path,new byte[1024],ct.Token); Assert.Fail(); } catch(OperationCanceledException) { }
                Assert.That(File.ReadAllText(path),Is.EqualTo("original"));
                using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
                {
                    try { await storage.WriteBytesAsync(path,Encoding.UTF8.GetBytes("failed")); Assert.Fail(); } catch(IOException) { }
                }
                Assert.That(File.ReadAllText(path),Is.EqualTo("original")); Assert.That(Directory.GetFiles(root).Length,Is.EqualTo(1));
                await storage.WriteBytesAsync(path,Encoding.UTF8.GetBytes("replacement")); Assert.That(File.ReadAllText(path),Is.EqualTo("replacement"));
            }
            finally { Directory.Delete(root,true); }
        }
        private sealed class CounterSave : SaveObject<int>
        {
            public int Restores; public bool Throw;
            public CounterSave(string key):base(key) { }
            public override int Capture()=>0;
            public override void Restore(int value) { Restores++; if(Throw) throw new InvalidOperationException("restore"); }
        }
        private sealed class Paths : IPathProvider
        {
            public Paths(string path) { PersistentDataPath=path; }
            public string PersistentDataPath {get;}
            public string Combine(string directoryPath,string fileName)=>Path.Combine(directoryPath,fileName);
        }
    }
}
