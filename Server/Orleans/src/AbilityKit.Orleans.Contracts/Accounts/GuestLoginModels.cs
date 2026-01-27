using Orleans.Serialization;

namespace AbilityKit.Orleans.Contracts.Accounts;

[GenerateSerializer]
public sealed record GuestLoginResponse(
    [property: Id(0)] string AccountId,
    [property: Id(1)] string SessionToken,
    [property: Id(2)] long ExpireAtUnixMs);

[GenerateSerializer]
public sealed record ValidateSessionRequest(
    [property: Id(0)] string SessionToken);

[GenerateSerializer]
public sealed record ValidateSessionResponse(
    [property: Id(0)] bool IsValid,
    [property: Id(1)] string? AccountId,
    [property: Id(2)] long? ExpireAtUnixMs);

[GenerateSerializer]
public sealed record RenewSessionRequest(
    [property: Id(0)] string SessionToken,
    [property: Id(1)] int ExtendSeconds,
    [property: Id(2)] bool RotateToken);

[GenerateSerializer]
public sealed record RenewSessionResponse(
    [property: Id(0)] bool IsValid,
    [property: Id(1)] long? ExpireAtUnixMs,
    [property: Id(2)] string? SessionToken);

[GenerateSerializer]
public sealed record LogoutRequest(
    [property: Id(0)] string SessionToken);

[GenerateSerializer]
public sealed record LogoutResponse(
    [property: Id(0)] bool Success);

[GenerateSerializer]
public sealed record CreateSessionForAccountRequest(
    [property: Id(0)] string AccountId,
    [property: Id(1)] int ExpireSeconds,
    [property: Id(2)] bool KickExisting);

[GenerateSerializer]
public sealed record CreateSessionForAccountResponse(
    [property: Id(0)] string SessionToken,
    [property: Id(1)] long ExpireAtUnixMs,
    [property: Id(2)] string? KickedSessionToken);
