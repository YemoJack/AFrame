using System;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;
using AFrame.Testing;
using NUnit.Framework;

namespace AFrame.Game.Character.Tests
{
    public sealed class CharacterTests
    {
        [Test]
        public void MovementChangesStateAndEmitsEventOnNextTick()
        {
            using var game = CreateGame();
            var character = game.Context.Get<ICharacterState>();
            var received = new List<CharacterMovedEvent>();
            using var subscription = game.Context.Events.Subscribe<CharacterMovedEvent>(received.Add);

            game.Submit(new MoveCharacterCommand(new Float3(1, 0, 0)));
            Assert.That(character.Position.x, Is.Zero);
            Assert.That(received, Is.Empty);

            game.Tick();
            Assert.That(game.Context.Tick, Is.EqualTo(1));
            Assert.That(character.Position.x, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Position, Is.EqualTo(character.Position));
        }

        [Test]
        public void InvalidMoveIsRejectedWithoutChangingState()
        {
            using var game = CreateGame();
            var character = game.Context.Get<ICharacterState>();
            var received = new List<CommandRejectedEvent>();
            using var subscription = game.Context.Events.Subscribe<CommandRejectedEvent>(received.Add);
            game.Submit(new MoveCharacterCommand(new Float3(2, 0, 0)));
            game.Tick();

            Assert.That(character.Position.x, Is.Zero);
            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Error.Code, Is.EqualTo("character.invalid_direction"));
            Assert.That(received[0].Command.Tick, Is.EqualTo(1));
        }

        private static TestGame CreateGame() => new TestGame(new IGameModule[] { new Inputs(), new CharacterModule("test.character") });

        private sealed class Inputs : IGameModule, ICharacterDefinitions, IInputSource<CharacterInput>
        {
            public ModuleId Id => new ModuleId("test.inputs");
            public ModuleScope Scope => ModuleScope.Session;
            public IReadOnlyList<ModuleId> Dependencies => Array.Empty<ModuleId>();
            public void Configure(IModuleBuilder builder)
            {
                builder.AddService<ICharacterDefinitions>(this);
                builder.AddService<IInputSource<CharacterInput>>(this);
            }

            public void Start(ModuleContext context) { }
            public void Stop() { }
            public CharacterInput Get(PlayerId player) => default;
            CharacterDefinition ICharacterDefinitions.Get(string id) => new CharacterDefinition { speed = 5f };
        }
    }
}
