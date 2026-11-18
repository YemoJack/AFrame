using System;

namespace AFrame.Foundation
{
    [Serializable]
    public readonly struct ModuleId : IEquatable<ModuleId>
    {
        private readonly string value;

        public ModuleId(string value)
        {
            this.value = value ?? string.Empty;
        }

        public string Value => value ?? string.Empty;

        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(ModuleId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ModuleId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(ModuleId left, ModuleId right) => left.Equals(right);
        public static bool operator !=(ModuleId left, ModuleId right) => !left.Equals(right);
    }
}
