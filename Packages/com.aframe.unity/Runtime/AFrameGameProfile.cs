using System.Collections.Generic;
using System.Linq;
using AFrame.Runtime;
using UnityEngine;

namespace AFrame.Unity
{
    [CreateAssetMenu(menuName = "AFrame/Game Profile", fileName = "GameProfile")]
    public sealed class AFrameGameProfile : ScriptableObject
    {
        [SerializeField] private string profileId = "default";
        [SerializeField] private List<AFrameModuleAsset> modules =
            new List<AFrameModuleAsset>();

        public string ProfileId => profileId;
        public IReadOnlyList<AFrameModuleAsset> Modules => modules;

        public void Configure(string id, IEnumerable<AFrameModuleAsset> moduleAssets)
        {
            profileId = string.IsNullOrWhiteSpace(id) ? "default" : id;
            modules = moduleAssets == null
                ? new List<AFrameModuleAsset>()
                : moduleAssets.Where(module => module != null).ToList();
        }

        public IReadOnlyList<IGameModule> CreateModules()
        {
            var result = new List<IGameModule>();
            foreach (var moduleAsset in modules)
            {
                if (moduleAsset == null)
                    continue;

                var module = moduleAsset.CreateModule();
                if (module == null)
                    throw new System.InvalidOperationException(
                        $"Module asset {moduleAsset.name} returned a null runtime module.");

                result.Add(module);
            }

            return result;
        }
    }
}
