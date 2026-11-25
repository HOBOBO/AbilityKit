using AbilityKit.Network.Runtime;

namespace AbilityKit.Network.Abstractions
{
    /// <summary>
    /// 公开框架数据包路由器的可选连接能力。
    /// </summary>
    public interface IProtocolRoutedConnection
    {
        NetworkPacketRouter PacketRouter { get; }
    }

    /// <summary>
    /// 公开只读诊断快照的可选连接能力。
    /// </summary>
    public interface INetworkConnectionDiagnosticsSource
    {
        NetworkConnectionDiagnosticsSnapshot GetDiagnosticsSnapshot();
    }
}
