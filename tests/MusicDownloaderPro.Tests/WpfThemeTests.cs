using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using MusicDownloaderPro.Models;
using Xunit;

namespace MusicDownloaderPro.Tests;

public sealed class WpfThemeTests
{
    public sealed class Commands
    {
        public OnlineMusicResult? Clicked { get; private set; }
        public RelayCommand<OnlineMusicResult> DownloadOnlineResultCommand { get; }
        public RelayCommand<OnlineMusicResult> OpenOnlineSourceCommand { get; }
        public Commands()
        {
            DownloadOnlineResultCommand = new(result => Clicked = result);
            OpenOnlineSourceCommand = new(result => Clicked = result);
        }
    }

    [Fact]
    public void TemplatesLoadAndCardButtonsTargetTheirOwnResult()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                foreach (var file in new[] { "Colors", "Controls", "Layout", "Search" })
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"/MusicDownloaderPro;component/Themes/{file}.xaml", UriKind.Relative) });
                var header = new GridViewColumnHeader { Content = "Título" };
                header.Style = (Style)app.FindResource(typeof(GridViewColumnHeader));
                header.ApplyTemplate();
                Assert.IsType<System.Windows.Controls.Primitives.Thumb>(header.Template.FindName("PART_HeaderGripper", header));
                var first = Track("Primeira música", true);
                var second = Track(new string('M', 240), true);
                var locked = Track("Sem arquivo disponível", false);
                var commands = new Commands();
                var list = new ListView { ItemsSource = new[] { first, second, locked }, ItemTemplate = (DataTemplate)app.FindResource("OnlineResultCard"), ItemContainerStyle = (Style)app.FindResource("SearchResultItem") };
                window = new Window { Width = 720, Height = 600, DataContext = commands, Content = list, ShowInTaskbar = false };
                window.Show(); window.UpdateLayout();
                var buttons = Descendants<Button>(window).ToArray();
                var download = Assert.Single(buttons, b => ReferenceEquals(b.CommandParameter, second) && b.Content?.ToString()?.Contains("Baixar") == true);
                Assert.Same(second, download.CommandParameter);
                download.Command.Execute(download.CommandParameter);
                Assert.Same(second, commands.Clicked);
                Assert.True(download.ActualWidth > 50);
                var unavailable = Assert.Single(buttons, b => ReferenceEquals(b.CommandParameter, locked) && b.Content?.ToString()?.Contains("Baixar") == true);
                Assert.Equal(Visibility.Collapsed, unavailable.Visibility);
                var directory = Environment.GetEnvironmentVariable("MDP_TEST_SCREENSHOTS");
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(directory, "search-cards.png")); encoder.Save(stream);
                }
                window.Close(); app.Shutdown();
            }
            catch (Exception ex) { failure = ex; window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF test timed out");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static OnlineMusicResult Track(string title, bool downloadable) => new("id", title, "Artista", "Álbum", "CC BY 4.0", "Fonte de teste", new Uri("https://archive.org/details/test"), null,
        downloadable ? new[] { new AuthorizedDownloadOption(new Uri("https://archive.org/download/test/music.mp3"), "MP3", 1000, "Original", "music.mp3") } : Array.Empty<AuthorizedDownloadOption>());

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
