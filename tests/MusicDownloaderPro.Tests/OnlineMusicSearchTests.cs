using MusicDownloaderPro.Models;
using MusicDownloaderPro.Services;
using Xunit;

namespace MusicDownloaderPro.Tests;

public sealed class OnlineMusicSearchTests
{
    [Fact]
    public void SearchParserReadsInternetArchiveDocuments()
    {
        const string json = """{"response":{"docs":[{"identifier":"item-1","title":"Piano Song","creator":"Jane Artist","collection":["audio_music"]}]}}""";
        var items = InternetArchiveMusicProvider.ParseSearchResponse(json);
        Assert.Single(items);
        Assert.Equal("item-1", items[0].Identifier);
        Assert.Equal("Piano Song", items[0].Title);
        Assert.Equal("Jane Artist", items[0].Artist);
    }

    [Fact]
    public void MetadataParserReturnsOnlyAllowedOriginalAudio()
    {
        const string json = """{"metadata":{"title":"Album","creator":"Artist","licenseurl":"https://creativecommons.org/licenses/by/4.0/"},"files":[{"name":"song.mp3","format":"VBR MP3","source":"original","size":"1234"},{"name":"page.html","format":"Metadata","source":"original"},{"name":"derived.mp3","format":"VBR MP3","source":"derivative"}]}""";
        var options = InternetArchiveMusicProvider.ParseMetadata("item-1", json);
        var option = Assert.Single(options);
        Assert.Equal("MP3", option.Format);
        Assert.Equal(1234, option.Size);
        Assert.Equal("https://archive.org/download/item-1/song.mp3", option.Url.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptySearchIsRejected(string query)
    {
        var service = new OnlineMusicSearchService([]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(query, default));
    }

    [Fact]
    public async Task SearchLongerThanTwoHundredCharactersIsRejected()
    {
        var service = new OnlineMusicSearchService([]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(new string('a', 201), default));
    }
}
