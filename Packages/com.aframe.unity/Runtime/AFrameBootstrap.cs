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
            if (startOnAwake)
                StartGame();
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
                host.Start(profile.CreateModules(), new UnityClock());
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
