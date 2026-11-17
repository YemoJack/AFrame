using System;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;

namespace AFrame.Game.Character
{
    public sealed class CharacterInputModule : IGameModule
    {
        private readonly CharacterInputAdapter input;

        public CharacterInputModule(CharacterInputAdapter input) => this.input = input;
        public ModuleId Id => new ModuleId("game.character-input");
        public ModuleScope Scope => ModuleScope.Session;
        public IReadOnlyList<ModuleId> Dependencies => Array.Empty<ModuleId>();
        public void Configure(IModuleBuilder builder)
        {
            builder.AddService<IInputSource<CharacterInput>>(input);
            builder.ProvideMany<ITickInput>(_ => input);
        }

        public void Start(ModuleContext context) { }
        public void Stop() { }
    }
}
