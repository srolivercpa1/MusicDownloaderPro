using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace MusicDownloaderPro.Utilities;

public sealed class AudioCover : Image
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(8) };
    private CancellationTokenSource? _load;
    public static readonly DependencyProperty CoverUriProperty = DependencyProperty.Register(nameof(CoverUri), typeof(Uri), typeof(AudioCover), new PropertyMetadata(null, Changed));
    public Uri? CoverUri { get => (Uri?)GetValue(CoverUriProperty); set => SetValue(CoverUriProperty, value); }
    public AudioCover()
    {
        Loaded += (_, _) => _ = LoadAsync();
        Unloaded += (_, _) => _load?.Cancel();
    }
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is AudioCover cover && cover.IsLoaded) _ = cover.LoadAsync();
    }
    private async Task LoadAsync()
    {
        _load?.Cancel();
        using var current = new CancellationTokenSource();
        _load = current;
        Source = null;
        try
        {
            var uri = CoverUri;
            for (int redirects = 0; redirects < 4 && uri is not null; redirects++)
            {
                if (uri.Scheme != "https" || uri.Port != 443 || uri.UserInfo.Length != 0 ||
                    !(uri.Host == "archive.org" || uri.Host.EndsWith(".archive.org", StringComparison.OrdinalIgnoreCase))) return;
                using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, current.Token);
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    var location = response.Headers.Location;
                    uri = location is null ? null : new Uri(uri, location); continue;
                }
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 2_097_152) return;
                using var bytes = new MemoryStream();
                using var stream = await response.Content.ReadAsStreamAsync(current.Token);
                var buffer = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(buffer, current.Token)) > 0)
                {
                    if (bytes.Length + read > 2_097_152) return;
                    bytes.Write(buffer, 0, read);
                }
                bytes.Position = 0;
                var bitmap = new BitmapImage();
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 108; bitmap.StreamSource = bytes; bitmap.EndInit(); bitmap.Freeze();
                if (_load == current && !current.IsCancellationRequested) Source = bitmap;
                return;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException) { /* Optional artwork leaves the musical fallback visible. */ }
        finally { if (_load == current) _load = null; }
    }
}
