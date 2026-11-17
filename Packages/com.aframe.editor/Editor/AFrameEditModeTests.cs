using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace AFrame.Editor
{
    internal static class AFrameEditModeTests
    {
        public static void Run(string feature, string selector, Action<bool, string> completed)
        {
            var testAssemblies = CompilationPipeline.GetAssemblies()
                .Where(assembly => assembly.name.EndsWith(".Tests", StringComparison.Ordinal))
                .Select(assembly => (name: assembly.name, path: CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name)))
                .Where(assembly => AFrameArchitectureValidator.IsAFrameAssembly(assembly.path)).ToArray();
            string[] names;
            if (string.IsNullOrEmpty(feature))
                names = testAssemblies.Select(assembly => assembly.name).ToArray();
            else if (feature == "runtime")
                names = testAssemblies.Where(assembly => assembly.name == "AFrame.Runtime.Tests")
                    .Select(assembly => assembly.name).ToArray();
            else
            {
                var manifests = Directory.Exists("Assets/Game/Features")
                    ? Directory.GetFiles("Assets/Game/Features", "feature.json", SearchOption.AllDirectories)
                    : Array.Empty<string>();
                var manifest = manifests.SingleOrDefault(path => (string)JObject.Parse(File.ReadAllText(path))["id"] == feature);
                if (manifest == null)
                    throw new ArgumentException($"Unknown AFrame feature: {feature}.");
                var root = Path.GetDirectoryName(manifest).Replace('\\', '/') + "/";
                names = testAssemblies.Where(assembly => assembly.path.StartsWith(root, StringComparison.Ordinal))
                    .Select(assembly => assembly.name).ToArray();
            }
            if (names.Length == 0)
                throw new InvalidOperationException("No AFrame EditMode test assemblies match the selection.");

            var filter = new Filter { assemblyNames = names, testMode = TestMode.EditMode };
            if (!string.IsNullOrEmpty(selector))
                filter.groupNames = new[] { "^" + Regex.Escape(selector).Replace("\\*", ".*").Replace("\\?", ".") + "$" };
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callback = new Completion(api, completed);
            api.RegisterCallbacks(callback);
            try { api.Execute(new ExecutionSettings(filter)); }
            catch { callback.Dispose(); throw; }
        }

        private sealed class Completion : ICallbacks, IDisposable
        {
            private readonly TestRunnerApi api;
            private readonly Action<bool, string> complete;

            public Completion(TestRunnerApi api, Action<bool, string> complete)
            {
                this.api = api;
                this.complete = complete;
            }

            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                Dispose();
                var passed = result.PassCount;
                var failed = result.FailCount;
                var skipped = result.SkipCount + result.InconclusiveCount;
                var success = passed > 0 && failed == 0 && skipped == 0;
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        Directory.CreateDirectory("Logs/AFrame");
                        TestRunnerApi.SaveResultToFile(result, "Logs/AFrame/test-results.xml");
                        AFrameContextBuilder.WriteJson("Logs/AFrame/test.json", new { passed, failed, skipped, succeeded = success });
                    }
                    catch (Exception error)
                    {
                        complete(false, error.Message);
                        return;
                    }
                    complete(success, success ? "" : $"EditMode: {passed} passed, {failed} failed, {skipped} skipped. See Logs/AFrame/test-results.xml.");
                };
            }

            public void Dispose()
            {
                api.UnregisterCallbacks(this);
                UnityEngine.Object.DestroyImmediate(api);
            }
        }
    }
}
