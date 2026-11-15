namespace AFrame.Foundation
{
    public interface IClock
    {
        double Time { get; }
        double DeltaTime { get; }
    }

    public sealed class SystemClock : IClock
    {
        private readonly System.Diagnostics.Stopwatch stopwatch =
            System.Diagnostics.Stopwatch.StartNew();
        private double lastTime;

        public double Time => stopwatch.Elapsed.TotalSeconds;

        public double DeltaTime
        {
            get
            {
                var currentTime = Time;
                var delta = currentTime - lastTime;
                lastTime = currentTime;
                return delta < 0d ? 0d : delta;
            }
        }
    }

    public interface IRandomSource
    {
        int Next(int minInclusive, int maxExclusive);
        float NextFloat();
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly System.Random random;

        public SystemRandomSource(int seed)
        {
            random = new System.Random(seed);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            return random.Next(minInclusive, maxExclusive);
        }

        public float NextFloat()
        {
            return (float)random.NextDouble();
        }
    }
}
