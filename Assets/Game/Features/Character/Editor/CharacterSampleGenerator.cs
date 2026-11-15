using System.IO;
using System.Linq;
using AFrame.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFrame.Game.Character
{
    public static class CharacterSampleGenerator
    {
        private const string CompositionRoot = "Assets/Game/Composition";
        private const string ModulePath = CompositionRoot + "/CharacterModule.asset";
        private const string ProfilePath = CompositionRoot + "/DefaultGameProfile.asset";
        private const string ScenePath = "Assets/Game/Scenes/AFrameSampleScene.unity";

        [MenuItem("AFrame/Generate/Sample Character")]
        public static void Generate()
        {
            Directory.CreateDirectory(CompositionRoot);

            var module = AssetDatabase.LoadAssetAtPath<CharacterModuleAsset>(ModulePath);
            if (module == null)
            {
                module = ScriptableObject.CreateInstance<CharacterModuleAsset>();
                AssetDatabase.CreateAsset(module, ModulePath);
            }
            module.SetDefaults();
            EditorUtility.SetDirty(module);

            var profile = AssetDatabase.LoadAssetAtPath<AFrameGameProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<AFrameGameProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile.Configure("default", new[] { module });
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            CreateScene(profile);
            AssetDatabase.Refresh();
            Debug.Log($"AFrame Character sample generated at {ScenePath}.");
        }

        private static void CreateScene(AFrameGameProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrapObject = new GameObject("AFrameBootstrap");
            var bootstrap = bootstrapObject.AddComponent<AFrameBootstrap>();
            bootstrap.SetProfile(profile);

            var character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            character.name = "Character";
            character.transform.position = Vector3.zero;
            var view = character.AddComponent<CharacterView>();
            var input = character.AddComponent<CharacterInputAdapter>();
            view.SetBootstrap(bootstrap);
            input.SetBootstrap(bootstrap);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 6f, -8f);
            camera.transform.LookAt(character.transform);

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(scene => scene.path == scenePath))
                return;

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
