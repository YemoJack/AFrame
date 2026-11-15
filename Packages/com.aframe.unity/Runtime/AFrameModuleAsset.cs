using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;
using UnityEngine;

namespace AFrame.Unity
{
    public abstract class AFrameModuleAsset : ScriptableObject
    {
        [SerializeField] private string moduleId;

        public ModuleId Id => new ModuleId(moduleId);
        public string IdString => moduleId;
        public abstract IReadOnlyList<ModuleId> Dependencies { get; }
        public abstract IGameModule CreateModule();

        protected void EnsureModuleId(string defaultId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                moduleId = defaultId;
        }
    }
}
