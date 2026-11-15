using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AFrame.Data;
using AFrame.Runtime;
using AFrame.Unity;
using UnityEditor;
using UnityEngine;

namespace AFrame.Editor
{
    public static class AFrameArchitectureValidator
    {
        public const string ReportPath = "Assets/Game/Generated/ValidationReport.json";

        public static ValidationReport Run(bool writeReport = true)
        {
            var issues = new List<ValidationIssue>();
            ValidateFeatures(issues);
            ValidateDefinitions(issues);
            ValidateGeneratedDefinitions(issues);
            ValidateDomainBoundaries(issues);
            ValidateProfiles(issues);

            var report = new ValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                succeeded = issues.All(issue => issue.severity != "error"),
                issues = issues.ToArray()
            };

            if (writeReport)
            {
                Directory.CreateDirectory("Assets/Game/Generated");
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true) + Environment.NewLine);
                AssetDatabase.Refresh();
            }

            return report;
        }

        private static void ValidateFeatures(List<ValidationIssue> issues)
        {
            var manifests = new Dictionary<string, FeatureManifest>(StringComparer.Ordinal);
            var manifestPaths = Directory.Exists("Assets/Game/Features")
                ? Directory.GetFiles("Assets/Game/Features", "feature.manifest.json", SearchOption.AllDirectories)
                : Array.Empty<string>();

            foreach (var path in manifestPaths)
            {
                FeatureManifest manifest;
                try
                {
                    manifest = JsonUtility.FromJson<FeatureManifest>(File.ReadAllText(path));
                }
                catch (Exception exception)
                {
                    Add(issues, "error", path, $"Feature manifest is invalid JSON: {exception.Message}");
                    continue;
                }

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.id))
                {
                    Add(issues, "error", path, "Feature manifest requires a non-empty id.");
                    continue;
                }

                if (manifests.ContainsKey(manifest.id))
                {
                    Add(issues, "error", path, $"Feature id is duplicated: {manifest.id}.");
                    continue;
                }

                manifests.Add(manifest.id, manifest);
            }

            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var manifest in manifests)
                VisitFeature(manifest.Key, manifests, visiting, visited, issues);
        }

        private static void VisitFeature(
            string id,
            IReadOnlyDictionary<string, FeatureManifest> manifests,
            HashSet<string> visiting,
            HashSet<string> visited,
            List<ValidationIssue> issues)
        {
            if (visited.Contains(id))
                return;
            if (!visiting.Add(id))
            {
                Add(issues, "error", "Assets/Game/Features", $"Feature dependency cycle detected at {id}.");
                return;
            }

            foreach (var dependency in manifests[id].dependencies ?? Array.Empty<string>())
            {
                if (manifests.ContainsKey(dependency))
                    VisitFeature(dependency, manifests, visiting, visited, issues);
                else if (dependency.StartsWith("game.", StringComparison.Ordinal))
                    Add(issues, "error", "Assets/Game/Features", $"Feature {id} depends on missing feature {dependency}.");
            }

            visiting.Remove(id);
            visited.Add(id);
        }

        private static void ValidateDefinitions(List<ValidationIssue> issues)
        {
            if (!Directory.Exists("Assets/Game"))
                return;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles("Assets/Game", "*.aframe.json", SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/').IndexOf("/Generated/", StringComparison.Ordinal) >= 0)
                    continue;

                if (!DefinitionSourceReader.TryRead(path, out var document, out _, out var error))
                {
                    Add(issues, "error", path, error);
                    continue;
                }

                if (!ids.Add(document.id))
                    Add(issues, "error", path, $"Definition id is duplicated: {document.id}.");
            }
        }

        private static void ValidateGeneratedDefinitions(List<ValidationIssue> issues)
        {
            if (!Directory.Exists("Assets/Game/Generated/Definitions"))
                return;

            var sourcePaths = Directory.Exists("Assets/Game")
                ? Directory.GetFiles("Assets/Game", "*.aframe.json", SearchOption.AllDirectories)
                : Array.Empty<string>();

            foreach (var sourcePath in sourcePaths)
            {
                var normalized = sourcePath.Replace('\\', '/');
                if (normalized.IndexOf("/Generated/", StringComparison.Ordinal) >= 0)
                    continue;
                if (!DefinitionSourceReader.TryRead(normalized, out var document, out var json, out _))
                    continue;

                var assetPath = "Assets/Game/Generated/Definitions/" + SanitizeAssetName(document.id) + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<AFrameDefinitionAsset>(assetPath);
                if (asset == null)
                {
                    Add(issues, "error", assetPath, $"Generated definition is missing for source id {document.id}.");
                    continue;
                }

                var hash = DefinitionSourceReader.ComputeHash(json);
                if (!string.Equals(asset.SourceHash, hash, StringComparison.Ordinal)
                    || !string.Equals(asset.SourcePath, normalized, StringComparison.Ordinal))
                {
                    Add(issues, "error", assetPath, "Generated definition is out of date. Run AFrame/Content/Import Definitions.");
                }
            }
        }

        private static string SanitizeAssetName(string value)
        {
            var builder = new System.Text.StringBuilder(value == null ? 0 : value.Length);
            foreach (var character in value ?? string.Empty)
                builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_');
            return builder.Length == 0 ? "Definition" : builder.ToString();
        }

        private static void ValidateDomainBoundaries(List<ValidationIssue> issues)
        {
            if (!Directory.Exists("Assets/Game/Features"))
                return;

            foreach (var path in Directory.GetFiles("Assets/Game/Features", "*.cs", SearchOption.AllDirectories))
            {
                var normalized = path.Replace('\\', '/');
                var source = File.ReadAllText(path);
                if (normalized.IndexOf("/Runtime/Domain/", StringComparison.Ordinal) >= 0
                    && (source.IndexOf("using UnityEngine", StringComparison.Ordinal) >= 0
                        || source.IndexOf("UnityEngine.", StringComparison.Ordinal) >= 0))
                {
                    Add(issues, "error", path, "Domain code cannot reference UnityEngine.");
                }

                if (source.IndexOf("GameObject.Find(", StringComparison.Ordinal) >= 0
                    || source.IndexOf("FindObjectOfType<", StringComparison.Ordinal) >= 0
                    || source.IndexOf("Resources.Load(", StringComparison.Ordinal) >= 0)
                {
                    Add(issues, "warning", path, "Implicit Unity lookup detected. Prefer an explicit adapter dependency.");
                }
            }
        }

        private static void ValidateProfiles(List<ValidationIssue> issues)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AFrameGameProfile"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<AFrameGameProfile>(path);
                if (profile == null)
                    continue;

                try
                {
                    new ModuleGraph(profile.CreateModules()).GetOrderedModules();
                }
                catch (Exception exception)
                {
                    Add(issues, "error", path, exception.Message);
                }
            }
        }

        private static void Add(List<ValidationIssue> issues, string severity, string path, string message)
        {
            issues.Add(new ValidationIssue
            {
                severity = severity,
                path = path.Replace('\\', '/'),
                message = message
            });
        }
    }
}
