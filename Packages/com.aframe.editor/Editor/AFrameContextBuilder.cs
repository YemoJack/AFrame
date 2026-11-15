using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AFrame.Data;
using AFrame.Unity;
using UnityEditor;
using UnityEngine;

namespace AFrame.Editor
{
    public static class AFrameContextBuilder
    {
        private const string ContextRoot = "Assets/Game/Generated/Context";

        public static void Build()
        {
            Directory.CreateDirectory(ContextRoot);
            var report = AFrameArchitectureValidator.Run();

            Write(
                "ProjectMap.json",
                new ProjectMapDocument
                {
                    projectName = Application.productName,
                    unityVersion = Application.unityVersion,
                    packages = ReadPackageNames(),
                    featureManifests = FindRelative("Assets/Game/Features", "feature.manifest.json"),
                    scripts = FindRelative("Assets/Game", "*.cs")
                });

            Write("FeatureContracts.json", BuildFeatureContracts());
            Write("ModuleGraph.json", BuildModuleGraph());
            Write("PublicApiIndex.json", BuildPublicApiIndex());
            File.WriteAllText(
                $"{ContextRoot}/ValidationReport.json",
                JsonUtility.ToJson(report, true) + Environment.NewLine);
            AssetDatabase.Refresh();

            Debug.Log($"AFrame project context written to {ContextRoot}.");
        }

        private static FeatureContractDocument BuildFeatureContracts()
        {
            var summaries = new List<FeatureContractSummary>();
            if (Directory.Exists("Assets/Game/Features"))
            {
                foreach (var path in Directory.GetFiles(
                             "Assets/Game/Features",
                             "feature.manifest.json",
                             SearchOption.AllDirectories))
                {
                    var manifest = JsonUtility.FromJson<FeatureManifest>(File.ReadAllText(path));
                    if (manifest == null)
                        continue;

                    summaries.Add(new FeatureContractSummary
                    {
                        id = manifest.id,
                        path = path.Replace('\\', '/'),
                        entryPoint = manifest.entryPoint,
                        dependencies = manifest.dependencies ?? Array.Empty<string>(),
                        acceptanceChecks = manifest.acceptanceChecks ?? Array.Empty<string>()
                    });
                }
            }

            return new FeatureContractDocument { features = summaries.ToArray() };
        }

        private static ModuleGraphDocument BuildModuleGraph()
        {
            var profiles = new List<ProfileModuleSummary>();
            foreach (var guid in AssetDatabase.FindAssets("t:AFrameGameProfile"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<AFrameGameProfile>(path);
                if (profile == null)
                    continue;

                profiles.Add(new ProfileModuleSummary
                {
                    profile = path,
                    modules = profile.Modules
                        .Where(module => module != null)
                        .Select(module => new ModuleSummary
                        {
                            id = module.IdString,
                            dependencies = module.Dependencies == null
                                ? Array.Empty<string>()
                                : module.Dependencies.Select(dependency => dependency.Value).ToArray()
                        })
                        .ToArray()
                });
            }

            return new ModuleGraphDocument { profiles = profiles.ToArray() };
        }

        private static PublicApiIndexDocument BuildPublicApiIndex()
        {
            var entries = new List<PublicApiSummary>();
            foreach (var path in FindFiles("Packages", "*.cs").Concat(FindFiles("Assets/Game", "*.cs")))
            {
                var source = File.ReadAllText(path);
                var matches = Regex.Matches(
                    source,
                    @"public\s+(?:(?:sealed|abstract|static|readonly)\s+)*(?:class|struct|interface|enum)\s+([A-Za-z0-9_]+)");
                if (matches.Count == 0)
                    continue;

                entries.Add(new PublicApiSummary
                {
                    assembly = Path.GetFileName(Path.GetDirectoryName(path)),
                    path = path.Replace('\\', '/'),
                    types = matches.Cast<Match>().Select(match => match.Groups[1].Value).Distinct().ToArray()
                });
            }

            return new PublicApiIndexDocument { entries = entries.ToArray() };
        }

        private static string[] ReadPackageNames()
        {
            if (!File.Exists("Packages/manifest.json"))
                return Array.Empty<string>();

            return File.ReadAllLines("Packages/manifest.json")
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("\"com.", StringComparison.Ordinal))
                .Select(line => line.Substring(1, line.IndexOf('"', 1) - 1))
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] FindRelative(string root, string pattern)
        {
            return FindFiles(root, pattern)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static IEnumerable<string> FindFiles(string root, string pattern)
        {
            return Directory.Exists(root)
                ? Directory.GetFiles(root, pattern, SearchOption.AllDirectories)
                : Enumerable.Empty<string>();
        }

        private static void Write<T>(string name, T value)
        {
            File.WriteAllText(
                $"{ContextRoot}/{name}",
                JsonUtility.ToJson(value, true) + Environment.NewLine);
        }
    }
}
