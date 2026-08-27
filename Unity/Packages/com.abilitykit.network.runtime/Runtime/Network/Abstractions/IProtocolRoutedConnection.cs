using AbilityKit.Network.Runtime;

namespace AbilityKit.Network.Abstractions
{
    /// <summary>Optional connection capability exposing the framework packet router.</summary>
    public interface IProtocolRoutedConnection
    {
        NetworkPacketRouter PacketRouter { get; }
    }

    /// <summary>Optional connection capability exposing a read-only diagnostics snapshot.</summary>
    public interface INetworkConnectionDiagnosticsSource
    {
        NetworkConnectionDiagnosticsSnapshot GetDiagnosticsSnapshot();
    }
}
