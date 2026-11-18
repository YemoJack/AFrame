using System;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;
using UnityEngine;
using EntityId = AFrame.Foundation.EntityId;

namespace AFrame.Unity
{
    internal sealed class ViewFactory : IDisposable
    {
        private readonly AFrameBootstrap bootstrap;
        private readonly AFrameGameProfile profile;
        private readonly Dictionary<EntityId, GameObject> instances = new Dictionary<EntityId, GameObject>();
        private readonly IDisposable spawn;
        private readonly IDisposable despawn;

        public ViewFactory(AFrameBootstrap bootstrap, AFrameGameProfile profile)
        {
            this.bootstrap = bootstrap;
            this.profile = profile;
            var events = bootstrap.Context.Get<IViewEventStream>();
            spawn = events.Subscribe<EntitySpawned>(Spawn);
            despawn = events.Subscribe<EntityDespawned>(message => Despawn(message.Entity));
        }

        private void Spawn(EntitySpawned message)
        {
            if (message.Entity.Equals(EntityId.None) || instances.ContainsKey(message.Entity))
                throw new InvalidOperationException($"Invalid or duplicate entity {message.Entity.Value}.");
            var prefab = profile.GetView(message.View);
            if (prefab == null)
                throw new InvalidOperationException($"No prefab registered for view {message.View.Value}.");
            var instance = UnityEngine.Object.Instantiate(prefab);
            instances.Add(message.Entity, instance);
            foreach (var view in instance.GetComponentsInChildren<AFrameView>(true))
                bootstrap.BindView(view, message.Entity);
        }

        private void Despawn(EntityId entity)
        {
            if (!instances.Remove(entity, out var instance) || instance == null)
                return;
            foreach (var view in instance.GetComponentsInChildren<AFrameView>(true))
                bootstrap.UnbindView(view);
            instance.SetActive(false);
            UnityEngine.Object.Destroy(instance);
        }

        public void Dispose()
        {
            spawn.Dispose();
            despawn.Dispose();
            foreach (var entity in new List<EntityId>(instances.Keys))
                Despawn(entity);
        }
    }
}
