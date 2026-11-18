using System;
using System.Collections.Generic;
using System.Linq;
using AFrame.Foundation;

namespace AFrame.Runtime
{
    public enum HostState { Stopped, Starting, Running, Paused, Faulted, Stopping }

    public sealed class GameModuleHost : IDisposable
    {
        private readonly GameModuleHost app;
        private readonly ModuleScope scope;
        private readonly List<IGameModule> started = new List<IGameModule>();
        private readonly List<(ModuleId Module, IFixedTickable Value)> tickables =
            new List<(ModuleId, IFixedTickable)>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<ModuleId, ModuleScope> activeModules = new Dictionary<ModuleId, ModuleScope>();
        private readonly List<GameModuleHost> sessions = new List<GameModuleHost>();
        private ServiceContainer services;
        private ICommandQueue queue;
        private CommandDispatcher dispatcher;
        private EventStream events;
        private bool ticking;
        private IReadOnlyList<ITickInput> inputs;

        public GameModuleHost() : this(ModuleScope.Session, null) { }

        public GameModuleHost(GameModuleHost app) : this(ModuleScope.Session,
            app ?? throw new ArgumentNullException(nameof(app))) { }

        public static GameModuleHost CreateApplication() => new GameModuleHost(ModuleScope.App, null);

        private GameModuleHost(ModuleScope scope, GameModuleHost app)
        {
            this.scope = scope;
            this.app = app;
        }

        public HostState State { get; private set; }
        public ModuleContext Context { get; private set; }
        public Exception Fault { get; private set; }
        public bool IsRunning => State == HostState.Running;

        public static ModuleRegistrationSummary DescribeRegistration(IGameModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            using var services = new ServiceContainer(null);
            var registration = new ModuleRegistration(module, services);
            module.Configure(registration);
            return new ModuleRegistrationSummary(registration.Required, services.SingleTypes, services.CollectionTypes,
                registration.CommandTypes);
        }

        public void Start(IEnumerable<IGameModule> gameModules, IClock clock = null, float tickDeltaTime = 0.02f)
        {
            if (State != HostState.Stopped && State != HostState.Faulted)
                throw new InvalidOperationException($"Cannot start a host in state {State}.");
            if (float.IsNaN(tickDeltaTime) || float.IsInfinity(tickDeltaTime) || tickDeltaTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(tickDeltaTime));
            if (app != null && (!app.IsRunning || app.scope != ModuleScope.App))
                throw new InvalidOperationException("A session requires a running application host.");

            Fault = null;
            State = HostState.Starting;
            try
            {
                var modules = new ModuleGraph(gameModules, app?.activeModules).GetOrderedModules();
                if (modules.Any(module => module.Scope != scope))
                    throw new InvalidOperationException("App and Session modules must run in separate hosts.");

                services = new ServiceContainer(app?.services);
                dispatcher = new CommandDispatcher();
                var transport = new LocalCommandTransport();
                queue = transport;
                events = new EventStream();
                if (app == null)
                    services.Add<IClock>(clock ?? new SystemClock());
                if (scope == ModuleScope.Session)
                {
                    services.Add<ICommandDispatcher>(dispatcher);
                    services.Add<ICommandSink>(transport);
                    services.Add<ICommandTransport>(transport);
                    services.Add<IEventStream>(events);
                    services.Add<IEventPublisher>(events);
                    services.Add<IEventSink>(events);
                    services.Add<IViewEventStream>(events);
                }
                Context = new ModuleContext(services, tickDeltaTime);

                var registrations = modules.ToDictionary(module => module.Id,
                    module => new ModuleRegistration(module, services));
                foreach (var module in modules)
                    module.Configure(registrations[module.Id]);

                var ordered = new List<ModuleRegistration>();
                var visiting = new HashSet<ModuleId>();
                var visited = new HashSet<ModuleId>();
                void Visit(ModuleRegistration registration)
                {
                    var id = registration.Module.Id;
                    if (visited.Contains(id))
                        return;
                    if (!visiting.Add(id))
                        throw new InvalidOperationException($"Service dependency cycle at {id}.");

                    var dependencies = registration.Module.Dependencies ?? Array.Empty<ModuleId>();
                    foreach (var dependency in dependencies.Concat(registration.Required.SelectMany(services.Owners))
                                 .Where(dependency => !dependency.Equals(id)).Distinct().OrderBy(item => item.Value, StringComparer.Ordinal))
                    {
                        if (registrations.TryGetValue(dependency, out var other))
                            Visit(other);
                    }
                    visiting.Remove(id);
                    visited.Add(id);
                    ordered.Add(registration);
                }
                foreach (var module in modules.OrderBy(module => module.Id.Value, StringComparer.Ordinal))
                    Visit(registrations[module.Id]);

                services.Build();
                foreach (var registration in ordered)
                {
                    registration.Activate(dispatcher, events, tickables, subscriptions);
                    activeModules.Add(registration.Module.Id, registration.Module.Scope);
                }
                foreach (var registration in ordered)
                {
                    // Stop must also tolerate a partially completed Start.
                    started.Add(registration.Module);
                    registration.Module.Start(Context);
                }
                inputs = services.GetAll<ITickInput>();
                State = HostState.Running;
                app?.sessions.Add(this);
            }
            catch (Exception error)
            {
                throw Fail(error);
            }
        }

