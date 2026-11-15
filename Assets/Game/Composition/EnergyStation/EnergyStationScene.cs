using System;
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

namespace AFrame.Game.EnergyStation
{
    public sealed class EnergyStationScene : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float timeLimitSeconds = 45f;
        [SerializeField] private Vector3[] coreLocations =
        {
            new Vector3(-4.2f, 0f, -2.2f),
            new Vector3(3.8f, 0f, -2.5f),
            new Vector3(-3.3f, 0f, 2.6f)
        };

        private readonly List<AFrameModuleAsset> modules = new List<AFrameModuleAsset>();
        private Transform runRoot;
        private AFrameGameProfile profile;
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

        private void Awake()
        {
            if (coreLocations == null || coreLocations.Length == 0)
                throw new InvalidOperationException("The Energy Station needs at least one core location.");

            BeginRun();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                EndRun();
                BeginRun();
            }

            if (bootstrap == null || !bootstrap.isActiveAndEnabled)
                return;

            foreach (var crystal in crystals)
                crystal.Rotate(Vector3.up, 55f * Time.deltaTime, Space.World);
        }

        private void BeginRun()
        {
            exitRequested = false;
            var root = new GameObject("Station Run");
            runRoot = root.transform;
            runRoot.SetParent(transform, false);

            StartSession();
            CreateRoom();
            var player = CreatePlayer();
            CreateCores(player);
            CreateExit(player);
            CreateCameraAndLight();

            coreSubscription = bootstrap.Context.Events.Subscribe<EnergyCoreStored>(OnCoreStored);
            clearedSubscription = bootstrap.Context.Events.Subscribe<StationCleared>(_ => FinishRun());
            timedOutSubscription = bootstrap.Context.Events.Subscribe<StationTimedOut>(_ =>
            {
                gate.SetActive(true);
                FinishRun();
            });
        }

        private void StartSession()
        {
            var character = ScriptableObject.CreateInstance<CharacterModuleAsset>();
            character.SetDefaults();
            character.SetMovementBounds(new Vector2(5.65f, 4.4f));
            modules.Add(character);
            AddModule(new InventoryModule(coreLocations.Length));
            AddModule(new PickupModule());
            AddModule(new ExitModule(timeLimitSeconds));

            profile = ScriptableObject.CreateInstance<AFrameGameProfile>();
            profile.Configure("energy-station", modules);

            var root = new GameObject("Energy Station Bootstrap");
            root.SetActive(false);
            bootstrap = root.AddComponent<AFrameBootstrap>();
            bootstrap.SetProfile(profile);
            root.SetActive(true);

            inventory = bootstrap.Context.Get<IInventoryQuery>();
            exit = bootstrap.Context.Get<IStationExitQuery>();
            commands = bootstrap.Context.Get<ICommandSink>();
        }

        private void AddModule(IGameModule module)
        {
            var asset = ScriptableObject.CreateInstance<StationModuleAsset>();
            asset.Initialize(module);
            modules.Add(asset);
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
            var view = player.AddComponent<CharacterView>();
            view.SetBootstrap(bootstrap);
            input = player.AddComponent<CharacterInputAdapter>();
            input.SetBootstrap(bootstrap);
            input.EnableSprint();

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
                    if (requested || inventory.Contains(coreId))
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
                if (exitRequested || !exit.IsUnlocked)
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
            shape.GetComponent<Renderer>().SetPropertyBlock(properties);
            return shape;
        }

        private void OnGUI()
        {
            if (inventory == null || exit == null)
                return;

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

            var status = exit.IsCleared ? $"Escaped with {seconds}s remaining"
                : exit.HasTimedOut ? $"{inventory.Collected} / {inventory.Total} cores recovered"
                : exit.IsUnlocked ? "Exit open  ·  Reach the green pad" : "Collect the cores before time runs out";
            GUI.Label(new Rect(panel.x, panel.y + 40f, width, 21f), status, statusStyle);

            var controls = exit.IsCleared || exit.HasTimedOut ? "R to restart" : "WASD / Arrows  ·  Shift to sprint  ·  R to restart";
            GUI.Label(new Rect(panel.x, panel.y + 65f, width, 20f), controls, statusStyle);
        }

        private void FinishRun()
        {
            // Keep query state available for the final HUD and stop advancing the session.
            bootstrap.enabled = false;
            input.enabled = false;
        }

        private void EndRun()
        {
            coreSubscription?.Dispose();
            clearedSubscription?.Dispose();
            timedOutSubscription?.Dispose();

            if (runRoot != null)
            {
                runRoot.gameObject.SetActive(false);
                Destroy(runRoot.gameObject);
            }
            if (bootstrap != null)
            {
                bootstrap.StopGame();
                bootstrap.gameObject.SetActive(false);
                Destroy(bootstrap.gameObject);
            }

            foreach (var module in modules)
                Destroy(module);
            modules.Clear();
            if (profile != null)
                Destroy(profile);
        }

        private void OnDestroy() => EndRun();
    }
}
