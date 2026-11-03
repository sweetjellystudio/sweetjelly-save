using System;
using System.Threading;
using System.Threading.Tasks;
using SWeetJelly.Shared.Result;

namespace SWeetJelly.Save.Contract
{
    public interface ISaveService
    {
        bool IsLoaded { get; }
        bool IsDirty { get; }

        event EventHandler Loaded;
        event EventHandler Saved;
        event EventHandler Deleted;

        Result Register(ISaveObject saveObject);
        bool Unregister(SaveKey key);

        void MarkDirty();

        Task<Result> LoadAsync(CancellationToken ct = default);
        Task<Result> SaveAsync(CancellationToken ct = default);
        Task<Result> SaveAsync(bool forceSave, CancellationToken ct = default);

        Task<Result> DeleteKeyAsync(SaveKey key, CancellationToken ct = default);

        Task<Result> DeleteAsync(CancellationToken ct = default);
    }
}
