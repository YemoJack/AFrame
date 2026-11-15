using System;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;

namespace AFrame.Game.Character
{
    public readonly struct MoveCharacterCommand : ICommand
    {
        public MoveCharacterCommand(Float3 direction)
        {
            Direction = direction;
        }

        public Float3 Direction { get; }
    }

    public readonly struct CharacterMovedEvent : IDomainEvent
    {
        public CharacterMovedEvent(Float3 position)
        {
            Position = position;
        }

        public Float3 Position { get; }
    }

    public interface ICharacterState
    {
        Float3 Position { get; }
        float Speed { get; }
    }

    public sealed class CharacterModule : IGameModule, ICommandHandler<MoveCharacterCommand>
    {
        private static readonly IReadOnlyList<ModuleId> NoDependencies = Array.Empty<ModuleId>();

        private readonly CharacterState state;
        private IEventPublisher events;

        public CharacterModule(float speed)
        {
            state = new CharacterState(speed);
        }

        public ModuleId Id => new ModuleId("game.character");
        public ModuleScope Scope => ModuleScope.Session;
        public IReadOnlyList<ModuleId> Dependencies => NoDependencies;

        public void Configure(IModuleBuilder builder)
        {
            builder.AddService<ICharacterState>(state);
            builder.AddCommandHandler(this);
        }

        public void Start(ModuleContext context)
        {
            events = context.Get<IEventPublisher>();
            state.SetTickDeltaTime(context.TickDeltaTime);
        }

        public void Stop()
        {
            events = null;
        }

        public Result Handle(in CommandEnvelope envelope, in MoveCharacterCommand command)
        {
            state.Position += command.Direction * (state.Speed * state.TickDeltaTime);
            events?.Publish(new CharacterMovedEvent(state.Position));
            return Result.Ok();
        }

        private sealed class CharacterState : ICharacterState
        {
            public CharacterState(float speed)
            {
                Speed = Math.Max(0f, speed);
            }

            public Float3 Position { get; set; }
            public float Speed { get; }
            public float TickDeltaTime { get; private set; }

            public void SetTickDeltaTime(float value)
            {
                TickDeltaTime = value;
            }
        }
    }
}
