using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AFrame.Runtime;
using AFrame.Unity;
using Mono.Cecil;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AFrame.Editor
{
    internal static class AFrameRuntimeRules
    {
        internal static void CheckCompiledCode(Action<string, string, string> issue)
        {
            foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player)
                         .OrderBy(assembly => assembly.name, StringComparer.Ordinal))
            {
                var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                if (!AFrameArchitectureValidator.IsAFrameAssembly(path))
                    continue;
                try
                {
                    if (!File.Exists(assembly.outputPath))
                        throw new FileNotFoundException("Compiled assembly is unavailable. Compile before validating.", assembly.outputPath);
                    using var definition = AssemblyDefinition.ReadAssembly(assembly.outputPath);
                    foreach (var type in AllTypes(definition.MainModule.Types))
                    {
                        foreach (var method in type.Methods.Where(method => method.HasBody))
                        {
                            foreach (var instruction in method.Body.Instructions)
                            {
                                if (instruction.Operand is MethodReference call && Forbidden(call, assembly.name, type.FullName))
                                    issue("cecil", path, $"{method.FullName} at IL_{instruction.Offset:x4} calls prohibited API {call.FullName}.");
                            }
                        }
                    }
                }
                catch (Exception error) { issue("cecil", path, error.Message); }
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name);
                if (!AFrameArchitectureValidator.IsAFrameAssembly(path) || name.EndsWith(".Editor", StringComparison.Ordinal)
                    || name.EndsWith(".Tests", StringComparison.Ordinal))
                    continue;
                var isCore = name.EndsWith(".Core", StringComparison.Ordinal);
                foreach (var type in assembly.GetTypes())
                {
                    if (isCore && type.IsVisible && !typeof(IGameModule).IsAssignableFrom(type))
                        issue("reflection", path, $"Core type {type.FullName} must be internal; only module entry types may be public.");
                    foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                                                        | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
                        if (!field.IsLiteral && !field.IsInitOnly && !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                            issue("reflection", path, $"Mutable static field {type.FullName}.{field.Name}.");
                }
            }
        }

        private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
        {
            foreach (var type in types)
            {
                yield return type;
                foreach (var nested in AllTypes(type.NestedTypes))
                    yield return nested;
            }
        }

        private static bool Forbidden(MethodReference call, string assembly, string caller)
        {
            var declaring = call.DeclaringType.FullName;
            var name = call.Name;
            if (declaring == "System.Random")
                return assembly != "AFrame.Foundation" || caller != "AFrame.Foundation.SystemRandomSource";
            if (declaring == "UnityEngine.Object" || declaring == "UnityEngine.GameObject")
                return name.StartsWith("Find", StringComparison.Ordinal)
                    || name.StartsWith("SendMessage", StringComparison.Ordinal);
            if (declaring == "UnityEngine.Component")
                return name.StartsWith("SendMessage", StringComparison.Ordinal);
            if (declaring == "System.DateTime" || declaring == "System.DateTimeOffset")
                return name == "get_Now" || name == "get_UtcNow" || name == "get_Today";
            return declaring == "System.Environment" && (name == "get_TickCount" || name == "get_TickCount64");
        }

        internal static void CheckAssets(IReadOnlyDictionary<string, JObject> features, AFrameGameProfile profile,
            Action<string, string, string> issue)
        {
            foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                var scene = default(UnityEngine.SceneManagement.Scene);
                try
                {
                    scene = EditorSceneManager.OpenPreviewScene(path);
                    var roots = scene.GetRootGameObjects();
                    var scopes = roots.SelectMany(root => root.GetComponentsInChildren<SceneScope>(true)).ToArray();
                    if (scopes.Length > 1 || scopes.Any(scope => scope.transform.parent != null))
                        issue("scene", path, "A scene may contain at most one SceneScope, on a root GameObject.");
                    var entries = roots.SelectMany(root => root.GetComponentsInChildren<AFrameSceneEntry>(true)).ToArray();
                    if (entries.Length > 1 || entries.Any(entry => entry.transform.parent != null))
                        issue("scene", path, "A scene may contain at most one AFrameSceneEntry, on a root GameObject.");
                    if (roots.Any(root => root.GetComponentsInChildren<AFrameBootstrap>(true).Length != 0))
                        issue("scene", path, "AFrameBootstrap is created at runtime and cannot be serialized in a scene.");
                    foreach (var entry in entries)
                        CheckFeatures(features, entry.Features, entry.DimensionOverride ?? profile?.Dimension ?? GameDimension.ThreeD,
                            profile?.NetworkMode ?? GameNetworkMode.Offline, path, issue);
                }
                catch (Exception error) { issue("scene", path, error.Message); }
                finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
            }

            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                         .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    if (root.GetComponentsInChildren<AFrameBootstrap>(true).Length != 0)
                        issue("prefab", path, "AFrameBootstrap cannot be serialized in a prefab.");
                }
                catch (Exception error) { issue("prefab", path, error.Message); }
                finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        internal static void CheckFeatures(IReadOnlyDictionary<string, JObject> features, IEnumerable<string> enabled,
            GameDimension dimension, GameNetworkMode network, string path, Action<string, string, string> issue)
        {
            if (!Enum.IsDefined(typeof(GameDimension), dimension) || !Enum.IsDefined(typeof(GameNetworkMode), network))
            {
                issue("profile", path, "Invalid dimension or network mode.");
                return;
            }

            var dimensionId = dimension == GameDimension.TwoD ? "2d" : "3d";
            var networkId = network == GameNetworkMode.Offline ? "offline" : "online";
            foreach (var id in enabled.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(id) || !features.TryGetValue(id, out var feature))
                {
                    issue("profile", path, $"Enabled feature '{id}' has no manifest.");
                    continue;
                }
                if (!Supports(feature, "dimensions", dimensionId) || !Supports(feature, "network", networkId))
                    issue("profile", path, $"Feature '{id}' does not support {dimensionId}/{networkId}.");
            }
        }

        private static bool Supports(JObject feature, string field, string value) =>
            feature["supports"] is JObject support && support[field] is JArray choices
            && choices.Any(choice => choice.Type == JTokenType.String && (string)choice == value);
    }
}
