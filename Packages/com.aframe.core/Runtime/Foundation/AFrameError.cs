using System;

namespace AFrame.Foundation
{
    [Serializable]
    public readonly struct AFrameError : IEquatable<AFrameError>
    {
        public AFrameError(string code, string message)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string Message { get; }

        public bool Equals(AFrameError other)
        {
            return string.Equals(Code, other.Code, StringComparison.Ordinal)
                   && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AFrameError other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Code) * 397)
                       ^ StringComparer.Ordinal.GetHashCode(Message);
            }
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Code) ? Message : $"{Code}: {Message}";
        }
    }
}
