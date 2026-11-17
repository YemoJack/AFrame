using System;
using System.IO;
using System.Linq;
using AFrame.Unity;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFrame.Editor
{
    [InitializeOnLoad]
    public static class AFrameEditorCommands
    {
        private const string Requests = "Library/AFrame/Requests";
        private const string ActiveKey = "AFrame.ActiveEditorRequest";

        [Serializable]
        private sealed class Command
        {
            public string name;
            public string scene;
            public string feature;
            public string testFilter;
            public string result;
            public string batch;
            public string phase;
            public string error;
            public double start;
            public long tick;
            public SceneRecord[] setup;
        }

        [Serializable]
        private sealed class SceneRecord
        {
            public string path;
            public bool loaded;
            public bool active;
        }

        static AFrameEditorCommands()
        {
            EditorApplication.update += Update;
            CompilationPipeline.assemblyCompilationFinished += CompilationFinished;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void Batch()
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, "-aframeRequest");
            if (index < 0 || index + 1 == arguments.Length)
                throw new ArgumentException("Missing -aframeRequest.");
            var command = JsonUtility.FromJson<Command>(File.ReadAllText(arguments[index + 1]));
            command.batch = "true";
            Start(command, refresh: false);
        }

        internal static void Menu(string name)
        {
            if (Active() != null || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Finish the current Play Mode or AFrame command first.");
            Start(new Command { name = name }, refresh: false);
        }

        private static Command Active()
        {
            var json = SessionState.GetString(ActiveKey, "");
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Command>(json);
        }

        private static void Persist(Command command) => SessionState.SetString(ActiveKey, JsonUtility.ToJson(command));

        private static void Start(Command command, bool refresh)
        {
            command.start = EditorApplication.timeSinceStartup;
            if (refresh)
            {
                command.phase = "compile";
                Persist(command);
                try
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    CompilationPipeline.RequestScriptCompilation();
                }
                catch (Exception error) { Complete(command, false, error.Message); }
            }
            else
                Execute(command);
        }

        private static void CompilationFinished(string assembly, CompilerMessage[] messages)
        {
            var command = Active();
            if (command?.phase != "compile")
                return;
            var errors = messages.Where(message => message.type == CompilerMessageType.Error)
                .Select(message => $"{message.file}:{message.line}: {message.message}").ToArray();
            if (errors.Length > 0)
            {
                command.error = string.Join("\n", errors);
                Persist(command);
            }
        }

        private static void Execute(Command command)
        {
            try
            {
                switch (command.name)
                {
                    case "compile": break;
                    case "initialize":
                        AFrameProjectInitializer.Initialize();
                        if (command.batch == "true")
                        {
                            command.name = "initialize-context";
                            Start(command, refresh: true);
                            return;
                        }
                        break;
                    case "initialize-context": AFrameContextBuilder.Build(); break;
                    case "import": AFrameDefinitionImporter.ImportAll(); break;
                    case "context": AFrameContextBuilder.Build(); break;
                    case "validate":
                        if (!AFrameArchitectureValidator.Run().succeeded)
                            throw new InvalidOperationException("See Logs/AFrame/validate.json.");
                        break;
                    case "test":
                        command.phase = "test";
                        command.start = EditorApplication.timeSinceStartup;
                        Persist(command);
                        AFrameEditModeTests.Run(command.feature, command.testFilter,
                            (succeeded, error) => Complete(command, succeeded, error));
                        return;
                    case "smoke":
                        BeginPlayMode(command);
                        return;
                    default: throw new ArgumentException($"Unknown AFrame command {command.name}.");
                }
                Complete(command, true, "");
            }
            catch (Exception error) { Fail(command, error); }
        }

        private static void BeginPlayMode(Command command)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running the smoke check.");
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                    throw new InvalidOperationException("Save the open scenes before running the smoke check.");
            }
            command.scene = string.IsNullOrEmpty(command.scene)
                ? (File.Exists("Assets/Game/Scenes/EnergyStation.unity") ? "Assets/Game/Scenes/EnergyStation.unity"
                    : "Assets/Game/Scenes/AFrameStart.unity")
                : command.scene;
            command.setup = EditorSceneManager.GetSceneManagerSetup().Select(scene => new SceneRecord
            {
                path = scene.path, loaded = scene.isLoaded, active = scene.isActive
            }).ToArray();
            command.phase = "enter";
            command.start = EditorApplication.timeSinceStartup;
            Persist(command);
            EditorSceneManager.OpenScene(command.scene, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            var command = Active();
            if (command?.phase == "exit" && state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreSetup(command);
                Complete(command, string.IsNullOrEmpty(command.error), command.error);
            }
        }

        private static void RestoreSetup(Command command)
        {
            if (!string.IsNullOrEmpty(command.batch) || command.setup == null)
                return;
            try
            {
                if (command.setup.Any(scene => scene.active && scene.loaded)
                    && command.setup.All(scene => !string.IsNullOrEmpty(scene.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(command.setup.Select(scene => new SceneSetup
                    {
                        path = scene.path, isLoaded = scene.loaded, isActive = scene.active
                    }).ToArray());
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            catch (Exception error) { command.error = $"Could not restore open scenes: {error.Message}"; }
        }

        private static void Update()
        {
            var command = Active();
            if (command == null)
            {
                if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode
                    && Directory.Exists(Requests))
                {
                    var path = Directory.GetFiles(Requests, "*.request.json").OrderBy(File.GetCreationTimeUtc).FirstOrDefault();
                    if (path != null)
                    {
                        var request = JsonUtility.FromJson<Command>(File.ReadAllText(path));
                        File.Delete(path);
                        Start(request, refresh: true);
                    }
                }
                return;
            }

            if (command.phase == "compile")
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating
                    || EditorApplication.timeSinceStartup - command.start < 1d)
                    return;
                if (!string.IsNullOrEmpty(command.error))
                    Complete(command, false, command.error);
                else
                    Execute(command);
                return;
            }

            if (command.phase == "test")
            {
                if (EditorApplication.timeSinceStartup - command.start > 150d)
                    Complete(command, false, "EditMode test runner timed out.");
                return;
            }
            if (command.phase == "exit")
                return;
            try
            {
                var roots = EditorApplication.isPlaying ? SceneManager.GetActiveScene().GetRootGameObjects()
                    : Array.Empty<GameObject>();
                var host = roots.Select(root => root.GetComponent<AFrameSceneEntry>()?.Host
                    ?? root.GetComponent<SceneScope>()?.Host).FirstOrDefault(value => value != null);
                if (EditorApplication.timeSinceStartup - command.start > 30d)
                    throw new TimeoutException($"Smoke did not advance: phase {command.phase}, Play Mode "
                        + $"{EditorApplication.isPlaying}, host present {host != null}, state {host?.SessionState}, Tick {host?.Context?.Tick}.");
                if (host == null)
                    return;
                if (host.SessionFault != null)
                    throw host.SessionFault;
                var scenario = roots.SelectMany(root => root.GetComponents<IAFrameSmokeScenario>()).SingleOrDefault();
                if (command.phase == "enter")
                {
                    scenario?.BeginSmoke();
                    if (host.Context == null)
                        return;
                    command.tick = host.Context.Tick;
                    command.phase = "run";
                    Persist(command);
                }
                else if (command.phase == "run" && (scenario?.SmokeComplete ?? (host.Context != null && host.Context.Tick >= command.tick + 6)))
                {
                    scenario?.VerifySmoke();
                    ExitPlayMode(command, "");
                }
            }
            catch (Exception error) { Fail(command, error); }
        }

        private static void Fail(Command command, Exception error)
        {
            if (command.name == "smoke" && EditorApplication.isPlayingOrWillChangePlaymode)
                ExitPlayMode(command, error.ToString());
            else
            {
                RestoreSetup(command);
                Complete(command, false, error.ToString());
            }
        }

        private static void ExitPlayMode(Command command, string error)
        {
            command.error = error;
            command.phase = "exit";
            Persist(command);
            EditorApplication.ExitPlaymode();
        }

        private static void Complete(Command command, bool succeeded, string error)
        {
            SessionState.EraseString(ActiveKey);
            var report = new { command = command.name, succeeded, error };
            AFrameContextBuilder.WriteJson("Logs/AFrame/" + command.name + "-result.json", report);
            if (!string.IsNullOrEmpty(command.result))
            {
                var temporary = command.result + ".tmp";
                AFrameContextBuilder.WriteJson(temporary, report);
                File.Move(temporary, command.result);
            }
            if (succeeded)
                Debug.Log($"AFrame {command.name} passed.");
            else
                Debug.LogError($"AFrame {command.name} failed. {error}");
            if (command.batch == "true")
                EditorApplication.Exit(succeeded ? 0 : 1);
        }
    }
}
