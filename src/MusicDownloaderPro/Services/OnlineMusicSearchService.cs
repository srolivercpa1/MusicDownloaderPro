using MusicDownloaderPro.Models;

namespace MusicDownloaderPro.Services;

public interface IOnlineMusicProvider
{
    string Name { get; }
    Task<ProviderSearchResult> SearchAsync(string query, int limit, CancellationToken token);
}

public sealed class OnlineMusicSearchService
{
    private readonly IReadOnlyList<IOnlineMusicProvider> _providers;
    public OnlineMusicSearchService(IEnumerable<IOnlineMusicProvider> providers) => _providers = providers.ToArray();

    public async Task<OnlineSearchResponse> SearchAsync(string query, CancellationToken token)
    {
        query = query.Trim();
        if (query.Length is < 1 or > 200) throw new ArgumentException("Digite entre 1 e 200 caracteres para pesquisar.", nameof(query));
        var tasks = _providers.Select(p => SearchProviderAsync(p, query, token)).ToArray();
        var responses = await Task.WhenAll(tasks);
        var failures = responses.Where(x => x.Result.Error is not null).Select(x => $"{x.Provider}: {x.Result.Error}").ToList();
        var results = responses.SelectMany(x => x.Result.Items)
            .GroupBy(x => $"{Normalize(x.Title)}|{Normalize(x.Artist)}")
            .Select(g => g.First()).OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase).Take(50).ToList();
        return new OnlineSearchResponse(results, failures);
    }

    private static async Task<(string Provider, ProviderSearchResult Result)> SearchProviderAsync(IOnlineMusicProvider provider, string query, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try { return (provider.Name, await provider.SearchAsync(query, 50, timeout.Token)); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return (provider.Name, new ProviderSearchResult([], "Tempo limite excedido.")); }
        catch (Exception ex) { LogService.Error(ex); return (provider.Name, new ProviderSearchResult([], "Fonte temporariamente indisponível.")); }
    }

    private static string Normalize(string value) => string.Concat(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit));
}
