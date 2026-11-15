using AFrame.Foundation;
using AFrame.Runtime;
using System.Collections.Generic;
using AFrame.Game.Inventory;

namespace AFrame.Game.Pickup
{
    public sealed class PickupModule : IGameModule
    {
        private readonly PickupHandler handler = new PickupHandler();
        public ModuleId Id => new ModuleId("game.pickup");
        public ModuleScope Scope => ModuleScope.Session;
        public IReadOnlyList<ModuleId> Dependencies => new[] { new ModuleId("game.inventory") };

        public void Configure(IModuleBuilder builder) => builder.AddCommandHandler(handler);
        public void Start(ModuleContext context) => handler.Commands = context.Get<ICommandSink>();
        public void Stop() => handler.Commands = null;

        private sealed class PickupHandler : ICommandHandler<CollectEnergyCore>
        {
            public ICommandSink Commands { get; set; }

            public Result Handle(in CommandEnvelope envelope, in CollectEnergyCore command)
            {
                Commands.Submit(envelope.Issuer, new StoreEnergyCore(command.CoreId));
                return Result.Ok();
            }
        }
    }
}
