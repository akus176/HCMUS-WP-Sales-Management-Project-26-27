using CoffeeShop.Contracts;

namespace CoffeeShop.WinUI.Services;

public interface IRefreshTokenStore
{
    Task<string?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class StaffSessionManager
{
    private static readonly TimeSpan RefreshLeeway = TimeSpan.FromMinutes(1);
    private readonly IStaffIdentityApi identityApi;
    private readonly IRefreshTokenStore tokenStore;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);
    private CoffeeShop.Contracts.TokenResponse? tokens;

    public StaffSessionManager(IStaffIdentityApi identityApi, IRefreshTokenStore tokenStore, TimeProvider? timeProvider = null)
    {
        this.identityApi = identityApi;
        this.tokenStore = tokenStore;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsAuthenticated => tokens is not null;
    public StaffSession? CurrentUser => tokens?.User;

    public async Task<StaffSession> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var response = await identityApi.LoginAsync(username, password, cancellationToken);
            await tokenStore.SaveAsync(response.RefreshToken, cancellationToken);
            tokens = response;
            return response.User;
        }
        finally { gate.Release(); }
    }

    public async Task<StaffSession?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var refresh = await tokenStore.LoadAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(refresh)) return null;
            try
            {
                var response = await identityApi.RefreshAsync(refresh, cancellationToken);
                await tokenStore.SaveAsync(response.RefreshToken, cancellationToken);
                tokens = response;
                return response.User;
            }
            catch (ApiException exception) when (exception.Kind == ApiErrorKind.Unauthorized)
            {
                tokens = null;
                await tokenStore.ClearAsync(cancellationToken);
                return null;
            }
        }
        finally { gate.Release(); }
    }

    public async Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (tokens is null) return null;
            if (tokens.ExpiresAt - timeProvider.GetUtcNow() > RefreshLeeway) return tokens.AccessToken;
            return await RefreshLockedAsync(tokens.RefreshToken, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task<string?> RefreshAfterUnauthorizedAsync(string observedAccessToken, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (tokens is null) return null;
            if (!string.Equals(tokens.AccessToken, observedAccessToken, StringComparison.Ordinal)) return tokens.AccessToken;
            return await RefreshLockedAsync(tokens.RefreshToken, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var previous = tokens;
            try
            {
                if (previous is not null)
                    await identityApi.LogoutAsync(previous.RefreshToken, previous.AccessToken, cancellationToken);
            }
            finally
            {
                tokens = null;
                await tokenStore.ClearAsync(cancellationToken);
            }
        }
        finally { gate.Release(); }
    }

    public async Task ClearLocalSessionAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            tokens = null;
            await tokenStore.ClearAsync(cancellationToken);
        }
        finally { gate.Release(); }
    }

    private async Task<string?> RefreshLockedAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            var response = await identityApi.RefreshAsync(refreshToken, cancellationToken);
            await tokenStore.SaveAsync(response.RefreshToken, cancellationToken);
            tokens = response;
            return response.AccessToken;
        }
        catch (ApiException exception) when (exception.Kind == ApiErrorKind.Unauthorized)
        {
            tokens = null;
            await tokenStore.ClearAsync(cancellationToken);
            return null;
        }
    }
}
