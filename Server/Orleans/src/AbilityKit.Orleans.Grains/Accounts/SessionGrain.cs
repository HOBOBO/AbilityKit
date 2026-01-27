using System;
using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.Accounts;
using Orleans;

namespace AbilityKit.Orleans.Grains.Accounts;

public sealed class SessionGrain : Grain, ISessionGrain
{
    private sealed record SessionInfo(string AccountId, long IssuedAtUnixMs, long ExpireAtUnixMs);

    private const int SlidingExpirationSeconds = 30 * 60;
    private const int MaxAbsoluteTtlSeconds = 24 * 60 * 60;

    private readonly Dictionary<string, SessionInfo> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _accountToToken = new(StringComparer.Ordinal);

    public Task<GuestLoginResponse> CreateGuestAsync()
    {
        CleanupExpired();

        var accountId = Guid.NewGuid().ToString("N");
        var sessionToken = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expireAt = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds();

        _sessions[sessionToken] = new SessionInfo(accountId, now, expireAt);
        _accountToToken[accountId] = sessionToken;
        return Task.FromResult(new GuestLoginResponse(accountId, sessionToken, expireAt));
    }

    public Task<ValidateSessionResponse> ValidateAsync(ValidateSessionRequest request)
    {
        CleanupExpired();

        if (request is null || string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return Task.FromResult(new ValidateSessionResponse(false, null, null));
        }

        if (_sessions.TryGetValue(request.SessionToken, out var info))
        {
            var updated = ApplySlidingExpirationIfNeeded(request.SessionToken, info);
            return Task.FromResult(new ValidateSessionResponse(true, updated.AccountId, updated.ExpireAtUnixMs));
        }

        return Task.FromResult(new ValidateSessionResponse(false, null, null));
    }

    public Task<RenewSessionResponse> RenewAsync(RenewSessionRequest request)
    {
        CleanupExpired();

        if (request is null || string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return Task.FromResult(new RenewSessionResponse(false, null, null));
        }

        if (!_sessions.TryGetValue(request.SessionToken, out var info))
        {
            return Task.FromResult(new RenewSessionResponse(false, null, null));
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
            _sessions.Remove(request.SessionToken);
            _sessions[newToken] = info with { ExpireAtUnixMs = newExpireAt };
            _accountToToken[info.AccountId] = newToken;
            return Task.FromResult(new RenewSessionResponse(true, newExpireAt, newToken));
        }

        _sessions[request.SessionToken] = info with { ExpireAtUnixMs = newExpireAt };
        return Task.FromResult(new RenewSessionResponse(true, newExpireAt, request.SessionToken));
    }

    public Task<LogoutResponse> LogoutAsync(LogoutRequest request)
    {
        CleanupExpired();

        if (request is null || string.IsNullOrWhiteSpace(request.SessionToken))
        {
            return Task.FromResult(new LogoutResponse(false));
        }

        string? accountId = null;
        if (_sessions.TryGetValue(request.SessionToken, out var info))
        {
            accountId = info.AccountId;
        }

        var removed = _sessions.Remove(request.SessionToken);
        if (removed && accountId != null && _accountToToken.TryGetValue(accountId, out var token) && string.Equals(token, request.SessionToken, StringComparison.Ordinal))
        {
            _accountToToken.Remove(accountId);
        }
        return Task.FromResult(new LogoutResponse(removed));
    }

    public Task<CreateSessionForAccountResponse> CreateSessionForAccountAsync(CreateSessionForAccountRequest request)
    {
        CleanupExpired();

        if (request is null || string.IsNullOrWhiteSpace(request.AccountId))
        {
            throw new ArgumentException("AccountId is required", nameof(request));
        }

        var expireSeconds = request.ExpireSeconds;
        if (expireSeconds <= 0) expireSeconds = 24 * 3600;
        if (expireSeconds > 30 * 24 * 3600) expireSeconds = 30 * 24 * 3600;

        string? kickedToken = null;
        if (_accountToToken.TryGetValue(request.AccountId, out var existingToken))
        {
            if (request.KickExisting)
            {
                kickedToken = existingToken;
                _sessions.Remove(existingToken);
                _accountToToken.Remove(request.AccountId);
            }
            else
            {
                if (_sessions.TryGetValue(existingToken, out var existingInfo))
                {
                    return Task.FromResult(new CreateSessionForAccountResponse(existingToken, existingInfo.ExpireAtUnixMs, null));
                }

                _accountToToken.Remove(request.AccountId);
            }
        }

        var sessionToken = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expireAt = DateTimeOffset.UtcNow.AddSeconds(expireSeconds).ToUnixTimeMilliseconds();

        _sessions[sessionToken] = new SessionInfo(request.AccountId, now, expireAt);
        _accountToToken[request.AccountId] = sessionToken;

        return Task.FromResult(new CreateSessionForAccountResponse(sessionToken, expireAt, kickedToken));
    }

    private void CleanupExpired()
    {
        if (_sessions.Count == 0) return;

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        List<string>? toRemove = null;

        foreach (var kv in _sessions)
        {
            if (kv.Value.ExpireAtUnixMs <= now)
            {
                toRemove ??= new List<string>();
                toRemove.Add(kv.Key);
            }
        }

        if (toRemove == null) return;
        foreach (var token in toRemove)
        {
            if (_sessions.TryGetValue(token, out var info))
            {
                if (_accountToToken.TryGetValue(info.AccountId, out var mapped) && string.Equals(mapped, token, StringComparison.Ordinal))
                {
                    _accountToToken.Remove(info.AccountId);
                }
            }

            _sessions.Remove(token);
        }
    }

    private SessionInfo ApplySlidingExpirationIfNeeded(string sessionToken, SessionInfo info)
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
        _sessions[sessionToken] = updated;
        return updated;
    }
}
