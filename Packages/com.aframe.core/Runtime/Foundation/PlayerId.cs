using System;

namespace AFrame.Foundation
{
    [Serializable]
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public static PlayerId None => new PlayerId("none");

        public PlayerId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? "none" : value;
        }

        public string Value { get; }

        public bool Equals(PlayerId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }
}
