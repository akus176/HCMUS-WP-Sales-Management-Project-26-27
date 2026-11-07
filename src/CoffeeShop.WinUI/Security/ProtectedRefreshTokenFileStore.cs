using System.Security.Cryptography;
using System.Text;
using CoffeeShop.WinUI.Services;

namespace CoffeeShop.WinUI.Security;

public interface ISecretProtector
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] protectedData);
}

public sealed class ProtectedRefreshTokenFileStore : IRefreshTokenStore
{
    private readonly string path;
    private readonly ISecretProtector protector;

    public ProtectedRefreshTokenFileStore(string path, ISecretProtector protector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(protector);
        this.path = Path.GetFullPath(path);
        this.protector = protector;
    }

    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var protectedData = await File.ReadAllBytesAsync(path, cancellationToken);
            var plaintext = protector.Unprotect(protectedData);
            var token = Encoding.UTF8.GetString(plaintext);
            CryptographicOperations.ZeroMemory(plaintext);
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch (CryptographicException)
        {
            await ClearAsync(cancellationToken);
            return null;
        }
    }

    public async Task SaveAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var plaintext = Encoding.UTF8.GetBytes(refreshToken);
        var protectedData = protector.Protect(plaintext);
        CryptographicOperations.ZeroMemory(plaintext);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, protectedData, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedData);
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
