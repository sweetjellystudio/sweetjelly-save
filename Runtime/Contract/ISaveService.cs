using System;
using System.Threading;
using System.Threading.Tasks;

namespace SWeetJelly.Save.Contract
{
    public interface ISaveService
    {
        bool IsLoaded { get; }
        bool IsDirty { get; }

        event EventHandler Loaded;
        event EventHandler Saved;
        event EventHandler Deleted;

        void Register(ISaveObject saveObject);
        bool Unregister(SaveKey key);

        void MarkDirty();

        Task LoadAsync(CancellationToken ct = default);
        Task SaveAsync(CancellationToken ct = default);
        Task SaveAsync(bool forceSave, CancellationToken ct = default);

        void Delete();
    }
}
