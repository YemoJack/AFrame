using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AFrame.Data;
using UnityEditor;
using UnityEngine;

namespace AFrame.Editor
{
    public static class AFrameDefinitionImporter
    {
        public const string SourceRoot = "Assets/Game";
        public const string GeneratedRoot = "Assets/Game/Generated/Definitions";

        public static int ImportAll(bool logResult = true)
        {
            if (!Directory.Exists(SourceRoot))
            {
                if (logResult)
                    Debug.Log("AFrame definition import skipped because Assets/Game does not exist.");
                return 0;
            }

            Directory.CreateDirectory(GeneratedRoot);
            var imported = 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (var sourcePath in Directory.GetFiles(SourceRoot, "*.aframe.json", SearchOption.AllDirectories))
            {
                var normalizedPath = sourcePath.Replace('\\', '/');
                if (normalizedPath.IndexOf("/Generated/", StringComparison.Ordinal) >= 0)
                    continue;

                if (!DefinitionSourceReader.TryRead(
                        normalizedPath,
                        out var document,
                        out var json,
                        out var error))
                {
                    Debug.LogError($"AFrame definition import failed for {normalizedPath}: {error}");
                    continue;
                }

                if (!ids.Add(document.id))
                {
                    Debug.LogError($"AFrame definition id is duplicated in this import: {document.id}");
                    continue;
                }

                var safeId = SanitizeAssetName(document.id);
                var outputPath = $"{GeneratedRoot}/{safeId}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<AFrameDefinitionAsset>(outputPath);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<AFrameDefinitionAsset>();
                    AssetDatabase.CreateAsset(asset, outputPath);
                }

                asset.SetSource(
                    document,
                    json,
                    normalizedPath,
                    ComputeHash(json));
                EditorUtility.SetDirty(asset);
                imported++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (logResult)
                Debug.Log($"AFrame imported {imported} definition source(s).");

            return imported;
        }

        private static string SanitizeAssetName(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
                builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_');
            return builder.Length == 0 ? "Definition" : builder.ToString();
        }

        private static string ComputeHash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
