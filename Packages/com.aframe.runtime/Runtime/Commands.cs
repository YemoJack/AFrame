using System;
using System.Collections.Generic;
using AFrame.Foundation;

namespace AFrame.Runtime
{
    public interface ICommand { }

    public readonly struct CommandEnvelope
    {
        public CommandEnvelope(PlayerId issuer, long tick, long sequence, ICommand payload)
        {
            Issuer = issuer;
            Tick = tick;
            Sequence = sequence;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public PlayerId Issuer { get; }
        public long Tick { get; }
        public long Sequence { get; }
        public ICommand Payload { get; }
    }

    public interface ICommandHandler<TCommand> where TCommand : struct, ICommand
    {
        Result Handle(in CommandEnvelope envelope, in TCommand command);
    }

    public interface ICommandDispatcher
    {
        void Register<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;
    }

    internal interface ICommandExecutor
    {
        Result Dispatch(in CommandEnvelope envelope);
    }

    public sealed class CommandDispatcher : ICommandDispatcher, ICommandExecutor
    {
        private readonly Dictionary<Type, Func<CommandEnvelope, Result>> handlers =
            new Dictionary<Type, Func<CommandEnvelope, Result>>();

        public void Register<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var commandType = typeof(TCommand);
            if (handlers.ContainsKey(commandType))
                throw new InvalidOperationException(
                    $"A command handler is already registered for {commandType.FullName}.");

            handlers.Add(commandType, envelope =>
            {
                if (!(envelope.Payload is TCommand command))
                    return Result.Fail("command.type_mismatch", commandType.FullName);

                return handler.Handle(in envelope, in command);
            });
        }

        Result ICommandExecutor.Dispatch(in CommandEnvelope envelope)
        {
            var commandType = envelope.Payload.GetType();
            if (!handlers.TryGetValue(commandType, out var handler))
                return Result.Fail("command.handler_missing", commandType.FullName);

            return handler(envelope);
        }
    }

    public interface ICommandTransport
    {
        void Send(ICommand command);
        void Send(PlayerId issuer, ICommand command);
    }

    public interface ICommandSink
    {
        void Submit<TCommand>(PlayerId issuer, in TCommand command)
            where TCommand : struct, ICommand;
    }

    internal interface ICommandQueue
    {
        void BeginTick(long tick);
        IReadOnlyList<CommandEnvelope> Drain(long tick);
    }

    public sealed class LocalCommandTransport : ICommandTransport, ICommandSink, ICommandQueue
    {
        private readonly List<CommandEnvelope> pending = new List<CommandEnvelope>();
        private long currentTick;
        private long sequence;

        public void Send(ICommand command) => Send(PlayerId.None, command);

        public void Send(PlayerId issuer, ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            pending.Add(new CommandEnvelope(issuer, currentTick + 1, sequence++, command));
        }

        public void Submit<TCommand>(PlayerId issuer, in TCommand command)
            where TCommand : struct, ICommand
        {
            Send(issuer, command);
        }

        void ICommandQueue.BeginTick(long tick) => currentTick = tick;

        IReadOnlyList<CommandEnvelope> ICommandQueue.Drain(long tick)
        {
            var result = new List<CommandEnvelope>();
            for (var index = pending.Count - 1; index >= 0; index--)
            {
                if (pending[index].Tick != tick)
                    continue;

                result.Add(pending[index]);
                pending.RemoveAt(index);
            }

            result.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            return result;
        }
    }
}
