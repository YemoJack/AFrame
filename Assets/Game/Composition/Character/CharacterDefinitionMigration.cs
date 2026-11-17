using System.IO;
using AFrame.Data;
using Newtonsoft.Json.Linq;

namespace AFrame.Game.Character
{
    public sealed class CharacterDefinitionMigration : IDefinitionMigration
    {
        public string DefinitionType => "character";
        public int FromVersion => 0;

        public void Migrate(JObject definition)
        {
            if (definition["moveSpeed"] == null || definition["speed"] != null)
                throw new InvalidDataException("Character schema 0 requires moveSpeed and must not include speed.");

            definition["speed"] = definition["moveSpeed"].DeepClone();
            definition.Remove("moveSpeed");
        }
    }
}
