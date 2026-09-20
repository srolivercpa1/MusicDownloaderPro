using MusicDownloaderPro.Services;
using System.IO;
using Xunit;

namespace MusicDownloaderPro.Tests;

public sealed class SecurityTests
{
    [Theory]
    [InlineData("http://example.com/song.mp3")]
    [InlineData("file:///C:/song.mp3")]
    [InlineData("https://localhost/song.mp3")]
    [InlineData("https://127.0.0.1/song.mp3")]
    public void RejectsUnsafeUrls(string value) =>
        Assert.False(SecurityService.IsSafePublicHttpsUrl(value, out _));

    [Theory]
    [InlineData("song.mp3", true)]
    [InlineData("song.flac", true)]
    [InlineData("setup.exe", false)]
    [InlineData("page.html", false)]
    public void AllowsOnlyAudioExtensions(string name, bool expected) =>
        Assert.Equal(expected, SecurityService.HasAllowedAudioExtension(name));

    [Fact]
    public void SanitizedPathCannotEscapeRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MusicDownloaderProTests");
        var result = SecurityService.CreateSafeDestination(root, "..\\..\\CON", ".mp3");
        Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, result, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("_CON.mp3", result, StringComparison.OrdinalIgnoreCase);
    }
}
