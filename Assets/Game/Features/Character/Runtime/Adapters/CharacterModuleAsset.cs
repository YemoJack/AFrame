using System;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;
using AFrame.Unity;
using UnityEngine;

namespace AFrame.Game.Character
{
    [CreateAssetMenu(menuName = "AFrame/Modules/Character", fileName = "CharacterModule")]
    public sealed class CharacterModuleAsset : AFrameModuleAsset
    {
        private static readonly IReadOnlyList<ModuleId> NoDependencies = Array.Empty<ModuleId>();

        [SerializeField] private float speed = 5f;
        [SerializeField] private Vector2 movementBounds;

        public override IReadOnlyList<ModuleId> Dependencies => NoDependencies;

        public override IGameModule CreateModule()
        {
            return new CharacterModule(speed, new Float3(movementBounds.x, 0f, movementBounds.y));
        }

        public void SetMovementBounds(Vector2 bounds) => movementBounds = bounds;

        public void SetDefaults()
        {
            EnsureModuleId("game.character");
        }

        private void OnValidate()
        {
            EnsureModuleId("game.character");
        }
    }
}
