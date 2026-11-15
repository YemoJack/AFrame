using System;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;

namespace AFrame.Game.Character
{
    public sealed class CharacterModule : IGameModule, ICommandHandler<MoveCharacterCommand>,
        ICommandHandler<SetCharacterMovement>, IFixedTickable
    {
        private static readonly IReadOnlyList<ModuleId> NoDependencies = Array.Empty<ModuleId>();
        private readonly CharacterState state;
        private readonly Float3 movementBounds;
        private IEventPublisher events;
        private Float3 direction;
        private float tickDeltaTime;
        private bool sprinting;

        public CharacterModule(float speed, Float3 movementBounds = default)
        {
            state = new CharacterState(speed);
            this.movementBounds = movementBounds;
        }

        public ModuleId Id => new ModuleId("game.character");
        public ModuleScope Scope => ModuleScope.Session;
        public IReadOnlyList<ModuleId> Dependencies => NoDependencies;

        public void Configure(IModuleBuilder builder)
        {
            builder.AddService<ICharacterState>(state);
            builder.AddCommandHandler<MoveCharacterCommand>(this);
            builder.AddCommandHandler<SetCharacterMovement>(this);
            builder.AddFixedTickable(this);
        }

        public void Start(ModuleContext context)
        {
            events = context.Get<IEventPublisher>();
            tickDeltaTime = context.TickDeltaTime;
            direction = Float3.Zero;
            sprinting = false;
        }

        public void Stop() => events = null;

        public Result Handle(in CommandEnvelope envelope, in MoveCharacterCommand command)
        {
            Move(command.Direction, tickDeltaTime);
            return Result.Ok();
        }

        public Result Handle(in CommandEnvelope envelope, in SetCharacterMovement command)
        {
            direction = command.Direction;
            sprinting = command.Sprinting;
            return Result.Ok();
        }

        public void FixedTick(in TickContext context)
        {
            const float sprintMultiplier = 1.65f;
            Move(direction, context.DeltaTime, sprinting ? sprintMultiplier : 1f);
        }

        private void Move(Float3 movement, float deltaTime, float speedMultiplier = 1f)
        {
            if (movement.Equals(Float3.Zero))
                return;

            var next = state.Position + movement * (state.Speed * speedMultiplier * deltaTime);
            if (movementBounds.x > 0f)
                next.x = Math.Max(-movementBounds.x, Math.Min(movementBounds.x, next.x));
            if (movementBounds.z > 0f)
                next.z = Math.Max(-movementBounds.z, Math.Min(movementBounds.z, next.z));
            if (next.Equals(state.Position))
                return;

            state.Position = next;
            events.Publish(new CharacterMovedEvent(next));
        }

        private sealed class CharacterState : ICharacterState
        {
            public CharacterState(float speed) => Speed = Math.Max(0f, speed);
            public Float3 Position { get; set; }
            public float Speed { get; }
        }
    }
}
