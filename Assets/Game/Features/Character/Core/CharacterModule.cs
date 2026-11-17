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
        private CharacterState state;
        private readonly Float3 movementBounds;
        private IEventSink events;
        private IInputSource<CharacterInput> inputs;
        private readonly string definitionId;
        private Float3 direction;
        private float tickDeltaTime;
        private bool sprinting;

        public CharacterModule(string definitionId, Float3 movementBounds = default)
        {
            this.definitionId = definitionId;
            this.movementBounds = movementBounds;
        }

        public ModuleId Id => new ModuleId("game.character");
        public ModuleScope Scope => ModuleScope.Session;
        public IReadOnlyList<ModuleId> Dependencies => NoDependencies;

        public void Configure(IModuleBuilder builder)
        {
            builder.Require<ICharacterDefinitions>();
            builder.Require<IInputSource<CharacterInput>>();
            builder.Require<IEventSink>();
            builder.Provide<CharacterState>(services =>
                state = new CharacterState(services.Get<ICharacterDefinitions>().Get(definitionId).speed, movementBounds));
            builder.Provide<ICharacterState>(services => services.Get<CharacterState>());
            builder.Provide<ISnapshotParticipant<Float3>>(services => services.Get<CharacterState>());
            builder.AddCommandHandler<MoveCharacterCommand>(this);
            builder.AddCommandHandler<SetCharacterMovement>(this);
            builder.AddFixedTickable(this);
        }

        public void Start(ModuleContext context)
        {
            events = context.Get<IEventSink>();
            inputs = context.Get<IInputSource<CharacterInput>>();
            tickDeltaTime = context.TickDeltaTime;
            direction = Float3.Zero;
            sprinting = false;
        }

        public void Stop() => events = null;

        public Result Handle(in CommandEnvelope envelope, in MoveCharacterCommand command)
        {
            if (!ValidDirection(command.Direction))
                return Result.Fail("character.invalid_direction", "Invalid movement direction.");
            Move(command.Direction, tickDeltaTime);
            return Result.Ok();
        }

        public Result Handle(in CommandEnvelope envelope, in SetCharacterMovement command)
        {
            if (!ValidDirection(command.Direction))
                return Result.Fail("character.invalid_direction", "Invalid movement direction.");
            direction = command.Direction;
            sprinting = command.Sprinting;
            return Result.Ok();
        }

        public void FixedTick(in TickContext context)
        {
            const float sprintMultiplier = 1.65f;
            var input = inputs.Get(PlayerId.None);
            var movement = input.Movement.Equals(Float3.Zero) ? direction : input.Movement;
            Move(movement, context.DeltaTime, (input.Sprinting || sprinting) ? sprintMultiplier : 1f);
        }

        private static bool ValidDirection(Float3 value) =>
            value.y == 0f && value.x * value.x + value.z * value.z <= 1.0001f;

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
            events.Raise(new CharacterMovedEvent(next));
        }

        private sealed class CharacterState : ICharacterState, ISnapshotParticipant<Float3>
        {
            private readonly Float3 bounds;
            public CharacterState(float speed, Float3 bounds)
            {
                Speed = speed;
                this.bounds = bounds;
            }

            public Float3 Capture() => Position;

            public void Validate(Float3 value)
            {
                if (!Finite(value.x) || value.y != 0f || !Finite(value.z)
                    || bounds.x > 0f && Math.Abs(value.x) > bounds.x
                    || bounds.z > 0f && Math.Abs(value.z) > bounds.z)
                    throw new ArgumentException("Invalid saved character position.");
            }

            private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

            public void Restore(Float3 value) { Validate(value); Position = value; }
            public Float3 Position { get; set; }
            public float Speed { get; }
        }
    }
}
