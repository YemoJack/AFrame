using System;
using System.Collections.Generic;
using AFrame.Runtime;
using UnityEngine;

namespace AFrame.Unity
{
    public interface IViewContext
    {
        T Query<T>() where T : class, IQuery;
        ICommandSink Commands { get; }
        void Subscribe<TEvent>(IEventSubscriber<TEvent> subscriber)
            where TEvent : struct, IDomainEvent;
    }

    public abstract class AFrameView : MonoBehaviour
    {
        public abstract void Bind(IViewContext context);
        public virtual void Unbind() { }
        public virtual void OnTickCompleted() { }
        public virtual void OnRender(float alpha) { }
    }

    internal sealed class ViewContext : IViewContext
    {
        private readonly ModuleContext moduleContext;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        public ViewContext(ModuleContext moduleContext)
        {
            this.moduleContext = moduleContext;
        }

        public ICommandSink Commands => moduleContext.Get<ICommandSink>();

        public T Query<T>() where T : class, IQuery => moduleContext.Get<T>();

        public void Subscribe<TEvent>(IEventSubscriber<TEvent> subscriber)
            where TEvent : struct, IDomainEvent
        {
            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            subscriptions.Add(moduleContext.Events.Subscribe<TEvent>(eventValue => subscriber.On(in eventValue)));
        }

        public void Dispose()
        {
            foreach (var subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
        }
    }

    [DisallowMultipleComponent]
    public sealed class SceneScope : MonoBehaviour
    {
        [SerializeField] private AFrameBootstrap bootstrap;
        private readonly List<AFrameView> views = new List<AFrameView>();
        private ViewContext context;

        private void OnEnable()
        {
            if (bootstrap == null)
                bootstrap = FindBootstrapInParents();
            if (bootstrap == null || !bootstrap.IsRunning)
                return;

            context = new ViewContext(bootstrap.Context);
            views.Clear();
            views.AddRange(GetComponentsInChildren<AFrameView>(true));
            foreach (var view in views)
                view.Bind(context);
        }

        private void LateUpdate()
        {
            if (context == null)
                return;
            foreach (var view in views)
                view.OnRender(0f);
        }

        private void OnDisable()
        {
            foreach (var view in views)
                view.Unbind();
            context?.Dispose();
            context = null;
            views.Clear();
        }

        private AFrameBootstrap FindBootstrapInParents()
        {
            return GetComponentInParent<AFrameBootstrap>();
        }
    }
}
