namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class TcpGatewayOptions
{
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 4000;
    public int MaxFrameLength { get; set; } = 1024 * 1024;

    public uint HelloOpCode { get; set; } = 1;

    public uint GuestLoginOpCode { get; set; } = 100;
    public uint CreateRoomOpCode { get; set; } = 110;
    public uint JoinRoomOpCode { get; set; } = 111;
    public uint LeaveRoomOpCode { get; set; } = 112;
    public uint ListRoomsOpCode { get; set; } = 113;

    public uint CloseRoomOpCode { get; set; } = 114;

    public uint RenewSessionOpCode { get; set; } = 120;
    public uint LogoutOpCode { get; set; } = 121;

    public uint CreateSessionForAccountOpCode { get; set; } = 122;

    public uint TimeSyncOpCode { get; set; } = 1300;

    public double FixedDeltaSeconds { get; set; } = 0.03333333333333333;

    public uint SubmitFrameInputOpCode { get; set; } = 2001;
    public uint FramePushedOpCode { get; set; } = 9001;

    public uint KickPushOpCode { get; set; } = 9000;

    public int RequestTimeoutMs { get; set; } = 5000;
}
