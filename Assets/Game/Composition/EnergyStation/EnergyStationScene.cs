using System;
using System.IO;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Game.Character;
using AFrame.Game.Exit;
using AFrame.Game.Inventory;
using AFrame.Game.Pickup;
using AFrame.Runtime;
using AFrame.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AFrame.Game.EnergyStation
{
    public sealed class EnergyStationScene : AFrameSceneEntry, IAFrameSmokeScenario
    {
        [SerializeField] private Material stationMaterial;
        [SerializeField, Min(0.02f)] private float timeLimitSeconds = 45f;
        [SerializeField] private Vector3[] coreLocations =
        {
            new Vector3(-4.2f, 0f, -2.2f),
            new Vector3(3.8f, 0f, -2.5f),
            new Vector3(-3.3f, 0f, 2.6f)
        };

        private readonly Float3 movementBounds = new Float3(5.65f, 0, 4.4f);
        private StationSave saves;
        private string status;
        private Exception smokeFailure;
        public bool SmokeComplete { get; private set; }
        private CharacterView characterView;
        private Transform runRoot;
        private AFrameBootstrap bootstrap;
        private IInventoryQuery inventory;
        private IStationExitQuery exit;
        private ICommandSink commands;
        private CharacterInputAdapter input;
        private GameObject[] coreObjects;
        private Transform[] crystals;
        private GameObject gate;
        private IDisposable coreSubscription;
        private IDisposable clearedSubscription;
        private IDisposable timedOutSubscription;
        private bool exitRequested;
        private GUIStyle titleStyle;
        private GUIStyle statusStyle;

        public override IReadOnlyList<string> Features => new[] { "character", "inventory", "pickup", "exit" };
        public override GameDimension? DimensionOverride => GameDimension.ThreeD;

        public override void Enter(AFrameBootstrap host)
        {
            if (coreLocations == null || coreLocations.Length == 0)
                throw new InvalidOperationException("The Energy Station needs at least one core location.");
            bootstrap = host;
            saves = new StationSave(Path.Combine(Application.persistentDataPath, "AFrame", "energy-station.json"),
                movementBounds, coreLocations.Length, timeLimitSeconds);
        }

        public override void SampleInput() => input?.Sample();

        private void Update()
        {
            if (bootstrap == null)
                return;

            var keyboard = Keyboard.current;
            if (inventory == null || bootstrap.Context == null)
            {
                if (keyboard != null && keyboard.lKey.wasPressedThisFrame && saves.Exists)
                    Load();
                else if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                    BeginRun();
                return;
            }

            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
                Save();
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                BeginRun();
                return;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !exit.IsCleared && !exit.HasTimedOut)
            {
                if (bootstrap.SessionState == HostState.Paused)
                    bootstrap.ResumeSession();
                else
                    bootstrap.PauseSession();
            }
            if (!bootstrap.IsRunning)
                return;
            foreach (var crystal in crystals)
                crystal.Rotate(Vector3.up, 55f * Time.deltaTime, Space.World);
        }

        private void BeginRun(Action<ModuleContext> restore = null)
        {
            EndRun();
            status = null;
            exitRequested = false;
            runRoot = new GameObject("Station Run").transform;
            runRoot.SetParent(transform, false);
            try
            {
                CreateRoom();
                var player = CreatePlayer();
                bootstrap.BeginSession(new IGameModule[]
                {
                    new CharacterInputModule(input),
                    new CharacterModule("character.default", movementBounds),
                    new InventoryModule(coreLocations.Length),
                    new PickupModule(),
                    new ExitModule(timeLimitSeconds)
                }, restore);
                bootstrap.BindView(characterView);
                inventory = bootstrap.Context.Get<IInventoryQuery>();
                exit = bootstrap.Context.Get<IStationExitQuery>();
                commands = bootstrap.Context.Get<ICommandSink>();
                CreateCores(player);
                CreateExit(player);
                CreateCameraAndLight();

                var events = bootstrap.Context.Get<IViewEventStream>();
                coreSubscription = events.Subscribe<EnergyCoreStored>(OnCoreStored);
                clearedSubscription = events.Subscribe<StationCleared>(_ => FinishRun());
                timedOutSubscription = events.Subscribe<StationTimedOut>(_ => FinishRun());

                for (var id = 0; id < coreObjects.Length; id++)
                    coreObjects[id].SetActive(!inventory.Contains(id));
                gate.SetActive(!exit.IsUnlocked);
                if (exit.IsCleared || exit.HasTimedOut)
                    FinishRun();
            }
            catch (Exception error)
            {
                EndRun();
                status = error.Message;
                Debug.LogException(error, this);
            }
        }

        private void Load()
        {
            try { BeginRun(saves.Read()); }
            catch (Exception error) { status = $"读取失败。{error.Message}"; }
        }

        private void Save()
        {
            status = "正在保存";
            // With the simulation paused, the last completed Tick is already a stable boundary.
            void Write()
            {
                try { saves.Save(bootstrap.Context); status = "已保存"; }
                catch (Exception error) { status = $"保存失败。{error.Message}"; }
            }
            if (bootstrap.IsRunning)
                bootstrap.AtTickBoundary(Write);
            else
                Write();
        }

        public void BeginSmoke()
        {
            BeginRun();
            if (bootstrap?.Context == null)
                throw new InvalidOperationException("The station did not start.");
            commands.Submit(PlayerId.None, new SetCharacterMovement(new Float3(1, 0, 0)));
            for (var id = 0; id < coreLocations.Length; id++)
                commands.Submit(PlayerId.None, new CollectEnergyCore(id));
            bootstrap.AtTickBoundary(() => bootstrap.AtTickBoundary(SmokeSaveAndRestart));
        }

        private void SmokeSaveAndRestart()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var source = bootstrap.Context;
                var before = source.Get<ICharacterState>().Position;
                var remaining = exit.SecondsRemaining;
                var appDefinitions = bootstrap.GetAppService<ICharacterDefinitions>();
                var expectedX = Math.Min(movementBounds.x,
                    appDefinitions.Get("character.default").speed * source.TickDeltaTime * source.Tick);
                if (inventory.Collected != coreLocations.Length || Math.Abs(before.x - expectedX) > 0.0001f || !exit.IsUnlocked)
                    throw new InvalidOperationException("Definition-driven movement, pickup or exit unlock did not advance.");

                var slot = new StationSave(path, movementBounds, coreLocations.Length, timeLimitSeconds);
                slot.Save(source);
                BeginRun(slot.Read());
                if (bootstrap.Context == null || ReferenceEquals(source, bootstrap.Context)
                    || bootstrap.GetAppService<ICharacterDefinitions>() != appDefinitions
                    || !bootstrap.Context.Get<ICharacterState>().Position.Equals(before)
                    || inventory.Collected != coreLocations.Length || exit.SecondsRemaining != remaining)
                    throw new InvalidOperationException("A new session did not restore the saved run.");

                bootstrap.PauseSession();
                commands.Submit(PlayerId.None, new ReachStationExit());
                if (exit.IsCleared || bootstrap.SessionState != HostState.Paused)
                    throw new InvalidOperationException("Pause did not keep the command queued.");
                bootstrap.ResumeSession();
                bootstrap.AtTickBoundary(() => SmokeComplete = true);
            }
            catch (Exception error)
            {
                smokeFailure = error;
                SmokeComplete = true;
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        public void VerifySmoke()
        {
            if (smokeFailure != null)
                throw smokeFailure;
            if (!exit.IsCleared || bootstrap.SessionState != HostState.Paused)
                throw new InvalidOperationException("The restored run did not clear after resuming.");
        }

        private Collider CreatePlayer()
        {
            var player = new GameObject("Player");
            player.transform.SetParent(runRoot, false);
            var collider = player.AddComponent<CapsuleCollider>();
            collider.radius = 0.37f;
            collider.height = 1.4f;
            collider.center = Vector3.up * 0.7f;
            var body = player.AddComponent<Rigidbody>();
            body.isKinematic = true;
            characterView = player.AddComponent<CharacterView>();
            input = player.AddComponent<CharacterInputAdapter>();

            Shape("Suit", PrimitiveType.Capsule, Vector3.up * 0.7f,
                new Vector3(0.74f, 0.7f, 0.74f), new Color(0.34f, 0.87f, 1f), player.transform);
            return collider;
        }

        private void CreateCores(Collider player)
        {
            coreObjects = new GameObject[coreLocations.Length];
            crystals = new Transform[coreLocations.Length];
            for (var index = 0; index < coreLocations.Length; index++)
            {
                var coreId = index;
                var location = coreLocations[index];
                var root = new GameObject($"Energy Core {index + 1}");
                root.transform.SetParent(runRoot, false);
                root.transform.localPosition = location;
                coreObjects[index] = root;

                Shape("Pad", PrimitiveType.Cylinder, Vector3.up * 0.05f,
                    new Vector3(1.34f, 0.05f, 1.34f), new Color(0.37f, 0.20f, 0.07f), root.transform);
                var crystal = Shape("Core", PrimitiveType.Cube, Vector3.up * 0.54f,
                    Vector3.one * 0.56f, new Color(1f, 0.67f, 0.14f), root.transform);
                crystal.transform.localRotation = Quaternion.Euler(28f, 45f, 25f);
                crystals[index] = crystal.transform;

                var trigger = root.AddComponent<SphereCollider>();
                trigger.radius = 0.7f;
                trigger.isTrigger = true;
                var requested = false;
                root.AddComponent<StationTrigger>().Bind(player, () =>
                {
                    if (!bootstrap.IsRunning || requested || inventory.Contains(coreId))
                        return;
                    requested = true;
                    commands.Submit(PlayerId.None, new CollectEnergyCore(coreId));
                });
            }
        }

        private void CreateExit(Collider player)
        {
            var exitPosition = new Vector3(3.8f, 0f, 3.55f);
            Shape("Exit Pad", PrimitiveType.Cube, exitPosition + Vector3.up * 0.02f,
                new Vector3(2.2f, 0.04f, 2f), new Color(0.08f, 0.48f, 0.34f));
            gate = Shape("Exit Lock", PrimitiveType.Cube, exitPosition + new Vector3(0f, 0.42f, -0.66f),
                new Vector3(2.2f, 0.82f, 0.16f), new Color(0.93f, 0.24f, 0.12f));

            var zone = new GameObject("Exit Trigger");
            zone.transform.SetParent(runRoot, false);
            zone.transform.localPosition = exitPosition;
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.center = Vector3.up * 0.6f;
            trigger.size = new Vector3(1.6f, 1.2f, 1.2f);
            trigger.isTrigger = true;
            zone.AddComponent<StationTrigger>().Bind(player, () =>
            {
                if (!bootstrap.IsRunning || exitRequested || !exit.IsUnlocked)
                    return;
                exitRequested = true;
                commands.Submit(PlayerId.None, new ReachStationExit());
            });
        }

        private void OnCoreStored(EnergyCoreStored message)
        {
            coreObjects[message.CoreId].SetActive(false);
            if (exit.IsUnlocked)
                gate.SetActive(false);
        }

        private void CreateRoom()
        {
            var dark = new Color(0.12f, 0.18f, 0.24f);
            var edge = new Color(0.22f, 0.34f, 0.43f);
            Shape("Floor", PrimitiveType.Cube, new Vector3(0f, -0.18f, 0f),
                new Vector3(12.4f, 0.34f, 9.8f), dark);
            for (var column = -2; column <= 2; column++)
            {
                Shape("Floor Seam", PrimitiveType.Cube, new Vector3(column * 2.4f, -0.005f, 0f),
                    new Vector3(0.025f, 0.01f, 9.4f), edge);
            }

            for (var side = -1; side <= 1; side += 2)
            {
                Shape("Side Rail", PrimitiveType.Cube, new Vector3(side * 6.08f, 0.19f, 0f),
                    new Vector3(0.22f, 0.38f, 9.8f), edge);
                Shape("End Rail", PrimitiveType.Cube, new Vector3(0f, 0.19f, side * 4.78f),
                    new Vector3(12.4f, 0.38f, 0.22f), edge);
            }
        }

        private void CreateCameraAndLight()
        {
            var cameraObject = new GameObject("Station Camera");
            cameraObject.transform.SetParent(runRoot, false);
            cameraObject.transform.localPosition = new Vector3(0f, 18f, -9f);
            cameraObject.transform.LookAt(transform.position + Vector3.up * 0.15f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.08f);

            var sun = new GameObject("Station Light");
            sun.transform.SetParent(runRoot, false);
            sun.transform.localRotation = Quaternion.Euler(55f, -24f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
        }

        private GameObject Shape(string label, PrimitiveType kind, Vector3 position,
            Vector3 scale, Color color, Transform parent = null)
        {
            var shape = GameObject.CreatePrimitive(kind);
            shape.name = label;
            shape.transform.SetParent(parent != null ? parent : runRoot, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.GetComponent<Collider>().enabled = false;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            var renderer = shape.GetComponent<Renderer>();
            if (stationMaterial != null && GraphicsSettings.currentRenderPipeline != null)
                renderer.sharedMaterial = stationMaterial;
            renderer.SetPropertyBlock(properties);
            return shape;
        }

        private void OnGUI()
        {
            if (bootstrap == null)
                return;
            if (inventory == null || exit == null || bootstrap.Context == null)
            {
                GUILayout.BeginArea(new Rect((Screen.width - 240f) * 0.5f, Screen.height * 0.3f, 240f, 220f), GUI.skin.box);
                GUILayout.Label("能源回收站");
                if (GUILayout.Button("开始 [Enter]")) BeginRun();
                if (saves.Exists && GUILayout.Button("读取存档 [L]")) Load();
                if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);
                GUILayout.EndArea();
                return;
            }

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter };
                statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 16,
                    alignment = TextAnchor.MiddleCenter };
            }

            var width = Mathf.Min(460f, Screen.width - 24f);
            var panel = new Rect((Screen.width - width) * 0.5f, 12f, width, 90f);
            GUI.Box(panel, GUIContent.none);

            var seconds = (int)Math.Ceiling(exit.SecondsRemaining);
            var headline = exit.IsCleared ? "Station Cleared" : exit.HasTimedOut ? "Time Up"
                : $"Energy Cores  {inventory.Collected} / {inventory.Total}     {seconds / 60:00}:{seconds % 60:00}";
            var textColor = GUI.contentColor;
            if (exit.HasTimedOut || (!exit.IsCleared && seconds <= 10))
                GUI.contentColor = new Color(1f, 0.64f, 0.40f);
            GUI.Label(new Rect(panel.x, panel.y + 5f, width, 32f), headline, titleStyle);
            GUI.contentColor = textColor;

            var hint = exit.IsCleared ? $"Escaped with {seconds}s remaining"
                : exit.HasTimedOut ? $"{inventory.Collected} / {inventory.Total} cores recovered"
                : exit.IsUnlocked ? "Exit open  ·  Reach the green pad" : "Collect the cores before time runs out";
            GUI.Label(new Rect(panel.x, panel.y + 40f, width, 21f), hint, statusStyle);

            var controls = exit.IsCleared || exit.HasTimedOut ? "R to restart" : "WASD / Arrows  ·  Shift to sprint  ·  R to restart";
            GUI.Label(new Rect(panel.x, panel.y + 65f, width, 20f), controls, statusStyle);
            GUILayout.BeginArea(new Rect(panel.x, panel.yMax + 8f, width, 65f));
            GUILayout.BeginHorizontal();
            if (!exit.IsCleared && !exit.HasTimedOut)
            {
                if (bootstrap.IsRunning)
                {
                    if (GUILayout.Button("暂停")) bootstrap.PauseSession();
                }
                else if (GUILayout.Button("继续"))
                    bootstrap.ResumeSession();
            }
            if (GUILayout.Button("保存 [F5]")) Save();
            if (GUILayout.Button("重开")) BeginRun();
            if (GUILayout.Button("返回菜单")) EndRun();
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);
            GUILayout.EndArea();
        }

        private void FinishRun()
        {
            // Keep query state available for the final HUD and stop advancing the session.
            bootstrap.PauseSession();
        }

        private void EndRun()
        {
            coreSubscription?.Dispose();
            clearedSubscription?.Dispose();
            timedOutSubscription?.Dispose();

            coreSubscription = clearedSubscription = timedOutSubscription = null;
            if (inventory != null || characterView != null)
                bootstrap?.EndSession();
            if (runRoot != null)
            {
                runRoot.gameObject.SetActive(false);
                Destroy(runRoot.gameObject);
            }
            runRoot = null;
            inventory = null;
            exit = null;
            commands = null;
            input = null;
            characterView = null;
            status = null;
        }

        private void OnDestroy() => EndRun();
    }
}
