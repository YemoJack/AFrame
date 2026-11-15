using AFrame.Foundation;
using AFrame.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AFrame.Game.Character
{
    public sealed class CharacterInputAdapter : MonoBehaviour
    {
        [SerializeField] private AFrameBootstrap bootstrap;
        [SerializeField] private bool sprintEnabled;
        private Float3 lastDirection;
        private bool lastSprinting;

        public void EnableSprint() => sprintEnabled = true;

        public void SetBootstrap(AFrameBootstrap value)
        {
            bootstrap = value;
        }

        private void Update()
        {
            if (bootstrap == null || !bootstrap.IsRunning)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                Submit(Float3.Zero);
                return;
            }

            var direction = Vector3.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                direction.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                direction.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                direction.z -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                direction.z += 1f;

            direction = direction.normalized;
            var sprinting = sprintEnabled && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            Submit(new Float3(direction.x, 0f, direction.z), sprinting);
        }

        private void OnDisable() => Submit(Float3.Zero);

        private void Submit(Float3 direction, bool sprinting = false)
        {
            if (direction.Equals(lastDirection) && sprinting == lastSprinting)
                return;

            lastDirection = direction;
            lastSprinting = sprinting;
            if (bootstrap != null && bootstrap.IsRunning)
                bootstrap.Context.Transport.Send(new SetCharacterMovement(direction, sprinting));
        }
    }
}
