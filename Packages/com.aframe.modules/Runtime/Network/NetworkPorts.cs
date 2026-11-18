using AFrame.Runtime;

namespace AFrame.Network
{
    public enum NetworkRole
    {
        Offline,
        Client,
        Server,
        Host
    }

    public interface INetworkSession
    {
        NetworkRole Role { get; }
        bool IsConnected { get; }
        void Send(ICommand command);
    }
}
