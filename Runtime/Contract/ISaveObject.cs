using System;

namespace SWeetJelly.Save.Contract
{
    public interface ISaveObject
    {
        SaveKey Key { get; }
        Type ValueType { get; }
        object CaptureUntyped();
        void RestoreUntyped(object value);
    }

    public interface ISaveObject<T> : ISaveObject
    {
        T Capture();
        void Restore(T value);
    }
}
