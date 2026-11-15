using AFrame.Foundation;
using AFrame.Runtime;
using System;
using System.Collections.Generic;

namespace AFrame.Game.Inventory
{
    public sealed class InventoryModule : IGameModule
    {
        private readonly int total;

        public InventoryModule(int total)
        {
            if (total < 1)
                throw new ArgumentOutOfRangeException(nameof(total));
            this.total = total;
        }

        public ModuleId Id => new ModuleId("game.inventory");
        public ModuleScope Scope => ModuleScope.Session;
        public IReadOnlyList<ModuleId> Dependencies => Array.Empty<ModuleId>();

        private Inventory inventory;

        public void Configure(IModuleBuilder builder)
        {
            inventory = new Inventory(total);
            builder.AddService<IInventoryQuery>(inventory);
            builder.AddCommandHandler(inventory);
        }

        public void Start(ModuleContext context) => inventory.Events = context.Get<IEventPublisher>();
        public void Stop() => inventory.Events = null;

        private sealed class Inventory : IInventoryQuery, ICommandHandler<StoreEnergyCore>
        {
            private readonly HashSet<int> stored = new HashSet<int>();

            public Inventory(int total) => Total = total;
            public int Collected => stored.Count;
            public int Total { get; }
            public IEventPublisher Events { get; set; }
            public bool Contains(int coreId) => stored.Contains(coreId);

            public Result Handle(in CommandEnvelope envelope, in StoreEnergyCore command)
            {
                var id = command.CoreId;
                if (id < 0 || id >= Total)
                    return Result.Fail("inventory.invalid_core", "Unknown energy core.");
                if (!stored.Add(id))
                    return Result.Ok();

                Events.Publish(new EnergyCoreStored(id));
                return Result.Ok();
            }
        }
    }
}
