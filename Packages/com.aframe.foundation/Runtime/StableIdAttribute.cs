using System;

namespace AFrame.Foundation
{
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class StableIdAttribute : Attribute
    {
        public StableIdAttribute(string id)
        {
            Id = id ?? string.Empty;
        }

        public string Id { get; }
    }
}
