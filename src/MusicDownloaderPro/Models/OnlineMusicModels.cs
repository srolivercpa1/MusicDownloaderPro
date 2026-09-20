namespace MusicDownloaderPro.Models;

public sealed record ArchiveSearchItem(string Identifier, string Title, string Artist);

public sealed record AuthorizedDownloadOption(
    Uri Url,
    string Format,
    long? Size,
    string Quality,
    string FileName);

public sealed record OnlineMusicResult(
    string Identifier,
    string Title,
    string Artist,
    string Album,
    string License,
    string Provider,
    Uri SourcePage,
    Uri? CoverUrl,
    IReadOnlyList<AuthorizedDownloadOption> DownloadOptions)
{
    public bool CanDownload => DownloadOptions.Count > 0;
    public string Formats => string.Join(", ", DownloadOptions.Select(x => x.Format).Distinct());
}

public sealed record ProviderSearchResult(
    IReadOnlyList<OnlineMusicResult> Items,
    string? Error = null);

public sealed record OnlineSearchResponse(
    IReadOnlyList<OnlineMusicResult> Results,
    IReadOnlyList<string> Failures);
