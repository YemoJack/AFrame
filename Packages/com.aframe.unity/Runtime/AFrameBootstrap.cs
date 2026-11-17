using System;
using System.Collections.Generic;
using System.Linq;
using AFrame.Foundation;
using AFrame.Runtime;
using UnityEngine;
using EntityId = AFrame.Foundation.EntityId;
using UnityEngine.SceneManagement;

namespace AFrame.Unity
{
    [DisallowMultipleComponent]
    public sealed class AFrameBootstrap : MonoBehaviour
    {
        public const string ProfileResource = "AFrameGameProfile";

        private readonly List<SceneScope> scopes = new List<SceneScope>();
        private readonly List<(AFrameView View, ViewContext Context)> directViews =
            new List<(AFrameView, ViewContext)>();
        private GameModuleHost app;
        private GameModuleHost session;
        private double accumulator;
        private AFrameSceneEntry entry;
        private Scene sessionScene;
        private ViewFactory viewFactory;
        private Action afterTick;
        private AFrameGameProfile profile;
        private Exception sceneFault;

        public AFrameGameProfile Profile => profile;
        public ModuleContext Context => session?.Context;
        public HostState SessionState => session?.State ?? HostState.Stopped;
        public bool IsRunning => SessionState == HostState.Running;
        public Exception SessionFault => session?.Fault ?? sceneFault;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var config = Resources.Load<AFrameGameProfile>(ProfileResource);
            if (config == null)
                return;

            var root = new GameObject(nameof(AFrameBootstrap));
            DontDestroyOnLoad(root);
            var bootstrap = root.AddComponent<AFrameBootstrap>();
            try { bootstrap.StartApp(config); }
            catch { Destroy(root); throw; }
        }

        private void StartApp(AFrameGameProfile config)
        {
            profile = config;
            app = GameModuleHost.CreateApplication();
            app.Start(profile.CreateModules(ModuleScope.App)
                .Append(new ProfileModule(config)), new UnityClock(), profile.TickDeltaTime);
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                var roots = scene.GetRootGameObjects();
                var entries = roots.Select(root => root.GetComponent<AFrameSceneEntry>()).Where(value => value != null).ToArray();
                var loadedScopes = roots.Select(root => root.GetComponent<SceneScope>()).Where(value => value != null).ToArray();
                if (entries.Length > 1 || loadedScopes.Length > 1)
                    throw new InvalidOperationException($"Scene {scene.name} has multiple AFrame entries or scopes.");

                if (entries.Length != 0 || mode == LoadSceneMode.Single)
                {
                    EndSession();
                    entry = entries.FirstOrDefault();
                    sessionScene = scene;
                }
                scopes.AddRange(loadedScopes);
                foreach (var scope in loadedScopes)
                    scope.Host = this;
                if (entries.Length != 0)
                    entry.Attach(this);
                else if (session == null && entry == null && loadedScopes.Length != 0)
                {
                    sessionScene = scene;
                    BeginSession();
                }
                else if (Context != null)
                    foreach (var scope in loadedScopes)
                        scope.Bind(Context);
            }
            catch (Exception error)
            {
                ReportSceneFault(error);
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            scopes.RemoveAll(scope => scope == null || scope.gameObject.scene == scene);
            if (sessionScene == scene)
            {
                entry = null;
                sessionScene = default;
                EndSession();
            }
        }

        public T GetAppService<T>() where T : class => app.Context.Get<T>();

        public void BeginSession(IEnumerable<IGameModule> modules = null, Action<ModuleContext> restore = null)
        {
            EndSession();
            sceneFault = null;
            session = new GameModuleHost(app);
            try
            {
                session.Start(modules ?? profile.CreateModules(ModuleScope.Session),
                    tickDeltaTime: profile.TickDeltaTime);
                restore?.Invoke(session.Context);
                viewFactory = new ViewFactory(this, profile);
                foreach (var scope in scopes.Where(scope => scope != null))
                    scope.Bind(Context);
            }
            catch
            {
                EndSession();
                throw;
            }
        }

        public void PauseSession() => session?.Pause();
        public void ResumeSession() => session?.Resume();

