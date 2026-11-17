using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AFrame.Data;
using AFrame.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AFrame.Editor
{
    public static class AFrameDefinitionImporter
    {
        public const string GeneratorVersion = "2.4.0";
        public const string GeneratedRoot = "Assets/Game/Generated/Definitions";
        internal const string RegistryPath = GeneratedRoot + "/DefinitionRegistry.asset";

        internal sealed class Source
        {
            public string Path;
            public DefinitionDocument Header;
            public string Json;
            public string RawJson;
            public Type Model;
            public (string Id, string Type)[] References;
            public string Hash => DefinitionSourceReader.ComputeHash(RawJson);
            public string Output => $"{GeneratedRoot}/{Header.id}.asset";

            public bool Matches(AFrameDefinitionAsset asset) =>
                asset != null && asset.DefinitionId == Header.id && asset.DefinitionType == Header.type
                && asset.SchemaVersion == Header.schemaVersion && asset.SourceJson == Json
                && asset.SourceHash == Hash && asset.SourcePath == Path && asset.GeneratorVersion == GeneratorVersion;
        }

        internal static List<Source> ReadSources(Action<string, string> issue)
        {
            var models = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var type in TypeCache.GetTypesDerivedFrom<IDefinition>().Where(type => !type.IsAbstract && !type.IsInterface))
            {
                var tag = type.GetCustomAttribute<DefinitionTypeAttribute>();
                if (tag == null || !models.TryAdd(tag.Id, type))
                    issue(type.FullName, "Definition type must have a unique DefinitionType ID.");
            }

            var migrations = new Dictionary<(string, int), IDefinitionMigration>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IDefinitionMigration>().Where(type => !type.IsAbstract && !type.IsInterface))
            {
                try
                {
                    var migration = (IDefinitionMigration)Activator.CreateInstance(type);
                    if (string.IsNullOrWhiteSpace(migration.DefinitionType) || migration.FromVersion < 0
                        || migration.FromVersion >= DefinitionSourceReader.CurrentVersion
                        || !migrations.TryAdd((migration.DefinitionType, migration.FromVersion), migration))
                        throw new InvalidOperationException("Invalid or duplicate definition migration.");
                }
                catch (Exception error) { issue(type.FullName, error.Message); }
            }

            var ids = new Dictionary<string, Source>(StringComparer.Ordinal);
            var sources = new List<Source>();
            if (!Directory.Exists("Assets/Game/Features"))
                return sources;
            foreach (var path in Directory.GetFiles("Assets/Game/Features", "*.aframe.json", SearchOption.AllDirectories)
                         .Select(path => path.Replace('\\', '/')).Where(path => path.Contains("/Definitions/"))
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                try
                {
                    if (!DefinitionSourceReader.TryRead(path, out var header, out var json, out var error))
                        throw new InvalidDataException(error);
                    if (!models.TryGetValue(header.type, out var model))
                        throw new InvalidDataException($"Unknown definition type {header.type}.");
                    var data = DefinitionSourceReader.ReadObject(json);
                    var version = header.schemaVersion;
                    while (version < DefinitionSourceReader.CurrentVersion)
                    {
                        if (!migrations.TryGetValue((header.type, version), out var migration))
                            throw new InvalidDataException($"No {header.type} migration from schemaVersion {version}.");
                        migration.Migrate(data);
                        if ((string)data["id"] != header.id || (string)data["type"] != header.type)
                            throw new InvalidDataException("A migration cannot change a definition ID or type.");
                        data["schemaVersion"] = ++version;
                    }

                    var normalized = data.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
                    DefinitionSourceReader.ReadDefinition(normalized, model);
                    var source = new Source
                    {
                        Path = path, Header = DefinitionSourceReader.ReadHeader(data), Json = normalized,
                        RawJson = json, Model = model, References = References(data["references"])
                    };
                    if (!ids.TryAdd(header.id, source))
                        throw new InvalidDataException($"Duplicate definition ID {header.id}.");
                    sources.Add(source);
                }
                catch (Exception error)
                {
                    issue(path, error.Message);
                }
            }
            foreach (var source in sources)
                foreach (var reference in source.References)
                    if (!ids.TryGetValue(reference.Id, out var target) || target.Header.type != reference.Type)
                        issue(source.Path, $"Reference '{reference.Id}' does not resolve to a '{reference.Type}' definition.");
            return sources;
        }

        private static (string Id, string Type)[] References(JToken token)
        {
            if (token == null)
                return Array.Empty<(string, string)>();
            if (!(token is JArray entries))
                throw new InvalidDataException("references must be an array of { id, type } objects.");
            var seen = new HashSet<(string, string)>();
            foreach (var value in entries)
            {
                if (!(value is JObject entry) || entry.Count != 2 || entry["id"]?.Type != JTokenType.String
                    || entry["type"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)entry["id"])
                    || string.IsNullOrWhiteSpace((string)entry["type"]) || !seen.Add(((string)entry["id"], (string)entry["type"])))
                    throw new InvalidDataException("Each definition reference requires a unique { id, type } pair.");
            }
            return seen.OrderBy(entry => entry.Item1, StringComparer.Ordinal)
                .ThenBy(entry => entry.Item2, StringComparer.Ordinal).ToArray();
        }

        public static int ImportAll(bool logResult = true)
        {
            var errors = new List<string>();
            var sources = ReadSources((path, error) => errors.Add($"{path}: {error}"));
            var code = AFrameDefinitionCode.Render(sources, (path, error) => errors.Add($"{path}: {error}"));
            if (errors.Count > 0)
                throw new InvalidDataException(string.Join("\n", errors));
            if (sources.Count == 0 && !Directory.Exists(GeneratedRoot))
            {
                AFrameDefinitionCode.Publish(code);
                return 0;
            }
            Directory.CreateDirectory(GeneratedRoot);
            AssetDatabase.Refresh();

            var registryObject = AssetDatabase.LoadMainAssetAtPath(RegistryPath);
            if (registryObject != null && !(registryObject is DefinitionRegistry))
                throw new InvalidDataException($"{RegistryPath} already contains an unrelated asset.");
            foreach (var source in sources)
            {
                var existing = AssetDatabase.LoadMainAssetAtPath(source.Output);
                if (existing != null && !(existing is AFrameDefinitionAsset))
                    throw new InvalidDataException($"{source.Output} already contains an unrelated asset.");
            }

            var definitions = new List<AFrameDefinitionAsset>();
            foreach (var source in sources)
            {
                var asset = AssetDatabase.LoadAssetAtPath<AFrameDefinitionAsset>(source.Output);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<AFrameDefinitionAsset>();
                    AssetDatabase.CreateAsset(asset, source.Output);
                }
                if (!source.Matches(asset))
                {
                    asset.SetSource(source.Header, source.Json, source.Path, source.Hash, GeneratorVersion);
                    EditorUtility.SetDirty(asset);
                }
                definitions.Add(asset);
            }

            var registry = AssetDatabase.LoadAssetAtPath<DefinitionRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<DefinitionRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }
            if (!registry.Definitions.SequenceEqual(definitions))
            {
                registry.SetDefinitions(definitions);
                EditorUtility.SetDirty(registry);
            }
            var profile = Resources.Load<AFrameGameProfile>(AFrameBootstrap.ProfileResource);
            if (profile != null && profile.Definitions == null)
            {
                profile.SetDefinitions(registry);
                EditorUtility.SetDirty(profile);
            }

            var expected = new HashSet<string>(sources.Select(source => source.Output), StringComparer.Ordinal);
            foreach (var obsolete in AssetDatabase.FindAssets("t:AFrameDefinitionAsset", new[] { GeneratedRoot })
                         .Select(AssetDatabase.GUIDToAssetPath).Where(path => !expected.Contains(path)))
                AssetDatabase.DeleteAsset(obsolete);
            AFrameDefinitionCode.Publish(code);
            AssetDatabase.SaveAssets();
            if (logResult)
                Debug.Log($"AFrame imported {sources.Count} definition source(s).");
            return sources.Count;
        }
    }
}
