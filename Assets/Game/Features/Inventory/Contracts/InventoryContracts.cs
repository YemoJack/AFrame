using AFrame.Foundation;
using AFrame.Runtime;

namespace AFrame.Game.Inventory
{
    [StableId("inventory.store_energy_core")]
    public readonly struct StoreEnergyCore : ICommand
    {
        public StoreEnergyCore(int coreId) => CoreId = coreId;
        public int CoreId { get; }
    }

    [StableId("inventory.energy_core_stored")]
    public readonly struct EnergyCoreStored : IDomainEvent
    {
        public EnergyCoreStored(int coreId) => CoreId = coreId;
        public int CoreId { get; }
    }

    public interface IInventoryQuery : IQuery
    {
        int Collected { get; }
        int Total { get; }
        bool Contains(int coreId);
    }
}