        public void Tick()
        {
            if (!IsRunning || scope != ModuleScope.Session)
                return;
            if (ticking)
                throw new InvalidOperationException("AFrame Tick cannot be re-entered.");

            var phase = "Receive";
            var subject = "";
            try
            {
                ticking = true;
                Context.Tick++;
                foreach (var input in inputs)
                    input.Freeze();
                queue.BeginTick(Context.Tick);
                phase = "Dispatch";
                foreach (var command in queue.Drain(Context.Tick))
                {
                    subject = $"{command.Payload.GetType().FullName} (sequence {command.Sequence})";
                    var result = ((ICommandExecutor)dispatcher).Dispatch(in command);
                    if (result.Failed)
                        events.Publish(new CommandRejectedEvent(command, result.Error));
                }
                phase = "Simulate";
                var tick = new TickContext(Context.Tick, Context.TickDeltaTime);
                foreach (var tickable in tickables)
                {
                    subject = tickable.Module.ToString();
                    tickable.Value.FixedTick(in tick);
                }
                phase = "Flush";
                subject = "";
                events.Flush();
            }
            catch (Exception error)
            {
                throw Fail(new InvalidOperationException(
                    $"AFrame session fault at Tick {Context.Tick}, phase {phase} {subject}.", error));
            }
            finally
            {
                ticking = false;
            }
        }

        public void Pause()
        {
            if (IsRunning)
                State = HostState.Paused;
        }

        public void Resume()
        {
            if (State == HostState.Paused)
                State = HostState.Running;
        }

        public void Stop()
        {
            if (ticking || State == HostState.Starting)
                throw new InvalidOperationException("Stop the session outside Tick and module startup.");
            if (State == HostState.Stopped || State == HostState.Stopping)
                return;

            State = HostState.Stopping;
            var errors = Clear();
            State = HostState.Stopped;
            if (errors.Count != 0)
                throw new AggregateException("AFrame shutdown failed.", errors);
        }

        private Exception Fail(Exception error)
        {
            State = HostState.Stopping;
            var errors = Clear();
            errors.Insert(0, error);
            Fault = errors.Count == 1 ? error : new AggregateException(errors);
            State = HostState.Faulted;
            return Fault;
        }

        private List<Exception> Clear()
        {
            var errors = new List<Exception>();
            void Cleanup(Action action)
            {
                try { action(); }
                catch (Exception error) { errors.Add(error); }
            }

            foreach (var session in sessions.ToArray())
                Cleanup(session.Stop);
            for (var index = started.Count - 1; index >= 0; index--)
                Cleanup(started[index].Stop);
            foreach (var subscription in subscriptions)
                Cleanup(subscription.Dispose);
            events?.Close();
            queue?.Close();
            if (services != null)
                Cleanup(services.Dispose);
            app?.sessions.Remove(this);
            started.Clear();
            tickables.Clear();
            subscriptions.Clear();
            activeModules.Clear();
            inputs = null;
            Context = null;
            services = null;
            queue = null;
            dispatcher = null;
            events = null;
            return errors;
        }

