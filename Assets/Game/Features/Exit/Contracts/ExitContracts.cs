using AFrame.Foundation;
using AFrame.Runtime;

namespace AFrame.Game.Exit
{
    [StableId("exit.reach_station_exit")]
    public readonly struct ReachStationExit : ICommand { }

    [StableId("exit.station_cleared")]
    public readonly struct StationCleared : IDomainEvent { }

    [StableId("exit.station_timed_out")]
    public readonly struct StationTimedOut : IDomainEvent { }

    public readonly struct StationProgress
    {
        public StationProgress(double secondsRemaining, bool cleared, bool timedOut)
        {
            SecondsRemaining = secondsRemaining;
            Cleared = cleared;
            TimedOut = timedOut;
        }

        public double SecondsRemaining { get; }
        public bool Cleared { get; }
        public bool TimedOut { get; }
    }

    public interface IStationExitQuery : IQuery
    {
        bool IsUnlocked { get; }
        bool IsCleared { get; }
        bool HasTimedOut { get; }
        double SecondsRemaining { get; }
    }
}
