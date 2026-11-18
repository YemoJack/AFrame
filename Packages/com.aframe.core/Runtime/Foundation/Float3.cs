using System;

namespace AFrame.Foundation
{
    [Serializable]
    public struct Float3 : IEquatable<Float3>
    {
        public float x;
        public float y;
        public float z;

        public Float3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Float3 Zero => new Float3(0f, 0f, 0f);

        public static Float3 operator +(Float3 left, Float3 right)
        {
            return new Float3(left.x + right.x, left.y + right.y, left.z + right.z);
        }

        public static Float3 operator *(Float3 value, float scalar)
        {
            return new Float3(value.x * scalar, value.y * scalar, value.z * scalar);
        }

        public bool Equals(Float3 other)
        {
            return x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z);
        }

        public override bool Equals(object obj)
        {
            return obj is Float3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = x.GetHashCode();
                hash = (hash * 397) ^ y.GetHashCode();
                return (hash * 397) ^ z.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"({x}, {y}, {z})";
        }
    }
}
