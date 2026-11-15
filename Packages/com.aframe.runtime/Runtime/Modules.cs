using System;
using System.Collections.Generic;
using System.Linq;
using AFrame.Foundation;

namespace AFrame.Runtime
{
    public interface IGameModule
    {
        ModuleId Id { get; }
        IReadOnlyList<ModuleId> Dependencies { get; }
        void Configure(IModuleBuilder builder);
        void Start(ModuleContext context);
        void Stop();
    }

    public interface IModuleBuilder
    {
        void AddService<T>(T service) where T : class;
        void AddCommandHandler<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : ICommand;
    }

    public sealed class ModuleContext
    {
        private readonly ServiceContainer services;

        internal ModuleContext(ServiceContainer services)
        {
            this.services = services;
        }

        public T Get<T>() where T : class
        {
            return services.GetRequired<T>();
        }

        public IClock Clock => Get<IClock>();
        public ICommandDispatcher Commands => Get<ICommandDispatcher>();
        public ICommandTransport Transport => Get<ICommandTransport>();
        public IEventStream Events => Get<IEventStream>();
    }

    public sealed class GameModuleHost : IDisposable
    {
        private readonly List<IGameModule> modules = new List<IGameModule>();
        private ModuleContext context;
        private bool running;

        public ModuleContext Context => context;
        public bool IsRunning => running;

        public void Start(IEnumerable<IGameModule> gameModules, IClock clock = null)
        {
            if (running)
                throw new InvalidOperationException("The game module host is already running.");

            var graph = new ModuleGraph(gameModules);
            modules.AddRange(graph.GetOrderedModules());

            var services = new ServiceContainer();
            var dispatcher = new CommandDispatcher();
            var events = new EventStream();

            services.Add<IClock>(clock ?? new SystemClock());
            services.Add<ICommandDispatcher>(dispatcher);
            services.Add<IEventStream>(events);
            services.Add<ICommandTransport>(new LocalCommandTransport(dispatcher));
            services.Add<IEventPublisher>(new EventPublisher(events));

            context = new ModuleContext(services);
            var builder = new ModuleBuilder(services, dispatcher);

            foreach (var module in modules)
                module.Configure(builder);

            foreach (var module in modules)
                module.Start(context);

            running = true;
        }

        public void Stop()
        {
            if (!running)
                return;

            for (var index = modules.Count - 1; index >= 0; index--)
                modules[index].Stop();

            modules.Clear();
            context = null;
            running = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }

    public interface IEventPublisher
    {
        void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;
    }

    internal sealed class EventPublisher : IEventPublisher
    {
        private readonly IEventStream events;

        public EventPublisher(IEventStream events)
        {
            this.events = events;
        }

        public void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            events.Publish(domainEvent);
        }
    }

    internal sealed class ModuleBuilder : IModuleBuilder
    {
        private readonly ServiceContainer services;
        private readonly ICommandDispatcher dispatcher;

        public ModuleBuilder(ServiceContainer services, ICommandDispatcher dispatcher)
        {
            this.services = services;
            this.dispatcher = dispatcher;
        }

        public void AddService<T>(T service) where T : class
        {
            services.Add(service);
        }

        public void AddCommandHandler<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : ICommand
        {
            dispatcher.Register(handler);
        }
    }

    internal sealed class ServiceContainer
    {
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        public void Add<T>(T service) where T : class
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var serviceType = typeof(T);
            if (services.ContainsKey(serviceType))
                throw new InvalidOperationException($"A service of type {serviceType.FullName} is already registered.");

            services.Add(serviceType, service);
        }

        public T GetRequired<T>() where T : class
        {
            if (!services.TryGetValue(typeof(T), out var value))
                throw new InvalidOperationException($"No service of type {typeof(T).FullName} is registered.");

            return (T)value;
        }
    }

    public sealed class ModuleGraph
    {
        private readonly Dictionary<ModuleId, IGameModule> byId;

        public ModuleGraph(IEnumerable<IGameModule> gameModules)
        {
            if (gameModules == null)
                throw new ArgumentNullException(nameof(gameModules));

            byId = new Dictionary<ModuleId, IGameModule>();
            foreach (var module in gameModules)
            {
                if (module == null)
                    throw new InvalidOperationException("A null game module was supplied.");
                if (!module.Id.IsValid)
                    throw new InvalidOperationException("Every game module must have a valid id.");
                if (byId.ContainsKey(module.Id))
                    throw new InvalidOperationException($"Duplicate game module id: {module.Id}.");

                byId.Add(module.Id, module);
            }
        }

        public IReadOnlyList<IGameModule> GetOrderedModules()
        {
            var ordered = new List<IGameModule>();
            var visiting = new HashSet<ModuleId>();
            var visited = new HashSet<ModuleId>();

            foreach (var module in byId.Values.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
                Visit(module, visiting, visited, ordered);

            return ordered;
        }

        private void Visit(
            IGameModule module,
            HashSet<ModuleId> visiting,
            HashSet<ModuleId> visited,
            List<IGameModule> ordered)
        {
            if (visited.Contains(module.Id))
                return;
            if (!visiting.Add(module.Id))
                throw new InvalidOperationException($"A cycle exists in the game module graph at {module.Id}.");

            foreach (var dependency in module.Dependencies ?? Array.Empty<ModuleId>())
            {
                if (!byId.TryGetValue(dependency, out var dependencyModule))
                    throw new InvalidOperationException(
                        $"Module {module.Id} depends on missing module {dependency}.");

                Visit(dependencyModule, visiting, visited, ordered);
            }

            visiting.Remove(module.Id);
            visited.Add(module.Id);
            ordered.Add(module);
        }
    }
}
