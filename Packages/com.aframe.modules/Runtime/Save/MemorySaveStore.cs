using System.Collections.Generic;
using AFrame.Runtime;

namespace AFrame.Save
{
    public sealed class MemorySaveStore : ISaveStore
    {
        private readonly Dictionary<string, object> values =
            new Dictionary<string, object>();

        public void Save<T>(string id, T value)
        {
            values[id] = value;
        }

        public bool TryLoad<T>(string id, out T value)
        {
            if (values.TryGetValue(id, out var stored) && stored is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }
    }
}
