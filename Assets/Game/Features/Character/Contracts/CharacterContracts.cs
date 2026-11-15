using AFrame.Foundation;
using AFrame.Runtime;

namespace AFrame.Game.Character
{
    [StableId("character.move")]
    public readonly struct MoveCharacterCommand : ICommand
    {
        public MoveCharacterCommand(Float3 direction) => Direction = direction;
        public Float3 Direction { get; }
    }

    [StableId("character.set_movement")]
    public readonly struct SetCharacterMovement : ICommand
    {
        public SetCharacterMovement(Float3 direction, bool sprinting = false)
        {
            Direction = direction;
            Sprinting = sprinting;
        }

        public Float3 Direction { get; }
        public bool Sprinting { get; }
    }

    [StableId("character.moved")]
    public readonly struct CharacterMovedEvent : IDomainEvent
    {
        public CharacterMovedEvent(Float3 position) => Position = position;
        public Float3 Position { get; }
    }

    public interface ICharacterState : IQuery
    {
        Float3 Position { get; }
        float Speed { get; }
    }
}
