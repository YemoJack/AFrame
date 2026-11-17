using System;
using System.Collections.Generic;

namespace AFrame.Runtime
{
    public interface IDomainEvent { }

    public interface IEventStream
    {
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;
    }

    public interface IViewEventStream
    {
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;
    }

    public interface IEventPublisher
    {
        void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;
    }

    public interface IEventSink
    {
        void Raise<TEvent>(in TEvent domainEvent) where TEvent : struct, IDomainEvent;
    }

    public interface IEventSubscriber<TEvent> where TEvent : struct, IDomainEvent
    {
        void On(in TEvent domainEvent);
    }

    public sealed class EventStream : IEventStream, IViewEventStream, IEventPublisher, IEventSink
    {
        private readonly Dictionary<Type, List<Delegate>> subscribers =
            new Dictionary<Type, List<Delegate>>();
        private readonly Dictionary<Type, List<Delegate>> views = new Dictionary<Type, List<Delegate>>();
        private bool closed;
        private readonly Queue<Action> pending = new Queue<Action>();

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
            => Add(subscribers, handler);

        IDisposable IViewEventStream.Subscribe<TEvent>(Action<TEvent> handler) => Add(views, handler);

        private IDisposable Add<TEvent>(Dictionary<Type, List<Delegate>> targets, Action<TEvent> handler)
        {
            if (closed)
                throw new ObjectDisposedException(nameof(EventStream));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(TEvent);
            if (!targets.TryGetValue(eventType, out var eventSubscribers))
            {
                eventSubscribers = new List<Delegate>();
                targets.Add(eventType, eventSubscribers);
            }

            eventSubscribers.Add(handler);
            return new Subscription(() => eventSubscribers.Remove(handler));
        }

        public void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            if (closed)
                throw new ObjectDisposedException(nameof(EventStream));
            pending.Enqueue(() => Dispatch(domainEvent));
        }

        public void Raise<TEvent>(in TEvent domainEvent) where TEvent : struct, IDomainEvent
        {
            Publish(domainEvent);
        }

        public void Flush(int maxEvents = 1024)
        {
            var processed = 0;
            while (pending.Count > 0)
            {
                if (++processed > maxEvents)
                    throw new InvalidOperationException("The event queue exceeded its safety limit.");

                pending.Dequeue()();
            }
        }

        private void Dispatch<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            Deliver(subscribers, domainEvent);
            Deliver(views, domainEvent);
        }

        private static void Deliver<TEvent>(Dictionary<Type, List<Delegate>> targets, TEvent value)
        {
            if (targets.TryGetValue(typeof(TEvent), out var list))
                foreach (var subscriber in list.ToArray())
                    ((Action<TEvent>)subscriber)(value);
        }

        internal void Close()
        {
            closed = true;
            pending.Clear();
            subscribers.Clear();
            views.Clear();
        }

        private sealed class Subscription : IDisposable
        {
            private Action dispose;

            public Subscription(Action dispose) => this.dispose = dispose;

            public void Dispose()
            {
                dispose?.Invoke();
                dispose = null;
            }
        }
    }
}

namespace AFrame.Runtime
{
    using AFrame.Foundation;

    [StableId("aframe.entity_spawned")]
    public readonly struct EntitySpawned : IDomainEvent
    {
        public EntitySpawned(EntityId entity, ViewKey view) { Entity = entity; View = view; }
        public EntityId Entity { get; }
        public ViewKey View { get; }
    }

    [StableId("aframe.entity_despawned")]
    public readonly struct EntityDespawned : IDomainEvent
    {
        public EntityDespawned(EntityId entity) => Entity = entity;
        public EntityId Entity { get; }
    }
}
