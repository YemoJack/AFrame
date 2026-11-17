using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AFrame.Data;
using AFrame.Runtime;
using AFrame.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace AFrame.Editor
{
    public static class AFrameContextBuilder
    {
        private const string Root = "Context/";

        public static void Build()
        {
            foreach (var file in Render())
                Write(file.Key, file.Value);
        }

        internal static IEnumerable<string> StaleFiles() =>
            Render().Where(file => !File.Exists(file.Key) || File.ReadAllText(file.Key) != file.Value)
                .Select(file => file.Key);

        internal static void WriteJson(string path, object value) => Write(path, Serialize(value));

        private static void Write(string path, string payload)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path) || File.ReadAllText(path) != payload)
                File.WriteAllText(path, payload);
        }

        private static string Serialize(object value) => JsonConvert.SerializeObject(value, Formatting.Indented) + "\n";

        private static Dictionary<string, string> Render()
        {
            var assemblies = CompilationPipeline.GetAssemblies()
                .Select(assembly => new { assembly, path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name) })
                .Where(item => AFrameArchitectureValidator.IsAFrameAssembly(item.path))
                .OrderBy(item => item.assembly.name, StringComparer.Ordinal).ToArray();
            var features = Directory.Exists("Assets/Game/Features")
                ? Directory.GetFiles("Assets/Game/Features", "feature.json", SearchOption.AllDirectories)
                    .Select(path => path.Replace('\\', '/')).OrderBy(path => path, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            var scenes = AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/Game/", StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var profiles = AssetDatabase.FindAssets("t:AFrameGameProfile").Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/Game/", StringComparison.Ordinal)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var loaded = AppDomain.CurrentDomain.GetAssemblies().GroupBy(assembly => assembly.GetName().Name)
                .ToDictionary(group => group.Key, group => group.First());
            var validation = AFrameArchitectureValidator.Run(false, false);
            var featureCatalog = features.Select(path => new
            {
                path,
                definition = ReadManifest(path),
                assemblies = assemblies.Where(item => item.path.StartsWith(Path.GetDirectoryName(path).Replace('\\', '/') + "/", StringComparison.Ordinal))
                    .Select(item => item.assembly.name)
            });
            return new Dictionary<string, string>
            {
                [Root + "ProjectMap.json"] = Serialize(new
                {
                    unityVersion = Application.unityVersion, scenes, profiles, features,
                    packages = Directory.GetDirectories("Packages", "com.aframe.*").Select(Path.GetFileName)
                        .OrderBy(name => name, StringComparer.Ordinal)
                }),
                [Root + "ModuleGraph.json"] = Serialize(new
                {
                    profiles = profiles.Select(path => DescribeProfile(path)),
                    assemblies = assemblies.Select(item => new
                    {
                        name = item.assembly.name, item.path,
                        references = item.assembly.assemblyReferences.Select(reference => reference.name)
                            .OrderBy(value => value, StringComparer.Ordinal)
                    })
                }),
                [Root + "FeatureContracts.json"] = Serialize(featureCatalog),
                [Root + "PublicApiIndex.json"] = Serialize(assemblies.Where(item => loaded.ContainsKey(item.assembly.name))
                    .Select(item => new
                    {
                        assembly = item.assembly.name, item.path,
                        types = loaded[item.assembly.name].GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal)
                            .Select(type => new
                            {
                                name = type.FullName,
                                members = type.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                    .Select(member => member.ToString()).OrderBy(name => name, StringComparer.Ordinal)
                            })
                    })),
                [Root + "ValidationReport.json"] = Serialize(new
                {
                    validation.succeeded, validation.covered, validation.checkedScopes, validation.notCovered, validation.issues
                })
            };
        }

        private static object ReadManifest(string path)
        {
            try { return JObject.Parse(File.ReadAllText(path)); }
            catch (Exception error) { return new { error = error.Message }; }
        }

        private static object DescribeProfile(string path)
        {
            try
            {
                var modules = AssetDatabase.LoadAssetAtPath<AFrameGameProfile>(path).CreateModules();
                return new
                {
                    path,
                    modules = modules.OrderBy(module => module.Id.Value, StringComparer.Ordinal).Select(module =>
                    {
                        var registration = GameModuleHost.DescribeRegistration(module);
                        return new
                        {
                            id = module.Id.Value,
                            scope = module.Scope.ToString(),
                            dependencies = (module.Dependencies ?? Array.Empty<AFrame.Foundation.ModuleId>())
                                .Select(id => id.Value).OrderBy(id => id, StringComparer.Ordinal),
                            requires = registration.Required.Select(type => type.FullName),
                            provides = registration.Provided.Select(type => type.FullName),
                            providesMany = registration.Collections.Select(type => type.FullName),
                            commands = registration.Commands.Select(type => type.FullName)
                        };
                    }).ToArray()
                };
            }
            catch (Exception error) { return new { path, error = error.Message }; }
        }
    }
}
