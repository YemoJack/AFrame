using AFrame.Foundation;
using AFrame.Runtime;
using UnityEngine;

namespace AFrame.Unity
{
    [DisallowMultipleComponent]
    public sealed class AFrameBootstrap : MonoBehaviour
    {
        [SerializeField] private AFrameGameProfile profile;
        [SerializeField] private bool startOnAwake = true;

        private GameModuleHost host;
        private double accumulator;

        public AFrameGameProfile Profile => profile;
        public ModuleContext Context => host == null ? null : host.Context;
        public bool IsRunning => host != null && host.IsRunning;

        public void SetProfile(AFrameGameProfile value)
        {
            if (IsRunning)
                throw new System.InvalidOperationException("Cannot change the profile while AFrame is running.");

            profile = value;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (startOnAwake)
                StartGame();
        }

        private void Update()
        {
            if (!IsRunning || profile == null)
                return;

            accumulator += Time.deltaTime;
            var tickDelta = profile.TickDeltaTime;
            var ticks = 0;
            while (isActiveAndEnabled && IsRunning && accumulator >= tickDelta && ticks < profile.MaxTicksPerFrame)
            {
                accumulator -= tickDelta;
                host.Tick();
                ticks++;
            }

            if (ticks == profile.MaxTicksPerFrame && accumulator > tickDelta)
                accumulator = tickDelta;
        }

        public void StartGame()
        {
            if (IsRunning)
                return;

            if (profile == null)
            {
                Debug.LogError("AFrameBootstrap requires an AFrameGameProfile.", this);
                return;
            }

            host = new GameModuleHost();
            try
            {
                accumulator = 0d;
                host.Start(profile.CreateModules(), new UnityClock(), profile.TickDeltaTime);
            }
            catch
            {
                host.Dispose();
                host = null;
                throw;
            }
        }

        public void StopGame()
        {
            if (host == null)
                return;

            host.Stop();
            host = null;
            accumulator = 0d;
        }

        private void OnDestroy()
        {
            StopGame();
        }
    }

    internal sealed class UnityClock : IClock
    {
        public double Time => UnityEngine.Time.timeAsDouble;
        public double DeltaTime => UnityEngine.Time.deltaTime;
    }
}
