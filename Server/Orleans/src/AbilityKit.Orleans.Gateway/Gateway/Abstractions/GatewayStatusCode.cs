namespace AbilityKit.Orleans.Gateway;

/// <summary>
/// Gateway 状态码
/// </summary>
public static class GatewayStatusCode
{
    public const int Success = 0;
    public const int BadRequest = 400;
    public const int Unauthorized = 401;
    public const int Forbidden = 403;
    public const int NotFound = 404;
    public const int Timeout = 408;
    public const int Conflict = 409;
    public const int SettledFrameVersionMismatch = 426;
    public const int InternalError = 500;
    public const int UnhandledOpCode = 600;
    public const int Exception = 700;
}
