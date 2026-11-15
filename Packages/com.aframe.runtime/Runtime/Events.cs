using System;
using System.Collections.Generic;

namespace AFrame.Runtime
{
    public interface IDomainEvent { }

    public interface IEventStream
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

    public sealed class EventStream : IEventStream, IEventPublisher, IEventSink
    {
        private readonly Dictionary<Type, List<Delegate>> subscribers =
            new Dictionary<Type, List<Delegate>>();
        private readonly Queue<Action> pending = new Queue<Action>();

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(TEvent);
            if (!subscribers.TryGetValue(eventType, out var eventSubscribers))
            {
                eventSubscribers = new List<Delegate>();
                subscribers.Add(eventType, eventSubscribers);
            }

            eventSubscribers.Add(handler);
            return new Subscription(() => eventSubscribers.Remove(handler));
        }

        public void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            pending.Enqueue(() => Dispatch(domainEvent));
        }

        public void Raise<TEvent>(in TEvent domainEvent) where TEvent : struct, IDomainEvent
        {
            Publish(domainEvent);
        }

        public void Flush(int maxEvents = 4096)
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
            if (!subscribers.TryGetValue(typeof(TEvent), out var eventSubscribers))
                return;

            foreach (var subscriber in eventSubscribers.ToArray())
                ((Action<TEvent>)subscriber)(domainEvent);
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
