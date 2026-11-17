using AFrame.Foundation;
using AFrame.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

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
            builder.Require<IEventSink>();
            inventory = new Inventory(total);
            builder.AddService<IInventoryQuery>(inventory);
            builder.AddService<ISnapshotParticipant<int[]>>(inventory);
            builder.AddCommandHandler(inventory);
        }

        public void Start(ModuleContext context) => inventory.Events = context.Get<IEventSink>();
        public void Stop() => inventory.Events = null;

        private sealed class Inventory : IInventoryQuery, ICommandHandler<StoreEnergyCore>, ISnapshotParticipant<int[]>
        {
            private readonly HashSet<int> stored = new HashSet<int>();

            public Inventory(int total) => Total = total;
            public int Collected => stored.Count;
            public int Total { get; }
            public IEventSink Events { get; set; }
            public bool Contains(int coreId) => stored.Contains(coreId);

            public int[] Capture() => stored.OrderBy(id => id).ToArray();

            public void Validate(int[] snapshot)
            {
                if (snapshot == null || snapshot.Any(id => id < 0 || id >= Total)
                    || snapshot.Distinct().Count() != snapshot.Length)
                    throw new ArgumentException("Invalid saved energy cores.");
            }

            public void Restore(int[] snapshot)
            {
                Validate(snapshot);
                stored.Clear();
                stored.UnionWith(snapshot);
            }

            public Result Handle(in CommandEnvelope envelope, in StoreEnergyCore command)
            {
                var id = command.CoreId;
                if (id < 0 || id >= Total)
                    return Result.Fail("inventory.invalid_core", "Unknown energy core.");
                if (!stored.Add(id))
                    return Result.Ok();

                Events.Raise(new EnergyCoreStored(id));
                return Result.Ok();
            }
        }
    }
}
