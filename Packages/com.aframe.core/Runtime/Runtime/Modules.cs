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

    public interface ITickInput { void Freeze(); }

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
        void Require<T>() where T : class;
        void Provide<T>(Func<IServiceResolver, T> factory) where T : class;
        void ProvideMany<T>(Func<IServiceResolver, T> factory) where T : class;
        void Subscribe<T>(Func<IServiceResolver, IEventSubscriber<T>> factory) where T : struct, IDomainEvent;
        void Handle<T>(Func<IServiceResolver, ICommandHandler<T>> factory) where T : struct, ICommand;
        void Tick(Func<IServiceResolver, IFixedTickable> factory);
        void AddService<T>(T service) where T : class;
        void AddCommandHandler<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;
        void AddFixedTickable(IFixedTickable tickable);
    }

    public interface IServiceResolver
    {
        T Get<T>() where T : class;
        IReadOnlyList<T> GetAll<T>() where T : class;
    }

    public sealed class ModuleRegistrationSummary
    {
        internal ModuleRegistrationSummary(IEnumerable<Type> required, IEnumerable<Type> provided,
            IEnumerable<Type> collections, IEnumerable<Type> commands)
        {
            Required = Sort(required);
            Provided = Sort(provided);
            Collections = Sort(collections);
            Commands = Sort(commands);
        }

        public IReadOnlyList<Type> Required { get; }
        public IReadOnlyList<Type> Provided { get; }
        public IReadOnlyList<Type> Collections { get; }
        public IReadOnlyList<Type> Commands { get; }

        private static IReadOnlyList<Type> Sort(IEnumerable<Type> values) =>
            values.Distinct().OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
    }

    public sealed class ModuleContext : IServiceResolver
    {
        private readonly ServiceContainer services;

        internal ModuleContext(ServiceContainer services, float tickDeltaTime)
        {
            this.services = services;
            TickDeltaTime = tickDeltaTime;
        }

        public float TickDeltaTime { get; }
        public long Tick { get; internal set; }
        public T Get<T>() where T : class => services.Get<T>();
        public IReadOnlyList<T> GetAll<T>() where T : class => services.GetAll<T>();
        public IClock Clock => Get<IClock>();
        public ICommandTransport Transport => Get<ICommandTransport>();
        public IEventStream Events => Get<IEventStream>();
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

    public sealed class ModuleGraph
    {
        private readonly Dictionary<ModuleId, IGameModule> byId;

        private readonly IReadOnlyDictionary<ModuleId, ModuleScope> external;

        public ModuleGraph(IEnumerable<IGameModule> gameModules,
            IReadOnlyDictionary<ModuleId, ModuleScope> external = null)
        {
            if (gameModules == null)
                throw new ArgumentNullException(nameof(gameModules));

            this.external = external;
            byId = new Dictionary<ModuleId, IGameModule>();
            foreach (var module in gameModules)
            {
                if (module == null || !module.Id.IsValid)
                    throw new InvalidOperationException("Every game module must have a valid id.");
                if (external?.ContainsKey(module.Id) == true || !byId.TryAdd(module.Id, module))
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

            foreach (var dependency in (module.Dependencies ?? Array.Empty<ModuleId>()).OrderBy(id => id.Value, StringComparer.Ordinal))
            {
                if (!byId.TryGetValue(dependency, out var dependencyModule))
                {
                    if (external != null && external.TryGetValue(dependency, out var scope) && scope <= module.Scope)
                        continue;

                    throw new InvalidOperationException($"Module {module.Id} depends on missing module {dependency}.");
                }
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
