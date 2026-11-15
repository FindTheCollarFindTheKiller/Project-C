using System.Security.Cryptography;
using System.Text;

namespace EpisodeStudio.Infrastructure.Security;

public interface IApiKeyStore
{
    Task SaveAsync(string providerName, string apiKey, CancellationToken cancellationToken = default);
    Task<string?> GetAsync(string providerName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string providerName, CancellationToken cancellationToken = default);
}

public sealed class WindowsProtectedApiKeyStore : IApiKeyStore
{
    private readonly string _baseDirectory;

    public WindowsProtectedApiKeyStore(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpisodeStudio", "secrets");

        Directory.CreateDirectory(_baseDirectory);
    }

    public Task SaveAsync(string providerName, string apiKey, CancellationToken cancellationToken = default)
    {
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiKey),
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        File.WriteAllBytes(GetPath(providerName), protectedBytes);
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var filePath = GetPath(providerName);
        if (!File.Exists(filePath))
        {
            return Task.FromResult<string?>(null);
        }

        var protectedBytes = File.ReadAllBytes(filePath);
        var plainBytes = ProtectedData.Unprotect(
            protectedBytes,
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);

        return Task.FromResult<string?>(Encoding.UTF8.GetString(plainBytes));
    }

    public Task DeleteAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var filePath = GetPath(providerName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private string GetPath(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name is required.", nameof(providerName));
        }

        var safeName = string.Concat(providerName.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            throw new ArgumentException("Provider name contains no valid file name characters.", nameof(providerName));
        }

        return Path.Combine(_baseDirectory, $"{safeName}.key");
    }
}
