using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AFrame.Data;
using AFrame.Foundation;
using AFrame.Runtime;
using AFrame.Unity;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace AFrame.Editor
{
    public static class AFrameArchitectureValidator
    {
        public static ValidationReport Run(bool writeReport = true, bool checkContext = true)
        {
            var issues = new List<ValidationIssue>();
            void Add(string source, string path, string message) =>
                issues.Add(new ValidationIssue { source = source, severity = "error", path = path, scope = path, message = message });
            var features = Directory.Exists("Assets/Game/Features")
                ? Directory.GetDirectories("Assets/Game/Features").OrderBy(path => path, StringComparer.Ordinal)
                : Enumerable.Empty<string>();
            var manifests = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (var feature in features)
            {
                var path = feature + "/feature.json";
                try
                {
                    var json = JObject.Parse(File.ReadAllText(path));
                    var id = (string)json["id"];
                    if (string.IsNullOrWhiteSpace(id) || !manifests.TryAdd(id, json))
                        Add("manifest", path, "Feature ID must be non-empty and unique.");
                }
                catch (Exception error) { Add("manifest", path, error.Message); }
            }

            foreach (var assembly in CompilationPipeline.GetAssemblies())
            {
                var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                if (!IsAFrameAssembly(path))
                    continue;
                var name = assembly.name;
                var feature = path.StartsWith("Assets/Game/Features/", StringComparison.Ordinal);
                var core = name.EndsWith(".Core", StringComparison.Ordinal);
                var contracts = name.EndsWith(".Contracts", StringComparison.Ordinal);
                bool pure = core || contracts || name == "AFrame.Foundation" || name == "AFrame.Runtime" || name == "AFrame.Data";
                if (pure && (bool?)JObject.Parse(File.ReadAllText(path))["noEngineReferences"] != true)
                    Add("asmdef", path, "Domain assemblies require noEngineReferences.");
                foreach (var reference in assembly.assemblyReferences)
                {
                    var target = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(reference.name);
                    if (pure && !AllowedPureReference(reference.name, core, contracts))
                        Add("asmdef", path, $"Domain assembly cannot reference {reference.name}.");
                    if (target?.StartsWith("Assets/Game/Features/", StringComparison.Ordinal) != true)
                        continue;
                    if (reference.name.EndsWith(".Core", StringComparison.Ordinal)
                        && !path.StartsWith("Assets/Game/Composition/", StringComparison.Ordinal)
                        && !name.EndsWith(".Tests", StringComparison.Ordinal))
                        Add("asmdef", path, $"Only Composition and Tests can reference {reference.name}.");
                    if (feature && !reference.name.EndsWith(".Contracts", StringComparison.Ordinal)
                        && path.Split('/')[3] != target.Split('/')[3])
                        Add("asmdef", path, $"Cross-feature dependency must target Contracts: {reference.name}.");
                }
            }

            var stableIds = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var type in TypeCache.GetTypesDerivedFrom<ICommand>().Concat(TypeCache.GetTypesDerivedFrom<IDomainEvent>()))
            {
                var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(type.Assembly.GetName().Name);
                if (!IsAFrameAssembly(path) || type.IsInterface || type.IsAbstract)
                    continue;
                var id = type.GetCustomAttribute<StableIdAttribute>()?.Id;
                var readOnly = type.CustomAttributes.Any(attribute =>
                    attribute.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
                if (!type.IsValueType || !readOnly || string.IsNullOrWhiteSpace(id))
                    Add("reflection", path, $"{type.FullName} must be a readonly struct with a StableId.");
                else if (!stableIds.TryAdd(id, type))
                    Add("reflection", path, $"{type.FullName} duplicates StableId {id} from {stableIds[id].FullName}.");
            }

            AFrameRuntimeRules.CheckCompiledCode(Add);

            var sources = AFrameDefinitionImporter.ReadSources((path, error) => Add("definition", path, error));
            var generatedCode = AFrameDefinitionCode.Render(sources, (path, error) => Add("definition", path, error));
            foreach (var stale in AFrameDefinitionCode.Stale(generatedCode))
                Add("hash", stale, "Generated definition code or link.xml is missing, stale or modified. Import definitions again.");
            var expected = new List<AFrameDefinitionAsset>();
            foreach (var source in sources)
            {
                var asset = AssetDatabase.LoadAssetAtPath<AFrameDefinitionAsset>(source.Output);
                if (!source.Matches(asset))
                    Add("hash", source.Output, "Definition is missing, stale or modified. Import definitions again.");
                if (asset != null)
                    expected.Add(asset);
            }
            if (Directory.Exists(AFrameDefinitionImporter.GeneratedRoot))
            {
                var existing = AssetDatabase.FindAssets("t:AFrameDefinitionAsset", new[] { AFrameDefinitionImporter.GeneratedRoot })
                    .Select(AssetDatabase.GUIDToAssetPath);
                foreach (var path in existing.Except(sources.Select(source => source.Output)))
                    Add("hash", path, "Definition has no matching source. Import definitions again.");
            }
            var registry = AssetDatabase.LoadAssetAtPath<DefinitionRegistry>(AFrameDefinitionImporter.RegistryPath);
            if ((sources.Count > 0 || registry != null) && (registry == null || !registry.Definitions.SequenceEqual(expected)))
                Add("hash", AFrameDefinitionImporter.RegistryPath, "Generated registry does not match the source definitions.");

            var profiles = AssetDatabase.FindAssets("t:AFrameGameProfile").Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/Game/", StringComparison.Ordinal)).ToArray();
            if (profiles.Count(path => path.EndsWith("/Resources/AFrameGameProfile.asset", StringComparison.Ordinal)) > 1)
                Add("profile", "Assets/Game", "Multiple default AFrame profiles.");
            AFrameGameProfile defaultProfile = null;
            foreach (var path in profiles)
            {
                var profile = AssetDatabase.LoadAssetAtPath<AFrameGameProfile>(path);
                if (registry != null && path.EndsWith("/Resources/AFrameGameProfile.asset", StringComparison.Ordinal)
                    && profile.Definitions != registry)
                    Add("profile", path, "Default profile must reference the generated definition registry.");
                if (path.EndsWith("/Resources/AFrameGameProfile.asset", StringComparison.Ordinal))
                    defaultProfile = profile;
                try
                {
                    var ordered = new ModuleGraph(profile.CreateModules()).GetOrderedModules();
                    var inferred = ordered.Select(module => module.Id.Value).Where(id => id.StartsWith("game.", StringComparison.Ordinal))
                        .Select(id => id.Substring(5)).Where(manifests.ContainsKey);
                    AFrameRuntimeRules.CheckFeatures(manifests, profile.EnabledFeatures.Concat(inferred),
                        profile.Dimension, profile.NetworkMode, path, Add);
                }
                catch (Exception error) { Add("profile", path, error.Message); }
            }

            AFrameRuntimeRules.CheckAssets(manifests, defaultProfile, Add);

            if (checkContext)
                foreach (var stale in AFrameContextBuilder.StaleFiles())
                    Add("context", stale, "Context is missing, stale or modified. Rebuild project context.");

            var report = new ValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                succeeded = issues.Count == 0,
                issues = issues.OrderBy(issue => issue.path, StringComparer.Ordinal)
                    .ThenBy(issue => issue.message, StringComparer.Ordinal).ToArray(),
                covered = checkContext ? new[] { "manifest", "asmdef", "reflection", "definition", "hash", "profile", "cecil", "scene", "prefab", "context" }
                    : new[] { "manifest", "asmdef", "reflection", "definition", "hash", "profile", "cecil", "scene", "prefab" },
                checkedScopes = new[]
                {
                    "cecil: compiled player assemblies in Assets/Game and Packages/com.aframe.*",
                    "scene/prefab: scene and prefab assets under Assets",
                    "profile: Assets/Game profiles and scene entries",
                    "hash: AFrame generated definitions and code"
                },
                notCovered = new[] { "IL2CPP player build", "global ErrorCode registry" }
            };
            if (writeReport)
                AFrameContextBuilder.WriteJson("Logs/AFrame/validate.json", report);
            return report;
        }

        internal static bool IsAFrameAssembly(string path) => path != null
            && (path.StartsWith("Packages/com.aframe.", StringComparison.Ordinal)
                || path.StartsWith("Assets/Game/", StringComparison.Ordinal));

        private static bool AllowedPureReference(string name, bool core, bool contracts) =>
            name == "AFrame.Foundation" || (core || contracts) && (name == "AFrame.Runtime"
                || name == "AFrame.Data" || name.EndsWith(".Contracts", StringComparison.Ordinal));
    }
}
