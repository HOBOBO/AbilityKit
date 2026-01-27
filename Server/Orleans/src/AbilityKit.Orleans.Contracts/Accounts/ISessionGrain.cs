using Orleans;

namespace AbilityKit.Orleans.Contracts.Accounts;

public interface ISessionGrain : IGrainWithStringKey
{
    Task<ValidateSessionResponse> ValidateAsync(ValidateSessionRequest request);

    Task<GuestLoginResponse> CreateGuestAsync();

    Task<RenewSessionResponse> RenewAsync(RenewSessionRequest request);

    Task<LogoutResponse> LogoutAsync(LogoutRequest request);

    Task<CreateSessionForAccountResponse> CreateSessionForAccountAsync(CreateSessionForAccountRequest request);
}
