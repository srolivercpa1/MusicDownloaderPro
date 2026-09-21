using System.IO;
using System.Xml.Linq;
using Xunit;

namespace MusicDownloaderPro.Tests;

public sealed class ThemeContractTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MusicDownloaderPro.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void EveryNamedResourceReferenceResolves()
    {
        var files = Directory.GetFiles(Path.Combine(Root(), "src", "MusicDownloaderPro"), "*.xaml", SearchOption.AllDirectories);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var keys = files.SelectMany(f => XDocument.Load(f).Descendants()).Select(n => n.Attribute(x + "Key")?.Value).Where(v => v is not null).ToHashSet();
        foreach (var file in files)
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), @"\{(?:Static|Dynamic)Resource (\w+)\}"))
                Assert.True(keys.Contains(match.Groups[1].Value), $"{file}: missing {match.Groups[1].Value}");
    }

    [Theory]
    [InlineData("TextPrimaryBrush", "SurfaceBrush")]
    [InlineData("TextPrimaryBrush", "SelectedBrush")]
    [InlineData("TextSecondaryBrush", "SurfaceElevatedBrush")]
    [InlineData("TextPrimaryBrush", "PrimaryBrush")]
    public void TextContrastMeetsMinimum(string foreground, string background)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var colors = XDocument.Load(Path.Combine(Root(), "src/MusicDownloaderPro/Themes/Colors.xaml")).Root!.Elements().ToDictionary(n => n.Attribute(x + "Key")!.Value, n => n.Attribute("Color")!.Value);
        static double Luminance(string hex)
        {
            var rgb = new[] { 1, 3, 5 }.Select(i => Convert.ToInt32(hex.Substring(i, 2), 16) / 255d).Select(c => c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4)).ToArray();
            return rgb[0] * .2126 + rgb[1] * .7152 + rgb[2] * .0722;
        }
        double a = Luminance(colors[foreground]), b = Luminance(colors[background]);
        Assert.True((Math.Max(a,b)+.05)/(Math.Min(a,b)+.05) >= 4.5);
    }
}
