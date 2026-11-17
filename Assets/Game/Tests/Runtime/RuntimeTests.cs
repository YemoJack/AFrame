using System;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;
using AFrame.Testing;
using NUnit.Framework;

namespace AFrame.Tests
{
    public sealed class RuntimeTests
    {
        [Test]
        public void TickDispatchesInSequenceBeforeSimulationAndEvents()
        {
            var order = new List<string>();
            var module = new OrderedModule(order);
            using var game = new TestGame(new[] { module });
            using var view = game.Context.Get<IViewEventStream>().Subscribe<TraceEvent>(message => order.Add("view:" + message.Id));
            game.Submit(new TraceCommand(2));
            game.Submit(new TraceCommand(1));

            Assert.That(order, Is.Empty);
            game.Tick();

            Assert.That(order, Is.EqualTo(new[] { "dispatch:2", "dispatch:1", "simulate", "domain:2", "view:2", "domain:1", "view:1" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidProviderGraphStopsBeforeModulesStart(bool duplicate)
        {
            var started = new List<string>();
            var consumer = new Registration("consumer", builder => builder.Require<IMarker>(), started);
            var providers = new[]
            {
                consumer,
                new Registration("first", builder => builder.Provide<IMarker>(_ => new Marker()), started),
                new Registration("second", builder => builder.Provide<IMarker>(_ => new Marker()), started)
            };
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var game = new TestGame(duplicate ? providers : new[] { consumer });
            });
            Assert.That(started, Is.Empty);
        }

        private interface IMarker { }
        private sealed class Marker : IMarker { }

        private sealed class Registration : IGameModule
        {
            private readonly Action<IModuleBuilder> configure;
            private readonly List<string> started;
            public Registration(string id, Action<IModuleBuilder> configure, List<string> started)
            {
                Id = new ModuleId(id);
                this.configure = configure;
                this.started = started;
            }
            public ModuleId Id { get; }
            public ModuleScope Scope => ModuleScope.Session;
            public IReadOnlyList<ModuleId> Dependencies => Array.Empty<ModuleId>();
            public void Configure(IModuleBuilder builder) => configure(builder);
            public void Start(ModuleContext context) => started.Add(Id.Value);
            public void Stop() { }
        }

        [StableId("test-runtime.trace-command")]
        private readonly struct TraceCommand : ICommand
        {
            public TraceCommand(int id) => Id = id;
            public int Id { get; }
        }

        [StableId("test-runtime.trace-event")]
        private readonly struct TraceEvent : IDomainEvent
        {
            public TraceEvent(int id) => Id = id;
            public int Id { get; }
        }

        private sealed class OrderedModule : IGameModule, ICommandHandler<TraceCommand>, IFixedTickable, IEventSubscriber<TraceEvent>
        {
            private readonly List<string> order;
            private IEventSink events;
            public OrderedModule(List<string> order) => this.order = order;
            public ModuleId Id => new ModuleId("trace");
            public ModuleScope Scope => ModuleScope.Session;
            public IReadOnlyList<ModuleId> Dependencies => Array.Empty<ModuleId>();
            public void Configure(IModuleBuilder builder)
            {
                builder.Handle<TraceCommand>(_ => this);
                builder.Tick(_ => this);
                builder.Subscribe<TraceEvent>(_ => this);
            }
            public void Start(ModuleContext context) => events = context.Get<IEventSink>();
            public void Stop() { }
            public void FixedTick(in TickContext context) => order.Add("simulate");
            public void On(in TraceEvent message) => order.Add("domain:" + message.Id);

            public Result Handle(in CommandEnvelope envelope, in TraceCommand command)
            {
                order.Add("dispatch:" + command.Id);
                events.Raise(new TraceEvent(command.Id));
                return Result.Ok();
            }
        }
    }
}
