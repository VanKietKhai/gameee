using System.Security.Cryptography;
using System.Text;
using ConanServerControl.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Security;

public sealed class DpapiSecretProtector : ISecretProtector
{
    private const string DevPrefix = "dev-base64:";
    private readonly ILogger<DpapiSecretProtector> _logger;

    public DpapiSecretProtector(ILogger<DpapiSecretProtector> logger)
    {
        _logger = logger;
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var bytes = Encoding.UTF8.GetBytes(plaintext);

        if (OperatingSystem.IsWindows())
        {
            var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        _logger.LogWarning(
            "Windows DPAPI is not available on this OS. Secrets are stored with a development-only encoding and are NOT securely protected.");
        return DevPrefix + Convert.ToBase64String(bytes);
    }

    public string Unprotect(string protectedPayload)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectedPayload);

        if (protectedPayload.StartsWith(DevPrefix, StringComparison.Ordinal))
        {
            var encoded = protectedPayload[DevPrefix.Length..];
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }

        if (OperatingSystem.IsWindows())
        {
            var bytes = Convert.FromBase64String(protectedPayload);
            var plain = ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }

        throw new PlatformNotSupportedException("Cannot unprotect DPAPI payloads on this operating system.");
    }
}
