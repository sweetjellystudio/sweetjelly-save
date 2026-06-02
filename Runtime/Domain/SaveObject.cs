using System;
using SWeetJelly.Save.Contract;

namespace SWeetJelly.Save.Domain
{
    [Serializable]
    public abstract class SaveObject<T> : ISaveObject<T>
    {
        public SaveKey Key { get; }

        public Type ValueType => typeof(T);

        protected SaveObject(SaveKey key)
        {
            if (key.IsEmpty)
                throw new ArgumentException("Save key cannot be empty.", nameof(key));

            Key = key;
        }

        public abstract T Capture();

        public abstract void Restore(T value);

        public object CaptureUntyped()
        {
            return Capture();
        }

        public void RestoreUntyped(object value)
        {
            if (value is T typed)
            {
                Restore(typed);
                return;
            }

            throw new InvalidCastException(
                $"Cannot restore value of type '{value?.GetType().Name}' to save object '{Key}' of type '{typeof(T).Name}'.");
        }
    }
}