using System;
using System.IO;
using System.Linq;
using AFrame.Data;
using UnityEditor;
using UnityEngine;

namespace AFrame.Editor
{
    public static class AFrameFeatureGenerator
    {
        public static string Generate(string featureName)
        {
            var typeName = SanitizeTypeName(featureName);
            var root = $"Assets/Game/Features/{typeName}";
            var runtimeRoot = $"{root}/Runtime";

            Directory.CreateDirectory($"{runtimeRoot}/Domain");
            Directory.CreateDirectory($"{runtimeRoot}/Application");
            Directory.CreateDirectory($"{runtimeRoot}/Adapters");
            Directory.CreateDirectory($"{runtimeRoot}/Contracts");
            Directory.CreateDirectory($"{root}/Editor");
            Directory.CreateDirectory($"{root}/Definitions");
            Directory.CreateDirectory($"{root}/Generated");
            Directory.CreateDirectory($"{root}/Docs");

            WriteIfMissing(
                $"{runtimeRoot}/AFrame.Game.{typeName}.Runtime.asmdef",
                $"{{\n  \"name\": \"AFrame.Game.{typeName}.Runtime\",\n  \"rootNamespace\": \"AFrame.Game.{typeName}\",\n  \"references\": [\"AFrame.Foundation\", \"AFrame.Runtime\", \"AFrame.Data\"],\n  \"includePlatforms\": [],\n  \"excludePlatforms\": [],\n  \"allowUnsafeCode\": false,\n  \"overrideReferences\": false,\n  \"precompiledReferences\": [],\n  \"autoReferenced\": true,\n  \"defineConstraints\": [],\n  \"versionDefines\": [],\n  \"noEngineReferences\": true\n}}\n");

            var manifest = new FeatureManifest
            {
                id = $"game.{ToKebabCase(typeName)}",
                version = "0.1.0",
                entryPoint = $"AFrame.Game.{typeName}.{typeName}Module",
                sourceRoots = new[] { "Runtime", "Definitions" },
                definitionSources = new[] { "Definitions/**/*.aframe.json" },
                acceptanceChecks = new[] { "compile", "import-definitions", "smoke" }
            };

            WriteIfMissing(
                $"{root}/feature.manifest.json",
                JsonUtility.ToJson(manifest, true) + Environment.NewLine);

            WriteIfMissing(
                $"{runtimeRoot}/Application/{typeName}Module.cs",
                $"using System;\nusing System.Collections.Generic;\nusing AFrame.Foundation;\nusing AFrame.Runtime;\n\nnamespace AFrame.Game.{typeName}\n{{\n    public sealed class {typeName}Module : IGameModule\n    {{\n        private static readonly IReadOnlyList<ModuleId> NoDependencies = Array.Empty<ModuleId>();\n\n        public ModuleId Id => new ModuleId(\"game.{ToKebabCase(typeName)}\");\n        public IReadOnlyList<ModuleId> Dependencies => NoDependencies;\n\n        public void Configure(IModuleBuilder builder) {{ }}\n        public void Start(ModuleContext context) {{ }}\n        public void Stop() {{ }}\n    }}\n}}\n");

            WriteIfMissing(
                $"{root}/Docs/{typeName}.md",
                $"# {typeName}\n\nThis feature owns its domain rules, application services and adapters.\n\n## Entry point\n\n`AFrame.Game.{typeName}.{typeName}Module`\n\n## Acceptance checks\n\n- Project compiles.\n- Definitions import without validation errors.\n- The feature smoke scenario runs.\n");

            AssetDatabase.Refresh();
            Debug.Log($"AFrame feature generated at {root}.");
            return root;
        }

        private static string SanitizeTypeName(string value)
        {
            var parts = (value ?? string.Empty)
                .Split(new[] { ' ', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1))
                .ToArray();
            var result = string.Concat(parts.Where(characters => characters.All(char.IsLetterOrDigit)));
            if (string.IsNullOrEmpty(result))
                result = "NewFeature";
            if (!char.IsLetter(result[0]))
                result = "Feature" + result;
            return result;
        }

        private static string ToKebabCase(string value)
        {
            return string.Concat(value.Select((character, index) =>
                index > 0 && char.IsUpper(character) ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));
        }

        private static void WriteIfMissing(string path, string content)
        {
            if (File.Exists(path))
                return;
            File.WriteAllText(path, content);
        }
    }

    public sealed class AFrameFeatureWindow : EditorWindow
    {
        private string featureName = "NewFeature";

        [MenuItem("AFrame/Generate/Feature")]
        private static void Open()
        {
            GetWindow<AFrameFeatureWindow>("AFrame Feature");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Feature name", EditorStyles.boldLabel);
            featureName = EditorGUILayout.TextField(featureName);
            EditorGUILayout.Space(8f);

            if (GUILayout.Button("Generate"))
            {
                AFrameFeatureGenerator.Generate(featureName);
                Close();
            }
        }
    }
}
