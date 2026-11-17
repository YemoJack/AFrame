using Newtonsoft.Json.Linq;

namespace AFrame.Data
{
    // Editor imports apply one version step at a time before publishing runtime assets.
    public interface IDefinitionMigration
    {
        string DefinitionType { get; }
        int FromVersion { get; }
        void Migrate(JObject definition);
    }
}
