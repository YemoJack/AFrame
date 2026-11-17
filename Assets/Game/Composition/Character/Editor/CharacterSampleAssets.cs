using System;
using System.IO;
using System.Linq;
using AFrame.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFrame.Game.Character
{
    internal static class CharacterSampleAssets
    {
        private const string Folder = "Assets/Game/Composition/Character";
        private const string ScenePath = "Assets/Game/Scenes/Character2D.unity";

        // Creation is explicit so opening the project never overwrites scenes or prefabs.
        public static void Create2D()
        {
            var profile = Resources.Load<AFrameGameProfile>(AFrameBootstrap.ProfileResource);
            if (profile == null)
                throw new InvalidOperationException("The sample requires the default AFrameGameProfile.");
            var prefab = CreatePrefab();
            var serializedProfile = new SerializedObject(profile);
            var views = serializedProfile.FindProperty("views");
            if (!Enumerable.Range(0, views.arraySize).Any(index =>
                    views.GetArrayElementAtIndex(index).FindPropertyRelative("key").stringValue == "character.sample.2d"))
            {
                views.InsertArrayElementAtIndex(views.arraySize);
                var entry = views.GetArrayElementAtIndex(views.arraySize - 1);
                entry.FindPropertyRelative("key").stringValue = "character.sample.2d";
                entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            }

            if (!File.Exists(ScenePath))
            {
                var setup = EditorSceneManager.GetSceneManagerSetup();
                var additive = setup.Any(scene => scene.isActive) && setup.All(scene => !string.IsNullOrEmpty(scene.path));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    var root = new GameObject("AFrame Character 2D");
                    var sample = new SerializedObject(root.AddComponent<CharacterSampleEntry>());
                    sample.FindProperty("dimension").enumValueIndex = (int)GameDimension.TwoD;
                    sample.ApplyModifiedPropertiesWithoutUndo();

                    var camera = new GameObject("Main Camera").AddComponent<Camera>();
                    camera.tag = "MainCamera";
                    camera.transform.position = new Vector3(0, 0, -10);
                    camera.orthographic = true;
                    camera.orthographicSize = 6;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.10f, 0.14f, 0.22f);
                    if (!EditorSceneManager.SaveScene(scene, ScenePath))
                        throw new IOException($"Could not create {ScenePath}.");
                }
                finally
                {
                    if (additive)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                        EditorSceneManager.RestoreSceneManagerSetup(setup);
                    }
                }
            }

            if (!EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreatePrefab()
        {
            var path = Folder + "/Character2D.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            var texturePath = Folder + "/Character2D.png";
            if (!File.Exists(texturePath))
            {
                var texture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
                try
                {
                    for (var y = 0; y < 24; y++)
                        for (var x = 0; x < 24; x++)
                        {
                            var dx = x - 11.5f;
                            var dy = y - 11.5f;
                            texture.SetPixel(x, y, dx * dx + dy * dy > 120 ? Color.clear : Color.white);
                        }
                    texture.Apply();
                    File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 24;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            var root = new GameObject("Character 2D View");
            try
            {
                var sprite = root.AddComponent<SpriteRenderer>();
                sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                sprite.color = new Color(0.24f, 0.85f, 1);
                var view = new SerializedObject(root.AddComponent<CharacterView>());
                view.FindProperty("dimension").enumValueIndex = (int)GameDimension.TwoD;
                view.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
