namespace AFrame.Runtime
{
    public interface ISaveStore
    {
        void Save<T>(string id, T value);
        bool TryLoad<T>(string id, out T value);
    }

    public interface ISnapshotParticipant<T>
    {
        T Capture();
        void Validate(T snapshot);
        void Restore(T snapshot);
    }
}
