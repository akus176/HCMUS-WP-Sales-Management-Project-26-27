using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using CoffeeShop.Contracts;

namespace CoffeeShop.WinUI.Services;

internal sealed class GoogleAdminSignIn(StaffIdentityApiClient api, StaffSessionManager session)
{
    public async Task<StaffSession?> LoginAsync(Action<Uri> openBrowser, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var ct = timeout.Token;
        var config = await api.GetGoogleConfigAsync(ct);
        var verifier = GoogleAdminOAuth.Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = GoogleAdminOAuth.Challenge(verifier);
        var state = GoogleAdminOAuth.Base64Url(RandomNumberGenerator.GetBytes(32));

        using var portReservation = new TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();
        var redirect = GoogleAdminOAuth.LoopbackRedirect(port);
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["redirect_uri"] = redirect,
            ["response_type"] = "code",
            ["scope"] = "openid email",
            ["state"] = state,
            ["nonce"] = challenge,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["prompt"] = "select_account",
        };
        var query = string.Join('&', parameters.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
        openBrowser(new Uri($"https://accounts.google.com/o/oauth2/v2/auth?{query}"));

        while (true)
        {
            var callback = await listener.GetContextAsync().WaitAsync(ct);
            using var response = callback.Response;
            var request = callback.Request;
            var match = request.HttpMethod == "GET" && request.Url?.GetLeftPart(UriPartial.Path) == redirect &&
                request.QueryString.GetValues("state") is [var returnedState] && returnedState == state;
            var code = match && request.QueryString.GetValues("code") is [var returnedCode] ? returnedCode : null;
            var denied = match && request.QueryString.GetValues("error") is [_] && code is null;
            var accepted = code is { Length: > 0 and <= 2048 } || denied;
            response.StatusCode = accepted ? 200 : 400;
            response.ContentType = "text/plain; charset=utf-8";
            response.Headers["Cache-Control"] = "no-store";
            response.Headers["Referrer-Policy"] = "no-referrer";
            var message = Encoding.UTF8.GetBytes(accepted
                ? "You can close this tab and return to Corner Cafe."
                : "This sign-in response could not be used. Return to Corner Cafe.");
            response.ContentLength64 = message.Length;
            await response.OutputStream.WriteAsync(message, ct);
            if (!accepted) continue;
            response.Close();
            listener.Stop();
            return denied ? null : await session.LoginWithAsync(
                token => api.LoginWithGoogleAsync(new(code, verifier, redirect), token), ct);
        }
    }
}
