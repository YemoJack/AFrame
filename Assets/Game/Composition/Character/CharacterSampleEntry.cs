using System;
using System.Collections.Generic;
using System.Linq;
using AFrame.Foundation;
using AFrame.Runtime;
using AFrame.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AFrame.Game.Character
{
    [RequireComponent(typeof(CharacterInputAdapter), typeof(SceneScope))]
    public sealed class CharacterSampleEntry : AFrameSceneEntry, IAFrameSmokeScenario
    {
        private const string Scene2D = "Assets/Game/Scenes/Character2D.unity";
        private const string Scene3D = "Assets/Game/Scenes/AFrameSampleScene.unity";

        [SerializeField] private GameDimension dimension;
        private CharacterInputAdapter input;
        private long smokeTick;

        public override IReadOnlyList<string> Features => new[] { "character" };
        public override GameDimension? DimensionOverride => dimension;

        public override void Enter(AFrameBootstrap bootstrap)
        {
            input = GetComponent<CharacterInputAdapter>();
            var modules = new List<IGameModule> { new CharacterInputModule(input), new CharacterModule("character.default") };
            if (dimension == GameDimension.TwoD)
                modules.Add(new SampleViewModule());
            bootstrap.BeginSession(modules);
        }

        public override void SampleInput() => input.Sample();

        private bool CanSwitch => Host != null && Host.IsRunning;

        private void Update()
        {
            if (CanSwitch && Keyboard.current?.tabKey.wasPressedThisFrame == true)
                SwitchView();
        }

        private void OnGUI()
        {
            if (CanSwitch && GUILayout.Button(dimension == GameDimension.TwoD ? "切换到 3D [Tab]" : "切换到 2D [Tab]"))
                SwitchView();
        }

        private void SwitchView() =>
            SceneManager.LoadScene(dimension == GameDimension.TwoD ? Scene3D : Scene2D);

        public void BeginSmoke()
        {
            smokeTick = Host.Context.Tick;
            Host.Context.Get<ICommandSink>().Submit(PlayerId.None, new MoveCharacterCommand(new Float3(0.6f, 0f, 0.8f)));
        }

        public bool SmokeComplete => Host.Context != null && Host.Context.Tick >= smokeTick + 4;

        public void VerifySmoke()
        {
            var view = gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CharacterView>()).Single();
            var position = Host.Context.Get<ICharacterState>().Position;
            var expected = dimension == GameDimension.TwoD
                ? new Vector3(position.x, position.z, 0f) : new Vector3(position.x, 0f, position.z);
            if (position.x <= 0 || position.z <= 0 || (view.transform.position - expected).sqrMagnitude > 0.0001f)
                throw new InvalidOperationException($"Character did not reach the expected {dimension} view position.");
        }

        private sealed class SampleViewModule : IGameModule
        {
            public ModuleId Id => new ModuleId("game.character-sample-view");
            public ModuleScope Scope => ModuleScope.Session;
            public IReadOnlyList<ModuleId> Dependencies => new[] { new ModuleId("game.character") };
            public void Configure(IModuleBuilder builder) => builder.Require<IEventSink>();
            public void Start(ModuleContext context) => context.Get<IEventSink>().Raise(
                new EntitySpawned(new AFrame.Foundation.EntityId(1), new ViewKey("character.sample.2d")));
            public void Stop() { }
        }
    }
}
