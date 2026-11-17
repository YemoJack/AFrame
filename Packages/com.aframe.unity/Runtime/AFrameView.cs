using System;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;
using UnityEngine;
using EntityId = AFrame.Foundation.EntityId;

namespace AFrame.Unity
{
    public interface IViewContext
    {
        EntityId Entity { get; }
        T Query<T>() where T : class, IQuery;
        ICommandSink Commands { get; }
        void Subscribe<TEvent>(IEventSubscriber<TEvent> subscriber) where TEvent : struct, IDomainEvent;
    }

    public abstract class AFrameView : MonoBehaviour
    {
        public abstract void Bind(IViewContext context);
        public virtual void Unbind() { }
        public virtual void OnTickCompleted() { }
        public virtual void OnRender(float alpha) { }
    }

    internal sealed class ViewContext : IViewContext, IDisposable
    {
        private readonly ModuleContext context;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        public ViewContext(ModuleContext context, EntityId entity = default)
        {
            this.context = context;
            Entity = entity;
        }

        public EntityId Entity { get; }
        public ICommandSink Commands => context.Get<ICommandSink>();
        public T Query<T>() where T : class, IQuery => context.Get<T>();

        public void Subscribe<T>(IEventSubscriber<T> subscriber) where T : struct, IDomainEvent
        {
            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));
            subscriptions.Add(context.Get<IViewEventStream>().Subscribe<T>(value => subscriber.On(in value)));
        }

        public void Dispose()
        {
            foreach (var subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
        }
    }
}
