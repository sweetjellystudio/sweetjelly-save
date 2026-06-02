using System;
using SWeetJelly.Save.Contract;

namespace SWeetJelly.Save.Domain
{
    [Serializable]
    public abstract class PrimitiveSaveObject<T> : ISaveObject<T>
    {
        public SaveKey Key { get; }
        public Type ValueType => typeof(T);
        public T Value { get; private set; }

        protected PrimitiveSaveObject(SaveKey key, T defaultValue = default)
        {
            if (key.IsEmpty)
                throw new ArgumentException("Save key cannot be empty.", nameof(key));

            Key = key;
            Value = defaultValue;
        }

        public T Capture()
        {
            return Value;
        }

        public void Restore(T value)
        {
            Value = value;
        }

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

            if (value == null && default(T) == null)
            {
                Restore(default);
                return;
            }

            throw new InvalidCastException(
                $"Cannot restore value of type '{value?.GetType().Name}' to save object '{Key}' of type '{typeof(T).Name}'.");
        }

        public void Set(T value)
        {
            Value = value;
        }
    }
}