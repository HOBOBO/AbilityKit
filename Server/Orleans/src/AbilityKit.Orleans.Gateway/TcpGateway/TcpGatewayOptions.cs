namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class TcpGatewayOptions
{
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 4000;
    public int MaxFrameLength { get; set; } = 1024 * 1024;

    public uint HelloOpCode { get; set; } = 1;

    public int RequestTimeoutMs { get; set; } = 5000;
}