        public void AtTickBoundary(Action action)
        {
            if (Context == null)
                throw new InvalidOperationException("AFrame has no session.");
            afterTick += action;
        }

        public void BindView(AFrameView view, EntityId entity = default)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));
            if (Context == null)
                throw new InvalidOperationException("Start a session before binding views.");
            var viewContext = new ViewContext(Context, entity);
            try { view.Bind(viewContext); }
            catch { viewContext.Dispose(); throw; }
            directViews.Add((view, viewContext));
        }

        public void UnbindView(AFrameView view)
        {
            for (var index = directViews.Count - 1; index >= 0; index--)
                if (directViews[index].View == view)
                    ReleaseView(index);
        }

        private void ReleaseView(int index)
        {
            var binding = directViews[index];
            directViews.RemoveAt(index);
            try { if (binding.View != null) binding.View.Unbind(); }
            catch (Exception error) { Debug.LogException(error, this); }
            finally { binding.Context.Dispose(); }
        }

        private void Update()
        {
            if (!IsRunning)
                return;
            try
            {
                entry?.SampleInput();
                var dt = profile.TickDeltaTime;
                accumulator += Time.deltaTime;
                var count = 0;
                while (isActiveAndEnabled && IsRunning && accumulator >= dt && count < profile.MaxTicksPerFrame)
                {
                    accumulator -= dt;
                    session.Tick();
                    foreach (var scope in scopes)
                        if (scope != null)
                            scope.TickCompleted();
                    foreach (var binding in directViews.ToArray())
                        if (binding.View != null && binding.View.isActiveAndEnabled)
                            binding.View.OnTickCompleted();
                    count++;
                    var callback = afterTick;
                    afterTick = null;
                    callback?.Invoke();
                }

                if (count == profile.MaxTicksPerFrame && accumulator >= dt)
                {
                    accumulator = 0;
                    Debug.LogWarning("AFrame discarded accumulated simulation time.", this);
                }
            }
            catch (Exception error)
            {
                if (SessionState == HostState.Faulted)
                {
                    Debug.LogException(error, this);
                    ReleaseSessionViews();
                }
                else
                    ReportSceneFault(error);
            }
        }

        private void LateUpdate()
        {
            if (Context == null)
                return;
            try
            {
                var alpha = Mathf.Clamp01((float)(accumulator / profile.TickDeltaTime));
                foreach (var scope in scopes)
                    if (scope != null)
                        scope.Render(alpha);
                foreach (var binding in directViews)
                    if (binding.View != null && binding.View.isActiveAndEnabled)
                        binding.View.OnRender(alpha);
            }
            catch (Exception error) { ReportSceneFault(error); }
        }

        private void ReportSceneFault(Exception error)
        {
            sceneFault = error;
            Debug.LogException(error, this);
            EndSession();
        }

        private void ReleaseSessionViews()
        {
            afterTick = null;
            accumulator = 0;
            viewFactory?.Dispose();
            viewFactory = null;
            foreach (var scope in scopes)
                if (scope != null)
                    scope.Release();
            while (directViews.Count > 0)
                ReleaseView(directViews.Count - 1);
        }

        public void EndSession()
        {
            ReleaseSessionViews();
            var old = session;
            session = null;
            try { old?.Stop(); }
            catch (Exception error) { Debug.LogException(error, this); }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            EndSession();
            try { app?.Stop(); }
            catch (Exception error) { Debug.LogException(error, this); }
        }

        private sealed class ProfileModule : IGameModule
        {
            private readonly AFrameGameProfile profile;
            public ProfileModule(AFrameGameProfile profile) => this.profile = profile;
            public ModuleId Id => new ModuleId("aframe.profile");
            public ModuleScope Scope => ModuleScope.App;
            public IReadOnlyList<ModuleId> Dependencies => Array.Empty<ModuleId>();
            public void Configure(IModuleBuilder builder)
            {
                builder.AddService(profile);
                if (profile.Definitions != null)
                    builder.AddService(profile.Definitions);
            }
            public void Start(ModuleContext context) { }
            public void Stop() { }
        }
    }

    internal sealed class UnityClock : IClock
    {
        public double Time => UnityEngine.Time.timeAsDouble;
        public double DeltaTime => UnityEngine.Time.deltaTime;
    }
}
