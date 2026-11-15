using System;
using System.Collections.Generic;

namespace AFrame.Runtime
{
    public interface ICommand { }

    public interface ICommandHandler<in TCommand> where TCommand : ICommand
    {
        void Handle(TCommand command);
    }

    public interface ICommandDispatcher
    {
        void Register<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand;
        void Dispatch(ICommand command);
    }

    public sealed class CommandDispatcher : ICommandDispatcher
    {
        private readonly Dictionary<Type, List<Action<ICommand>>> handlers =
            new Dictionary<Type, List<Action<ICommand>>>();

        public void Register<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var commandType = typeof(TCommand);
            if (!handlers.TryGetValue(commandType, out var commandHandlers))
            {
                commandHandlers = new List<Action<ICommand>>();
                handlers.Add(commandType, commandHandlers);
            }

            commandHandlers.Add(command => handler.Handle((TCommand)command));
        }

        public void Dispatch(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (!handlers.TryGetValue(command.GetType(), out var commandHandlers))
                throw new InvalidOperationException(
                    $"No command handler is registered for {command.GetType().FullName}.");

            foreach (var handler in commandHandlers.ToArray())
                handler(command);
        }
    }

    public interface ICommandTransport
    {
        void Send(ICommand command);
    }

    public sealed class LocalCommandTransport : ICommandTransport
    {
        private readonly ICommandDispatcher dispatcher;

        public LocalCommandTransport(ICommandDispatcher dispatcher)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void Send(ICommand command)
        {
            dispatcher.Dispatch(command);
        }
    }
}
