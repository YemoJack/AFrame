using System;
using System.Collections.Generic;
using System.Linq;
using AFrame.Foundation;

namespace AFrame.Runtime
{
    public enum ModuleScope { App, Session, Scene }

    public readonly struct TickContext
    {
        public TickContext(long tick, float deltaTime)
        {
            Tick = tick;
            DeltaTime = deltaTime;
        }

        public long Tick { get; }
        public float DeltaTime { get; }
    }

    public interface IFixedTickable
    {
        void FixedTick(in TickContext context);
    }

    public interface IQuery { }

    public interface IInputSource<TInput> where TInput : struct
    {
        TInput Get(PlayerId player);
    }

    public interface IGameModule
    {
        ModuleId Id { get; }
        ModuleScope Scope { get; }
        IReadOnlyList<ModuleId> Dependencies { get; }
        void Configure(IModuleBuilder builder);
        void Start(ModuleContext context);
        void Stop();
    }

    public interface IModuleBuilder
    {
        void AddService<T>(T service) where T : class;
        void AddCommandHandler<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;
        void AddFixedTickable(IFixedTickable tickable);
    }

    public sealed class ModuleContext
    {
        private readonly ServiceContainer services;

        internal ModuleContext(ServiceContainer services, float tickDeltaTime)
        {
            this.services = services;
            TickDeltaTime = tickDeltaTime;
        }

        public float TickDeltaTime { get; }
        public long Tick { get; internal set; }
        public T Get<T>() where T : class => services.GetRequired<T>();
        public IClock Clock => Get<IClock>();
        public ICommandTransport Transport => Get<ICommandTransport>();
        public IEventStream Events => Get<IEventStream>();
    }

    public sealed class GameModuleHost : IDisposable
    {
        private readonly List<IGameModule> modules = new List<IGameModule>();
        private readonly List<IFixedTickable> tickables = new List<IFixedTickable>();
        private ModuleContext context;
        private ICommandQueue queue;
        private ICommandExecutor executor;
        private EventStream events;
        private bool running;

        public ModuleContext Context => context;
        public bool IsRunning => running;

        public void Start(IEnumerable<IGameModule> gameModules, IClock clock = null, float tickDeltaTime = 0.02f)
        {
            if (running)
                throw new InvalidOperationException("The game module host is already running.");
            if (tickDeltaTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));

            var graph = new ModuleGraph(gameModules);
            modules.AddRange(graph.GetOrderedModules());

            var services = new ServiceContainer();
            var dispatcher = new CommandDispatcher();
            var transport = new LocalCommandTransport();
            events = new EventStream();
            queue = transport;
            executor = dispatcher;

            services.Add<IClock>(clock ?? new SystemClock());
            services.Add<ICommandDispatcher>(dispatcher);
            services.Add<ICommandTransport>(transport);
            services.Add<ICommandSink>(transport);
            services.Add<IEventStream>(events);
            services.Add<IEventPublisher>(events);

            context = new ModuleContext(services, tickDeltaTime);
            var builder = new ModuleBuilder(services, dispatcher, tickables);

            foreach (var module in modules)
                module.Configure(builder);
            foreach (var module in modules)
                module.Start(context);

            running = true;
        }

        public void Tick()
        {
            if (!running)
                return;

            context.Tick++;
            queue.BeginTick(context.Tick);
            foreach (var command in queue.Drain(context.Tick))
            {
                var result = executor.Dispatch(in command);
                if (result.Failed)
                    events.Publish(new CommandRejectedEvent(command, result.Error));
            }

            var tickContext = new TickContext(context.Tick, context.TickDeltaTime);
            foreach (var tickable in tickables)
                tickable.FixedTick(in tickContext);

            events.Flush();
        }

        public void Stop()
        {
            if (!running)
                return;

            for (var index = modules.Count - 1; index >= 0; index--)
                modules[index].Stop();

            modules.Clear();
            tickables.Clear();
            context = null;
            queue = null;
            executor = null;
            events = null;
            running = false;
        }

        public void Dispose() => Stop();
    }

    [StableId("aframe.command_rejected")]
    public readonly struct CommandRejectedEvent : IDomainEvent
    {
        public CommandRejectedEvent(CommandEnvelope command, AFrameError error)
        {
            Command = command;
            Error = error;
        }

        public CommandEnvelope Command { get; }
        public AFrameError Error { get; }
    }

    internal sealed class ModuleBuilder : IModuleBuilder
    {
        private readonly ServiceContainer services;
        private readonly ICommandDispatcher dispatcher;
        private readonly List<IFixedTickable> tickables;

        public ModuleBuilder(ServiceContainer services, ICommandDispatcher dispatcher, List<IFixedTickable> tickables)
        {
            this.services = services;
            this.dispatcher = dispatcher;
            this.tickables = tickables;
        }

        public void AddService<T>(T service) where T : class => services.Add(service);
        public void AddCommandHandler<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand => dispatcher.Register(handler);

        public void AddFixedTickable(IFixedTickable tickable)
        {
            if (tickable == null)
                throw new ArgumentNullException(nameof(tickable));
            tickables.Add(tickable);
        }
    }

    internal sealed class ServiceContainer
    {
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        public void Add<T>(T service) where T : class
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            if (!services.TryAdd(typeof(T), service))
                throw new InvalidOperationException($"A service of type {typeof(T).FullName} is already registered.");
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
                if (module == null || !module.Id.IsValid)
                    throw new InvalidOperationException("Every game module must have a valid id.");
                if (!byId.TryAdd(module.Id, module))
                    throw new InvalidOperationException($"Duplicate game module id: {module.Id}.");
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

        private void Visit(IGameModule module, HashSet<ModuleId> visiting, HashSet<ModuleId> visited, List<IGameModule> ordered)
        {
            if (visited.Contains(module.Id))
                return;
            if (!visiting.Add(module.Id))
                throw new InvalidOperationException($"A cycle exists in the game module graph at {module.Id}.");

            foreach (var dependency in module.Dependencies ?? Array.Empty<ModuleId>())
            {
                if (!byId.TryGetValue(dependency, out var dependencyModule))
                    throw new InvalidOperationException($"Module {module.Id} depends on missing module {dependency}.");
                if (module.Scope < dependencyModule.Scope)
                    throw new InvalidOperationException(
                        $"Module {module.Id} ({module.Scope}) cannot depend on inner-scope module " +
                        $"{dependencyModule.Id} ({dependencyModule.Scope}).");
                Visit(dependencyModule, visiting, visited, ordered);
            }

            visiting.Remove(module.Id);
            visited.Add(module.Id);
            ordered.Add(module);
        }
    }
}
