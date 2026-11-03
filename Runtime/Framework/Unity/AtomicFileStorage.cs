using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SWeetJelly.Serialization.Contract;

namespace SWeetJelly.Save.Framework.Unity
{
    /// <summary>Writes a sibling temporary file before replacing the destination.</summary>
    public sealed class AtomicFileStorage : IFileStorage
    {
        public bool Exists(string path) => File.Exists(path);
        public Task<byte[]> ReadBytesAsync(string path, CancellationToken ct = default) => File.ReadAllBytesAsync(path, ct);
        public void Delete(string path) { if (File.Exists(path)) File.Delete(path); }

        public async Task WriteBytesAsync(string path, byte[] bytes, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            string destination = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length, ct);
                    await stream.FlushAsync(ct);
                    stream.Flush(true);
                }
                ct.ThrowIfCancellationRequested();
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
