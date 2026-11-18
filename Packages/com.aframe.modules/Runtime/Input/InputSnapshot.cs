using AFrame.Foundation;

namespace AFrame.Input
{
    public readonly struct InputSnapshot
    {
        public InputSnapshot(Float3 movement, bool primaryPressed)
        {
            Movement = movement;
            PrimaryPressed = primaryPressed;
        }

        public Float3 Movement { get; }
        public bool PrimaryPressed { get; }
    }

    public interface IInputSource
    {
        InputSnapshot Read();
    }
}
