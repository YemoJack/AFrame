using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;
using AFrame.Unity;

namespace AFrame.Game.EnergyStation
{
    // Transient profile entries for the modules configured by the sample scene.
    internal sealed class StationModuleAsset : AFrameModuleAsset
    {
        private IGameModule module;
        public override IReadOnlyList<ModuleId> Dependencies => module.Dependencies;
        public override IGameModule CreateModule() => module;

        public void Initialize(IGameModule value)
        {
            module = value;
            EnsureModuleId(value.Id.Value);
        }
    }
}
