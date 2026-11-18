using System;

namespace AFrame.Foundation
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public EntityId(long value) => Value = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        public long Value { get; }
        public static EntityId None => default;
        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object value) => value is EntityId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }

    public readonly struct ViewKey
    {
        public ViewKey(string value) => Value = !string.IsNullOrWhiteSpace(value) ? value :
            throw new ArgumentException("A view key cannot be empty.", nameof(value));
        public string Value { get; }
    }
}
