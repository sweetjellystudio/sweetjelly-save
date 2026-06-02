using System;

namespace SWeetJelly.Save.Contract
{
    public readonly struct SaveKey : IEquatable<SaveKey>
    {
        public string Value { get; }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public SaveKey(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool Equals(SaveKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SaveKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value;
        }

        public static implicit operator SaveKey(string value)
        {
            return new SaveKey(value);
        }
    }
}