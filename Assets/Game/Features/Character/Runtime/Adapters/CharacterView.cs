using System;
using AFrame.Foundation;
using AFrame.Unity;
using UnityEngine;

namespace AFrame.Game.Character
{
    public sealed class CharacterView : MonoBehaviour
    {
        [SerializeField] private AFrameBootstrap bootstrap;
        private IDisposable subscription;

        public void SetBootstrap(AFrameBootstrap value)
        {
            bootstrap = value;
        }

        private void Start()
        {
            if (bootstrap == null || !bootstrap.IsRunning)
                return;

            subscription = bootstrap.Context.Events.Subscribe<CharacterMovedEvent>(OnCharacterMoved);
            ApplyPosition(bootstrap.Context.Get<ICharacterState>().Position);
        }

        private void OnDestroy()
        {
            subscription?.Dispose();
        }

        private void OnCharacterMoved(CharacterMovedEvent domainEvent)
        {
            ApplyPosition(domainEvent.Position);
        }

        private void ApplyPosition(Float3 position)
        {
            transform.position = new Vector3(position.x, position.y, position.z);
        }
    }
}
