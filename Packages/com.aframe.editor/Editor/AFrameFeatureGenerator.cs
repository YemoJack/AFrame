using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AFrame.Editor
{
    public static class AFrameFeatureGenerator
    {
        public static string Generate(string typeName)
        {
            if (!Regex.IsMatch(typeName ?? "", @"\A[A-Z][A-Za-z0-9]*\z"))
                throw new ArgumentException("Use a PascalCase feature name, for example QuestLog.");
            var prefix = $"AFrame.Game.{typeName}";
            var id = Regex.Replace(typeName, "(?<!^)[A-Z]", "-$0").ToLowerInvariant();
            var feature = $"Assets/Game/Features/{typeName}";
            var composition = $"Assets/Game/Composition/{typeName}";
            if (Directory.Exists(feature) || Directory.Exists(composition))
                throw new IOException($"A feature or composition named {typeName} already exists.");

            WriteAssembly($"{feature}/Contracts", prefix + ".Contracts", true, "AFrame.Foundation", "AFrame.Runtime");
            WriteAssembly($"{feature}/Core", prefix + ".Core", true, "AFrame.Foundation", "AFrame.Runtime", prefix + ".Contracts");
            WriteAssembly($"{feature}/Unity", prefix + ".Unity", false, "AFrame.Foundation", "AFrame.Runtime", "AFrame.Unity", prefix + ".Contracts");
            WriteAssembly(composition, prefix + ".Composition", false, "AFrame.Foundation", "AFrame.Runtime", "AFrame.Unity", prefix + ".Core");
            AFrameContextBuilder.WriteJson($"{feature}/feature.json", new
            {
                id, version = "0.1.0", physicsAuthority = "none",
                supports = new { dimensions = new[] { "2d", "3d" }, network = new[] { "offline" } }
            });
            Write($"{feature}/Contracts/{typeName}Contracts.cs", $@"using AFrame.Foundation;
using AFrame.Runtime;

namespace {prefix}
{{
    [StableId(""{id}.increment"")]
    public readonly struct Increment : ICommand {{ }}

    public interface ICountQuery : IQuery
    {{
        int Count {{ get; }}
    }}
}}
");
            Write($"{feature}/Core/{typeName}Module.cs", $@"using System;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;

namespace {prefix}
{{
    public sealed class {typeName}Module : IGameModule
    {{
        private sealed class Counter : ICountQuery, ICommandHandler<Increment>
        {{
            public int Count {{ get; private set; }}
            public Result Handle(in CommandEnvelope envelope, in Increment command)
            {{
                Count++;
                return Result.Ok();
            }}
        }}

        public ModuleId Id => new ModuleId(""game.{id}"");
        public ModuleScope Scope => ModuleScope.Session;
        public IReadOnlyList<ModuleId> Dependencies => Array.Empty<ModuleId>();
        public void Configure(IModuleBuilder builder)
        {{
            builder.Provide<Counter>(_ => new Counter());
            builder.Provide<ICountQuery>(services => services.Get<Counter>());
            builder.Handle<Increment>(services => services.Get<Counter>());
        }}

        public void Start(ModuleContext context) {{ }}
        public void Stop() {{ }}
    }}
}}
");
            Write($"{feature}/Unity/{typeName}View.cs", $@"using AFrame.Foundation;
using AFrame.Unity;
using UnityEngine;

namespace {prefix}
{{
    public sealed class {typeName}View : AFrameView
    {{
        private IViewContext view;
        private ICountQuery counter;
        public override void Bind(IViewContext context)
        {{
            view = context;
            counter = context.Query<ICountQuery>();
        }}

        public override void Unbind() => view = null;

        private void OnGUI()
        {{
            if (view != null && GUILayout.Button($""Count {{counter.Count}}""))
                view.Commands.Submit(PlayerId.None, new Increment());
        }}
    }}
}}
");
            Write($"{composition}/{typeName}ModuleAsset.cs", $@"using System;
using System.Collections.Generic;
using AFrame.Foundation;
using AFrame.Runtime;
using AFrame.Unity;
using UnityEngine;

namespace {prefix}
{{
    [CreateAssetMenu(menuName = ""AFrame/Modules/{typeName}"")]
    public sealed class {typeName}ModuleAsset : AFrameModuleAsset
    {{
        private void OnValidate() => EnsureModuleId(""game.{id}"");
        public override IReadOnlyList<ModuleId> Dependencies => Array.Empty<ModuleId>();
        public override IGameModule CreateModule() => new {typeName}Module();
    }}
}}
");
            Write($"{feature}/README.md", $"# {typeName}\n\n创建 **AFrame/Modules/{typeName}** 资产并加入默认 Profile。"
                + $"在带有 SceneScope 的场景根下挂上 `{typeName}View`。进入 Play Mode，点击按钮确认计数增加。\n");
            AssetDatabase.Refresh();
            return feature;
        }

        private static void Write(string path, string value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var file = new FileStream(path, FileMode.CreateNew);
            using var writer = new StreamWriter(file);
            writer.Write(value);
        }

        private static void WriteAssembly(string folder, string name, bool pure, params string[] references) =>
            AFrameContextBuilder.WriteJson($"{folder}/{name}.asmdef", new { name, references, noEngineReferences = pure });

        [MenuItem("AFrame/Generate/Feature")]
        private static void Open()
        {
            var window = ScriptableObject.CreateInstance<FeatureWindow>();
            window.titleContent = new GUIContent("Generate AFrame Feature");
            window.ShowUtility();
        }

        private sealed class FeatureWindow : EditorWindow
        {
            private string featureName = "NewFeature";
            private void OnGUI()
            {
                featureName = EditorGUILayout.TextField("Feature", featureName);
                if (!GUILayout.Button("Generate"))
                    return;
                try { Generate(featureName); Close(); }
                catch (Exception error) { EditorUtility.DisplayDialog("AFrame", error.Message, "OK"); }
            }
        }
    }
}
