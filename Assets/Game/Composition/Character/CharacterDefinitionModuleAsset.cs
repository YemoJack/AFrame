using System;
using System.Collections.Generic;
using AFrame.Data;
using AFrame.Foundation;
using AFrame.Runtime;
using AFrame.Unity;
using UnityEngine;

namespace AFrame.Game.Character
{
    [CreateAssetMenu(menuName = "AFrame/Modules/Character Definitions")]
    public sealed class CharacterDefinitionModuleAsset : AFrameModuleAsset
    {
        private void OnValidate() => EnsureModuleId("game.character-definitions");
        public override ModuleScope Scope => ModuleScope.App;
        public override IReadOnlyList<ModuleId> Dependencies => Array.Empty<ModuleId>();
        public override IGameModule CreateModule() => new Module();

        private sealed class Module : IGameModule
        {
            public ModuleId Id => new ModuleId("game.character-definitions");
            public ModuleScope Scope => ModuleScope.App;
            public IReadOnlyList<ModuleId> Dependencies => Array.Empty<ModuleId>();
            public void Configure(IModuleBuilder builder)
            {
                builder.Require<DefinitionRegistry>();
                builder.Provide<ICharacterDefinitions>(services => new CharacterDefinitions(services.Get<DefinitionRegistry>()));
            }
            public void Start(ModuleContext context) { }
            public void Stop() { }
        }

        private sealed class CharacterDefinitions : ICharacterDefinitions
        {
            private readonly Dictionary<string, CharacterDefinition> definitions = new Dictionary<string, CharacterDefinition>();

            public CharacterDefinitions(DefinitionRegistry registry)
            {
                foreach (var asset in registry.Definitions)
                    if (asset.DefinitionType == "character")
                        definitions.Add(asset.DefinitionId, DefinitionSourceReader.ReadDefinition<CharacterDefinition>(asset.SourceJson));
            }

            public CharacterDefinition Get(string id) => definitions.TryGetValue(id, out var value)
                ? new CharacterDefinition { speed = value.speed }
                : throw new KeyNotFoundException($"Missing character definition {id}.");
        }
    }
}
