using System.Net;
using System.IO;
using System.Text.RegularExpressions;

namespace MusicDownloaderPro.Services;

public static partial class SecurityService
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".flac", ".m4a", ".ogg" };
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase) { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

    public static bool IsSafePublicHttpsUrl(string value, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(parsed.UserInfo)) return false;
        if (parsed.IsLoopback || parsed.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(parsed.Host, out var ip) && IsPrivate(ip)) return false;
        uri = parsed;
        return true;
    }

    public static async Task<bool> ResolvesToPublicAddressAsync(Uri uri, CancellationToken token)
    {
        try { return (await Dns.GetHostAddressesAsync(uri.Host, token)).Length > 0 && (await Dns.GetHostAddressesAsync(uri.Host, token)).All(ip => !IsPrivate(ip)); }
        catch { return false; }
    }

    public static bool HasAllowedAudioExtension(string path) => AudioExtensions.Contains(Path.GetExtension(path));

    public static string CreateSafeDestination(string root, string requestedName, string extension)
    {
        var clean = Path.GetFileNameWithoutExtension(requestedName);
        clean = InvalidChars().Replace(clean, "_").Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(clean)) clean = "audio";
        if (ReservedNames.Contains(clean)) clean = "_" + clean;
        if (clean.Length > 160) clean = clean[..160];
        extension = AudioExtensions.Contains(extension) ? extension.ToLowerInvariant() : throw new InvalidDataException("Formato de áudio não permitido.");
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var result = Path.GetFullPath(Path.Combine(normalizedRoot, clean + extension));
        if (!result.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("O caminho de destino não é permitido.");
        return result;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10 || b[0] == 127 || b[0] == 0 || b[0] == 169 && b[1] == 254 || b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && b[1] == 168 || b[0] >= 224;
        }
        return false;
    }

    [GeneratedRegex("[\\\\/:*?\"<>|]+", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidChars();
}
