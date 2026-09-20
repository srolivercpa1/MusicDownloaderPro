using MusicDownloaderPro.Models;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace MusicDownloaderPro.Services;

public sealed class InternetArchiveMusicProvider : IOnlineMusicProvider
{
    private readonly HttpClient _http;
    public string Name => "Internet Archive";
    public InternetArchiveMusicProvider(HttpClient? http = null) => _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<ProviderSearchResult> SearchAsync(string query, int limit, CancellationToken token)
    {
        limit = Math.Clamp(limit, 1, 50);
        var searchUri = new Uri("https://archive.org/advancedsearch.php?q=" + Uri.EscapeDataString($"mediatype:audio AND ({query})") + $"&fl[]=identifier&fl[]=title&fl[]=creator&rows={limit}&page=1&output=json");
        using var searchResponse = await _http.GetAsync(searchUri, token);
        searchResponse.EnsureSuccessStatusCode();
        var candidates = ParseSearchResponse(await searchResponse.Content.ReadAsStringAsync(token));
        using var gate = new SemaphoreSlim(6);
        var tasks = candidates.Select(async candidate =>
        {
            await gate.WaitAsync(token);
            try { return await LoadItemAsync(candidate, token); }
            finally { gate.Release(); }
        });
        var items = (await Task.WhenAll(tasks)).Where(x => x is not null).Cast<OnlineMusicResult>().ToList();
        return new ProviderSearchResult(items);
    }

    private async Task<OnlineMusicResult?> LoadItemAsync(ArchiveSearchItem candidate, CancellationToken token)
    {
        var id = Uri.EscapeDataString(candidate.Identifier);
        using var response = await _http.GetAsync(new Uri($"https://archive.org/metadata/{id}"), token);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync(token);
        var options = ParseMetadata(candidate.Identifier, json);
        if (options.Count == 0) return null;
        using var document = JsonDocument.Parse(json);
        var metadata = document.RootElement.TryGetProperty("metadata", out var md) ? md : default;
        var title = GetText(metadata, "title", candidate.Title);
        var artist = GetText(metadata, "creator", candidate.Artist);
        var album = GetText(metadata, "album", "Não informado");
        var license = GetText(metadata, "licenseurl", GetText(metadata, "rights", "Não informado"));
        return new OnlineMusicResult(candidate.Identifier, title, artist, album, license, Name,
            new Uri($"https://archive.org/details/{id}"), new Uri($"https://archive.org/services/img/{id}"), options);
    }

    public static IReadOnlyList<ArchiveSearchItem> ParseSearchResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("response", out var response) || !response.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Array) return [];
        var items = new List<ArchiveSearchItem>();
        foreach (var doc in docs.EnumerateArray())
        {
            var id = GetText(doc, "identifier", ""); if (string.IsNullOrWhiteSpace(id)) continue;
            items.Add(new ArchiveSearchItem(id, GetText(doc, "title", "Não informado"), GetText(doc, "creator", "Não informado")));
        }
        return items;
    }

    public static IReadOnlyList<AuthorizedDownloadOption> ParseMetadata(string identifier, string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array) return [];
        var result = new List<AuthorizedDownloadOption>();
        foreach (var file in files.EnumerateArray())
        {
            var name = GetText(file, "name", "");
            var source = GetText(file, "source", "");
            if (!source.Equals("original", StringComparison.OrdinalIgnoreCase) || !SecurityService.HasAllowedAudioExtension(name)) continue;
            var extension = Path.GetExtension(name).TrimStart('.').ToUpperInvariant();
            var sizeText = GetText(file, "size", ""); long? size = long.TryParse(sizeText, out var parsed) ? parsed : null;
            var url = new Uri($"https://archive.org/download/{Uri.EscapeDataString(identifier)}/{EscapePath(name)}");
            result.Add(new AuthorizedDownloadOption(url, extension, size, "Original", name));
        }
        return result;
    }

    private static string EscapePath(string name) => string.Join("/", name.Split('/').Select(Uri.EscapeDataString));
    private static string GetText(JsonElement element, string property, string fallback)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(property, out var value)) return fallback;
        if (value.ValueKind == JsonValueKind.Array) return value.GetArrayLength() > 0 ? value[0].ToString() : fallback;
        var text = value.ToString(); return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }
}
