using System;
using AFrame.Data;

namespace AFrame.Game.Character
{
    [DefinitionType("character")]
    public sealed class CharacterDefinition : IDefinition
    {
        public float speed;

        public void Validate()
        {
            if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed))
                throw new ArgumentException("Character speed must be positive and finite.");
        }
    }
}

namespace AFrame.Game.Character
{
    public interface ICharacterDefinitions
    {
        CharacterDefinition Get(string id);
    }
}
