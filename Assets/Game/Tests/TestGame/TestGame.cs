using System;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;

namespace AFrame.Testing
{
    public sealed class TestGame : IDisposable
    {
        private readonly ManualClock clock = new ManualClock();
        private readonly GameModuleHost app;
        private readonly GameModuleHost host;

        public TestGame(IEnumerable<IGameModule> modules, IEnumerable<IGameModule> appModules = null,
            float tickDelta = 0.02f)
        {
            if (appModules != null)
            {
                app = GameModuleHost.CreateApplication();
                app.Start(appModules, clock, tickDelta);
            }

            host = app == null ? new GameModuleHost() : new GameModuleHost(app);
            try { host.Start(modules, clock, tickDelta); }
            catch { Dispose(); throw; }
        }

        public ModuleContext Context => host.Context;

        public void Submit<T>(in T command) where T : struct, ICommand =>
            Context.Get<ICommandSink>().Submit(PlayerId.None, in command);

        public void Tick()
        {
            clock.Advance(Context.TickDeltaTime);
            host.Tick();
        }

        public void Dispose()
        {
            try { host.Dispose(); }
            finally { app?.Dispose(); }
        }

        private sealed class ManualClock : IClock
        {
            public double Time { get; private set; }
            public double DeltaTime { get; private set; }

            public void Advance(float seconds)
            {
                DeltaTime = seconds;
                Time += seconds;
            }
        }
    }
}
