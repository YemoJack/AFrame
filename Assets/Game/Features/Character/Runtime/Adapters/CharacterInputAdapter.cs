using AFrame.Foundation;
using AFrame.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AFrame.Game.Character
{
    public sealed class CharacterInputAdapter : MonoBehaviour
    {
        [SerializeField] private AFrameBootstrap bootstrap;

        public void SetBootstrap(AFrameBootstrap value)
        {
            bootstrap = value;
        }

        private void Update()
        {
            if (bootstrap == null || !bootstrap.IsRunning || Keyboard.current == null)
                return;

            var direction = Float3.Zero;
            var keyboard = Keyboard.current;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                direction.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                direction.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                direction.z -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                direction.z += 1f;

            if (direction.Equals(Float3.Zero))
                return;

            bootstrap.Context.Transport.Send(new MoveCharacterCommand(direction));
        }
    }
}
