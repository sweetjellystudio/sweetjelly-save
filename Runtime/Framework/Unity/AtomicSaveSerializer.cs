using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SWeetJelly.Serialization.Contract;
using SWeetJelly.Shared.Result;

namespace SWeetJelly.Save.Framework.Unity
{
    /// <summary>Uses the supplied codecs while applying save-owned atomic file policy.</summary>
    public sealed class AtomicSaveSerializer : ISerializationService
    {
        private readonly ISerializationService codecs;
        private readonly IPathProvider paths;
        private readonly IFileStorage storage;
        public AtomicSaveSerializer(ISerializationService codecs, IPathProvider paths, IFileStorage storage)
        {
            this.codecs = codecs ?? throw new ArgumentNullException(nameof(codecs));
            this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }
        public Task<Result> SaveToPersistentDataPathAsync<T>(T value, string fileName, SerializationFormat format, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return SaveToPathAsync(value, paths.Combine(paths.PersistentDataPath, fileName), format, ct:ct);
        }
        public Task<Result<T>> LoadFromPersistentDataPathAsync<T>(string fileName, SerializationFormat format, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return LoadFromPathAsync<T>(paths.Combine(paths.PersistentDataPath, fileName), format, ct:ct);
        }
        public async Task<Result> SaveToPathAsync<T>(T value, string path, SerializationFormat format, string secureKey = "", CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var text = codecs.SerializeToText(value, format, secureKey);
                if (text.IsFailure) return Result.Failure(text.Error);
                ct.ThrowIfCancellationRequested();
                await storage.WriteBytesAsync(path, Encoding.UTF8.GetBytes(text.Value), ct);
                return Result.Success();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { return Result.Failure(SerializationErrors.WriteFailed(path,e)); }
        }
        public async Task<Result<T>> LoadFromPathAsync<T>(string path, SerializationFormat format, string secureKey = "", CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var bytes = await storage.ReadBytesAsync(path,ct);
                ct.ThrowIfCancellationRequested();
                return codecs.DeserializeFromText<T>(Encoding.UTF8.GetString(bytes),format,secureKey);
            }
            catch (OperationCanceledException) { throw; }
            catch (FileNotFoundException) { return Result<T>.Failure(SerializationErrors.FileNotFound(path)); }
            catch (Exception e) { return Result<T>.Failure(SerializationErrors.ReadFailed(path,e)); }
        }
        public bool ExistsAtPersistentDataPath(string fileName) => storage.Exists(paths.Combine(paths.PersistentDataPath,fileName));
        public void DeleteAtPersistentDataPath(string fileName) => storage.Delete(paths.Combine(paths.PersistentDataPath,fileName));
        public Result<string> SerializeToText<T>(T value,SerializationFormat format,string secureKey="") => codecs.SerializeToText(value,format,secureKey);
        public Result<string> SerializeToText(object value,Type valueType,SerializationFormat format,string secureKey="") => codecs.SerializeToText(value,valueType,format,secureKey);
        public Result<T> DeserializeFromText<T>(string text,SerializationFormat format,string secureKey="") => codecs.DeserializeFromText<T>(text,format,secureKey);
        public Result<object> DeserializeFromText(string text,Type valueType,SerializationFormat format,string secureKey="") => codecs.DeserializeFromText(text,valueType,format,secureKey);
    }
}
