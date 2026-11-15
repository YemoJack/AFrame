using AFrame.Foundation;
using AFrame.Runtime;

namespace AFrame.Game.Pickup
{
    [StableId("pickup.collect_energy_core")]
    public readonly struct CollectEnergyCore : ICommand
    {
        public CollectEnergyCore(int coreId) => CoreId = coreId;
        public int CoreId { get; }
    }
}
