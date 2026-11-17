using AFrame.Foundation;
using AFrame.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AFrame.Game.Character
{
    [DisallowMultipleComponent]
    public sealed class CharacterInputAdapter : MonoBehaviour, ITickInput, IInputSource<CharacterInput>
    {
        private CharacterInput sampled;
        private CharacterInput frozen;

        public void Sample(bool sprintEnabled = true)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !isActiveAndEnabled)
            {
                sampled = default;
                return;
            }

            var x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            var z = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            var direction = new Vector3(x, 0, z).normalized;
            sampled = new CharacterInput(new Float3(direction.x, 0, direction.z),
                sprintEnabled && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed));
        }

        public void Freeze() => frozen = sampled;
        public CharacterInput Get(PlayerId player) => frozen;
        private void OnDisable() => sampled = default;
    }
}
