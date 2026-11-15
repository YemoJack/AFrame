using System.Collections.Generic;
using UnityEngine;

namespace AFrame.Data
{
    [CreateAssetMenu(menuName = "AFrame/Data Definition Registry", fileName = "DefinitionRegistry")]
    public sealed class DefinitionRegistry : ScriptableObject
    {
        [SerializeField] private List<AFrameDefinitionAsset> definitions =
            new List<AFrameDefinitionAsset>();

        public IReadOnlyList<AFrameDefinitionAsset> Definitions => definitions;

        public bool TryGet(string id, out AFrameDefinitionAsset definition)
        {
            foreach (var candidate in definitions)
            {
                if (candidate != null && candidate.DefinitionId == id)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
