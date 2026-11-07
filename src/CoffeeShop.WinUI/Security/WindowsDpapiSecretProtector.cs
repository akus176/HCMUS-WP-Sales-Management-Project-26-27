using System.Security.Cryptography;
using System.Text;

namespace CoffeeShop.WinUI.Security;

/// <summary>Protects refresh tokens for the current Windows user with DPAPI.</summary>
public sealed class WindowsDpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CoffeeShop.WinUI.RefreshToken.v1");

    public byte[] Protect(byte[] plaintext) =>
        ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] protectedData) =>
        ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.CurrentUser);
}
