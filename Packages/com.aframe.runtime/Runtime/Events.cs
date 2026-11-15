using System;
using System.Collections.Generic;

namespace AFrame.Runtime
{
    public interface IDomainEvent { }

    public interface IEventStream
    {
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;
        void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;
    }

    public sealed class EventStream : IEventStream
    {
        private readonly Dictionary<Type, List<Delegate>> subscribers =
            new Dictionary<Type, List<Delegate>>();

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
            if (!subscribers.TryGetValue(typeof(TEvent), out var eventSubscribers))
                return;

            foreach (var subscriber in eventSubscribers.ToArray())
                ((Action<TEvent>)subscriber)(domainEvent);
        }

        private sealed class Subscription : IDisposable
        {
            private Action dispose;

            public Subscription(Action dispose)
            {
                this.dispose = dispose;
            }

            public void Dispose()
            {
                dispose?.Invoke();
                dispose = null;
            }
        }
    }
}