        public void Dispose() => Stop();
    }

    internal sealed class ModuleRegistration : IModuleBuilder
    {
        private readonly ServiceContainer services;
        private readonly List<Action<CommandDispatcher>> handlers = new List<Action<CommandDispatcher>>();
        private readonly List<Action<EventStream, List<IDisposable>>> subscribers =
            new List<Action<EventStream, List<IDisposable>>>();
        private readonly List<Func<IFixedTickable>> ticks = new List<Func<IFixedTickable>>();

        public ModuleRegistration(IGameModule module, ServiceContainer services)
        {
            Module = module;
            this.services = services;
        }

        public IGameModule Module { get; }
        public HashSet<Type> Required { get; } = new HashSet<Type>();
        public List<Type> CommandTypes { get; } = new List<Type>();
        private IServiceResolver Resolver => new DeclaredResolver(services, this);
        public void Require<T>() where T : class => Required.Add(typeof(T));
        public void Provide<T>(Func<IServiceResolver, T> factory) where T : class =>
            services.Register(Module.Id, () => factory(Resolver), false);
        public void ProvideMany<T>(Func<IServiceResolver, T> factory) where T : class =>
            services.Register(Module.Id, () => factory(Resolver), true);
        public void AddService<T>(T service) where T : class => Provide<T>(_ => service);
        public void Handle<T>(Func<IServiceResolver, ICommandHandler<T>> factory) where T : struct, ICommand
        {
            CommandTypes.Add(typeof(T));
            handlers.Add(dispatcher => dispatcher.Register(factory(Resolver), Module.Id));
        }
        public void AddCommandHandler<T>(ICommandHandler<T> handler) where T : struct, ICommand =>
            Handle<T>(_ => handler);
        public void Subscribe<T>(Func<IServiceResolver, IEventSubscriber<T>> factory) where T : struct, IDomainEvent =>
            subscribers.Add((stream, registrations) =>
            {
                var subscriber = factory(Resolver) ?? throw new InvalidOperationException($"{Module.Id} returned a null subscriber.");
                registrations.Add(stream.Subscribe<T>(value =>
                {
                    try { subscriber.On(in value); }
                    catch (Exception error)
                    {
                        throw new InvalidOperationException($"Event subscriber in module {Module.Id} failed.", error);
                    }
                }));
            });
        public void Tick(Func<IServiceResolver, IFixedTickable> factory) => ticks.Add(() => factory(Resolver));
        public void AddFixedTickable(IFixedTickable tickable) => Tick(_ => tickable);

        public void Activate(CommandDispatcher dispatcher, EventStream events,
            List<(ModuleId, IFixedTickable)> tickables, List<IDisposable> subscriptions)
        {
            if (Module.Scope == ModuleScope.App && (handlers.Count + subscribers.Count + ticks.Count != 0))
                throw new InvalidOperationException($"App module {Module.Id} cannot register session simulation.");
            foreach (var handler in handlers)
                handler(dispatcher);
            foreach (var subscriber in subscribers)
                subscriber(events, subscriptions);
            foreach (var tick in ticks)
                tickables.Add((Module.Id, tick() ?? throw new InvalidOperationException($"{Module.Id} returned a null tickable.")));
        }

        private sealed class DeclaredResolver : IServiceResolver
        {
            private readonly ServiceContainer services;
            private readonly ModuleRegistration registration;

            public DeclaredResolver(ServiceContainer services, ModuleRegistration registration)
            {
                this.services = services;
                this.registration = registration;
            }

            private void Check<T>()
            {
                if (!registration.Required.Contains(typeof(T)) && !services.IsOwnedBy(typeof(T), registration.Module.Id))
                    throw new InvalidOperationException($"{registration.Module.Id} must declare Require<{typeof(T).Name}>.");
            }

            public T Get<T>() where T : class { Check<T>(); return services.Get<T>(); }
            public IReadOnlyList<T> GetAll<T>() where T : class { Check<T>(); return services.GetAll<T>(); }
        }
    }

