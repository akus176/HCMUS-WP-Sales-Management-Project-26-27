using System.Security.Cryptography;
using System.Text;

namespace CoffeeShop.Contracts;

public static class GoogleAdminOAuth
{
    public static string LoopbackRedirect(int port) => $"http://127.0.0.1:{port}/oauth/google";

    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
