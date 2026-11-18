using System;
using AFrame.Data;
using AFrame.Foundation;
using System.Collections.Generic;
using System.Linq;
using AFrame.Runtime;
using UnityEngine;

namespace AFrame.Unity
{
    public enum GameDimension { ThreeD, TwoD }
    public enum GameNetworkMode { Offline, Online }

    [CreateAssetMenu(menuName = "AFrame/Game Profile", fileName = "GameProfile")]
    public sealed class AFrameGameProfile : ScriptableObject
    {
        [SerializeField] private string profileId = "default";
        [SerializeField] private GameDimension dimension;
        [SerializeField] private GameNetworkMode networkMode;
        [SerializeField] private string[] enabledFeatures = Array.Empty<string>();
        [SerializeField, Min(1f)] private float tickRate = 50f;
        [SerializeField, Min(1)] private int maxTicksPerFrame = 5;
        [SerializeField] private List<AFrameModuleAsset> modules =
            new List<AFrameModuleAsset>();

        [Serializable]
        private sealed class ViewPrefab
        {
            public string key;
            public GameObject prefab;
        }

        [SerializeField] private DefinitionRegistry definitions;
        [SerializeField] private ViewPrefab[] views = Array.Empty<ViewPrefab>();

        public DefinitionRegistry Definitions => definitions;
        public void SetDefinitions(DefinitionRegistry value) => definitions = value;

        internal GameObject GetView(ViewKey key) =>
            views.FirstOrDefault(view => view != null && view.key == key.Value)?.prefab;

        public string ProfileId => profileId;
        public GameDimension Dimension => dimension;
        public GameNetworkMode NetworkMode => networkMode;
        public IReadOnlyList<string> EnabledFeatures => enabledFeatures;
        public float TickRate => tickRate;
        public float TickDeltaTime => 1f / Mathf.Max(1f, tickRate);
        public int MaxTicksPerFrame => Mathf.Max(1, maxTicksPerFrame);
        public IReadOnlyList<AFrameModuleAsset> Modules => modules;

        public void Configure(string id, IEnumerable<AFrameModuleAsset> moduleAssets)
        {
            profileId = string.IsNullOrWhiteSpace(id) ? "default" : id;
            modules = moduleAssets == null
                ? new List<AFrameModuleAsset>()
                : moduleAssets.Where(module => module != null).ToList();
        }

        public IReadOnlyList<IGameModule> CreateModules(ModuleScope? scope = null)
        {
            var result = new List<IGameModule>();
            foreach (var moduleAsset in modules)
            {
                if (moduleAsset == null)
                    throw new InvalidOperationException("AFrame profile contains a missing module asset.");
                if (scope.HasValue && moduleAsset.Scope != scope.Value)
                    continue;

                var module = moduleAsset.CreateModule();
                if (module == null)
                    throw new System.InvalidOperationException(
                        $"Module asset {moduleAsset.name} returned a null runtime module.");

                if (module.Scope != moduleAsset.Scope)
                    throw new InvalidOperationException($"Module asset {moduleAsset.name} declares a different scope from its module.");
                result.Add(module);
            }

            return result;
        }
    }
}