    internal sealed class ServiceContainer : IServiceResolver, IDisposable
    {
        private sealed class Entry
        {
            public ModuleId Owner;
            public Func<object> Create;
            public object Value;
            public bool Creating;
        }

        private readonly ServiceContainer parent;
        private readonly Dictionary<Type, List<Entry>> entries = new Dictionary<Type, List<Entry>>();
        private readonly HashSet<Type> collections = new HashSet<Type>();
        private readonly List<object> created = new List<object>();
        private bool disposed;

        public ServiceContainer(ServiceContainer parent) => this.parent = parent;
        public IEnumerable<Type> SingleTypes => entries.Keys.Where(type => !collections.Contains(type));
        public IEnumerable<Type> CollectionTypes => collections;
        public void Add<T>(T value) where T : class => Register(default, () => value, false);
        private bool Contains(Type type) => entries.ContainsKey(type) || parent?.Contains(type) == true;

        public void Register<T>(ModuleId owner, Func<T> factory, bool many) where T : class
        {
            var type = typeof(T);
            if (parent?.Contains(type) == true)
                throw new InvalidOperationException($"{owner} cannot shadow App service {type.FullName}.");
            if (!entries.TryGetValue(type, out var list))
            {
                list = new List<Entry>();
                entries.Add(type, list);
                if (many)
                    collections.Add(type);
            }
            else if (!many || !collections.Contains(type))
                throw new InvalidOperationException($"Duplicate service {type.FullName} in module {owner}.");
            list.Add(new Entry { Owner = owner, Create = factory });
        }

        public IEnumerable<ModuleId> Owners(Type type)
        {
            if (entries.TryGetValue(type, out var list))
                return list.Select(entry => entry.Owner).Where(owner => owner.IsValid);
            if (parent != null)
                return parent.Owners(type);
            throw new InvalidOperationException($"Missing service {type.FullName}.");
        }

        public bool IsOwnedBy(Type type, ModuleId owner) =>
            entries.TryGetValue(type, out var list) && list.All(entry => entry.Owner.Equals(owner));

        public T Get<T>() where T : class
        {
            EnsureAlive();
            if (entries.TryGetValue(typeof(T), out var list))
            {
                if (collections.Contains(typeof(T)))
                    throw new InvalidOperationException($"Use GetAll for {typeof(T).FullName}.");
                return (T)Resolve(list[0]);
            }
            if (parent != null)
                return parent.Get<T>();
            throw new InvalidOperationException($"Missing service {typeof(T).FullName}.");
        }

        public IReadOnlyList<T> GetAll<T>() where T : class
        {
            EnsureAlive();
            if (!entries.TryGetValue(typeof(T), out var list))
                return parent?.GetAll<T>() ?? Array.Empty<T>();
            if (!collections.Contains(typeof(T)))
                throw new InvalidOperationException($"Use Get for {typeof(T).FullName}.");
            return list.Select(entry => (T)Resolve(entry)).ToArray();
        }

        public void Build()
        {
            foreach (var list in entries.Values)
                foreach (var entry in list)
                    Resolve(entry);
        }

        private void EnsureAlive()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ServiceContainer));
        }

        private object Resolve(Entry entry)
        {
            if (entry.Value != null)
                return entry.Value;
            if (entry.Creating)
                throw new InvalidOperationException($"Service factory cycle at {entry.Owner}.");

            entry.Creating = true;
            entry.Value = entry.Create() ?? throw new InvalidOperationException($"Null service from {entry.Owner}.");
            if (!created.Any(value => ReferenceEquals(value, entry.Value)))
                created.Add(entry.Value);
            entry.Creating = false;
            return entry.Value;
        }

        public void Dispose()
        {
            disposed = true;
            var errors = new List<Exception>();
            for (var index = created.Count - 1; index >= 0; index--)
            {
                if (!(created[index] is IDisposable resource))
                    continue;
                try { resource.Dispose(); }
                catch (Exception error) { errors.Add(error); }
            }
            created.Clear();
            entries.Clear();
            if (errors.Count > 0)
                throw new AggregateException(errors);
        }
    }
}
