using System;
using System.IO;
using System.Linq;
using AFrame.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFrame.Editor
{
    public static class AFrameProjectInitializer
    {
        private const string ScenePath = "Assets/Game/Scenes/AFrameStart.unity";
        private const string ProfilePath = "Assets/Game/ProjectConfig/Resources/AFrameGameProfile.asset";

        [MenuItem("AFrame/Initialize Project")]
        private static void Menu() => Initialize();

        public static void Initialize()
        {
            var profilePaths = AssetDatabase.FindAssets("t:AFrameGameProfile").Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith("/Resources/AFrameGameProfile.asset", StringComparison.Ordinal)).ToArray();
            if (profilePaths.Length > 1)
                throw new InvalidOperationException("Multiple default AFrame profiles already exist.");
            if (profilePaths.Length == 0 && File.Exists(ProfilePath))
                throw new IOException($"Profile path is already in use: {ProfilePath}.");

            var wasActive = SceneManager.GetActiveScene();
            var replaceUntitled = string.IsNullOrEmpty(wasActive.path);
            if (!File.Exists(ScenePath) && replaceUntitled && (wasActive.isDirty || SceneManager.sceneCount != 1))
                throw new InvalidOperationException("Save the untitled scene before initializing AFrame.");

            foreach (var directory in new[] { "Assets/Game/Composition", "Assets/Game/Features",
                         "Assets/Game/Scenes", Path.GetDirectoryName(ProfilePath), "Tasks" })
                Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();

            if (profilePaths.Length == 0)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AFrameGameProfile>(), ProfilePath);

            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    replaceUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    new GameObject("AFrame Scene Scope").AddComponent<SceneScope>();
                    var camera = new GameObject("Main Camera").AddComponent<Camera>();
                    camera.tag = "MainCamera";
                    if (!EditorSceneManager.SaveScene(scene, ScenePath))
                        throw new IOException($"Could not create {ScenePath}.");
                }
                finally
                {
                    if (!replaceUntitled)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                        if (wasActive.IsValid() && wasActive.isLoaded)
                            SceneManager.SetActiveScene(wasActive);
                    }
                }
            }

            if (!EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();

            var task = "Tasks/first-feature.task.json";
            if (!File.Exists(task))
            {
                AFrameContextBuilder.WriteJson(task, new
                {
                    id = "first-feature",
                    goal = "Add a gameplay feature and its Unity view.",
                    allowedPaths = new[] { "Assets/Game/Features/**", "Assets/Game/Composition/**", "Assets/Game/Scenes/**", "Context/**" },
                    readOnlyPaths = new[] { "Packages/**", "Assets/Game/Generated/**" },
                    acceptanceChecks = new[] { "compile", "validate", "smoke" }
                });
            }
            AssetDatabase.SaveAssets();
            AFrameContextBuilder.Build();
            Debug.Log($"AFrame is ready. Open {ScenePath} and enter Play Mode.");
        }
    }
}
