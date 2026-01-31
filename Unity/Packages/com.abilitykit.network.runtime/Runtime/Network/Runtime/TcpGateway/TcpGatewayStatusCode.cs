namespace AbilityKit.Network.Runtime.TcpGateway
{
    public enum TcpGatewayStatusCode : int
    {
        Ok = 0,
        UnhandledOpCode = 1,
        Timeout = 2,
        Exception = 3,
        BadRequest = 4
    }
}
