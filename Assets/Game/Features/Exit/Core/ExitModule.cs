using System;
using AFrame.Foundation;
using AFrame.Runtime;
using System.Collections.Generic;
using AFrame.Game.Inventory;

namespace AFrame.Game.Exit
{
    public sealed class ExitModule : IGameModule
    {
        private readonly float timeLimitSeconds;
        private ExitState state;

        public ExitModule(float timeLimitSeconds = 45f)
        {
            if (float.IsNaN(timeLimitSeconds) || float.IsInfinity(timeLimitSeconds) || timeLimitSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds));
            this.timeLimitSeconds = timeLimitSeconds;
        }

        public ModuleId Id => new ModuleId("game.exit");
        public ModuleScope Scope => ModuleScope.Session;
        public IReadOnlyList<ModuleId> Dependencies => new[] { new ModuleId("game.inventory") };

        public void Configure(IModuleBuilder builder)
        {
            state = new ExitState(timeLimitSeconds);
            builder.AddService<IStationExitQuery>(state);
            builder.AddCommandHandler(state);
            builder.AddFixedTickable(state);
        }

        public void Start(ModuleContext context)
        {
            state.Inventory = context.Get<IInventoryQuery>();
            state.Events = context.Get<IEventPublisher>();
        }

        public void Stop()
        {
            state.Inventory = null;
            state.Events = null;
        }

        private sealed class ExitState : IStationExitQuery, ICommandHandler<ReachStationExit>, IFixedTickable
        {
            public ExitState(float timeLimitSeconds) => SecondsRemaining = timeLimitSeconds;

            public IInventoryQuery Inventory { get; set; }
            public IEventPublisher Events { get; set; }
            public bool IsUnlocked => !HasTimedOut && Inventory != null && Inventory.Collected == Inventory.Total;
            public bool IsCleared { get; private set; }
            public bool HasTimedOut { get; private set; }
            public double SecondsRemaining { get; private set; }

            public void FixedTick(in TickContext context)
            {
                if (IsCleared || HasTimedOut)
                    return;

                SecondsRemaining -= context.DeltaTime;
                // Allow for rounding from the float Tick step at the deadline.
                if (SecondsRemaining > 0.00001d)
                    return;

                SecondsRemaining = 0d;
                HasTimedOut = true;
                Events.Publish(new StationTimedOut());
            }

            public Result Handle(in CommandEnvelope envelope, in ReachStationExit command)
            {
                if (IsCleared)
                    return Result.Ok();
                if (HasTimedOut)
                    return Result.Fail("exit.timed_out", "The station timer has expired.");
                if (!IsUnlocked)
                    return Result.Fail("exit.locked", "Collect every energy core first.");

                IsCleared = true;
                Events.Publish(new StationCleared());
                return Result.Ok();
            }
        }
    }
}
