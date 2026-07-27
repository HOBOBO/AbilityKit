using AbilityKit.Orleans.Contracts.Accounts;
using AbilityKit.Orleans.Grains.Persistence;
using Orleans;
using ContractGuestLoginResponse = AbilityKit.Orleans.Contracts.Accounts.GuestLoginResponse;

namespace AbilityKit.Orleans.Grains.Accounts;

public sealed class SessionGrain : Grain, ISessionGrain
{
    private const int SlidingExpirationSeconds = 30 * 60;
    private const int MaxAbsoluteTtlSeconds = 24 * 60 * 60;

    private readonly ISessionStateStore _sessionStateStore;

    public SessionGrain(ISessionStateStore sessionStateStore)
    {
        _sessionStateStore = sessionStateStore ?? throw new ArgumentNullException(nameof(sessionStateStore));
    }

    public async Task<ContractGuestLoginResponse> CreateGuestAsync()
    {
        await CleanupExpiredAsync();

        var accountId = Guid.NewGuid().ToString("N");
        var sessionToken = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expireAt = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds();

        await _sessionStateStore.UpsertAsync(new SessionStateRecord(sessionToken, accountId, now, expireAt));
        return new ContractGuestLoginResponse(accountId, sessionToken, expireAt);
    }

    public async Task<ValidateSessionResponse> ValidateAsync(ValidateSessionRequest request)
    {
        await CleanupExpiredAsync();

        if (request is null || string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return new ValidateSessionResponse(false, null, null);
        }

        var info = await _sessionStateStore.GetByTokenAsync(request.SessionToken);
        if (info is null)
        {
            return new ValidateSessionResponse(false, null, null);
        }

        var updated = await ApplySlidingExpirationIfNeededAsync(info);
        return new ValidateSessionResponse(true, updated.AccountId, updated.ExpireAtUnixMs);
    }

    public async Task<RenewSessionResponse> RenewAsync(RenewSessionRequest request)
    {
        await CleanupExpiredAsync();

        if (request is null || string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return new RenewSessionResponse(false, null, null);
        }

        var info = await _sessionStateStore.GetByTokenAsync(request.SessionToken);
        if (info is null)
        {
            return new RenewSessionResponse(false, null, null);
        }

        var extendSeconds = request.ExtendSeconds;
        if (extendSeconds <= 0) extendSeconds = 3600;
        if (extendSeconds > 24 * 3600) extendSeconds = 24 * 3600;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var absoluteMaxExpireAt = info.IssuedAtUnixMs + (long)MaxAbsoluteTtlSeconds * 1000;
        var desiredExpireAt = now + (long)extendSeconds * 1000;
        var newExpireAt = Math.Min(desiredExpireAt, absoluteMaxExpireAt);

        if (request.RotateToken)
        {
            var newToken = Guid.NewGuid().ToString("N");
            await _sessionStateStore.RemoveByTokenAsync(request.SessionToken);
            await _sessionStateStore.UpsertAsync(info with { SessionToken = newToken, ExpireAtUnixMs = newExpireAt });
            return new RenewSessionResponse(true, newExpireAt, newToken);
        }

        await _sessionStateStore.UpsertAsync(info with { ExpireAtUnixMs = newExpireAt });
        return new RenewSessionResponse(true, newExpireAt, request.SessionToken);
    }

    public async Task<LogoutResponse> LogoutAsync(LogoutRequest request)
    {
        await CleanupExpiredAsync();

        if (request is null || string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return new LogoutResponse(false);
        }

        var existing = await _sessionStateStore.GetByTokenAsync(request.SessionToken);
        if (existing is null)
        {
            return new LogoutResponse(false);
        }

        await _sessionStateStore.RemoveByTokenAsync(request.SessionToken);
        return new LogoutResponse(true);
    }

    public async Task<CreateSessionForAccountResponse> CreateSessionForAccountAsync(CreateSessionForAccountRequest request)
    {
        await CleanupExpiredAsync();

        if (request is null || string.IsNullOrWhiteSpace(request.AccountId))
        {
            throw new ArgumentException("AccountId is required", nameof(request));
        }

        var expireSeconds = request.ExpireSeconds;
        if (expireSeconds <= 0) expireSeconds = 24 * 3600;
        if (expireSeconds > 30 * 24 * 3600) expireSeconds = 30 * 24 * 3600;

        string? kickedToken = null;
        var existingInfo = await _sessionStateStore.GetByAccountIdAsync(request.AccountId);
        if (existingInfo is not null)
        {
            if (request.KickExisting)
            {
                kickedToken = existingInfo.SessionToken;
                await _sessionStateStore.RemoveByTokenAsync(existingInfo.SessionToken);
            }
            else
            {
                return new CreateSessionForAccountResponse(existingInfo.SessionToken, existingInfo.ExpireAtUnixMs, null);
            }
        }

        var sessionToken = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expireAt = DateTimeOffset.UtcNow.AddSeconds(expireSeconds).ToUnixTimeMilliseconds();

        await _sessionStateStore.UpsertAsync(new SessionStateRecord(sessionToken, request.AccountId, now, expireAt));

        return new CreateSessionForAccountResponse(sessionToken, expireAt, kickedToken);
    }

    private Task CleanupExpiredAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return _sessionStateStore.CleanupExpiredAsync(now);
    }

    private async Task<SessionStateRecord> ApplySlidingExpirationIfNeededAsync(SessionStateRecord info)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var absoluteMaxExpireAt = info.IssuedAtUnixMs + (long)MaxAbsoluteTtlSeconds * 1000;
        var desiredExpireAt = now + (long)SlidingExpirationSeconds * 1000;
        var newExpireAt = Math.Min(desiredExpireAt, absoluteMaxExpireAt);

        if (newExpireAt <= info.ExpireAtUnixMs)
        {
            return info;
        }

        var updated = info with { ExpireAtUnixMs = newExpireAt };
        await _sessionStateStore.UpsertAsync(updated);
        return updated;
    }
}
