using Microsoft.Data.Sqlite;
using MusicDownloaderPro.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows.Media;

namespace MusicDownloaderPro.Services;

public static class AppPaths
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicDownloaderPro");
    public static readonly string Database = Path.Combine(Root, "musicdownloader.db");
    public static readonly string Logs = Path.Combine(Root, "Logs");
    public static readonly string Settings = Path.Combine(Root, "settings.json");
    public static string DefaultMusic => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "MusicDownloader Pro");
}

public static class LogService
{
    public static void Error(Exception? ex) { try { Directory.CreateDirectory(AppPaths.Logs); File.AppendAllText(Path.Combine(AppPaths.Logs, $"log-{DateTime.UtcNow:yyyy-MM-dd}.txt"), $"{DateTime.UtcNow:O} {ex}\n"); } catch { } }
}

public static class DatabaseService
{
    private static string ConnectionString => $"Data Source={AppPaths.Database}";
    public static async Task InitializeAsync()
    {
        Directory.CreateDirectory(AppPaths.Root);
        await using var connection = new SqliteConnection(ConnectionString); await connection.OpenAsync();
        var sql = """
        PRAGMA journal_mode=WAL;
        CREATE TABLE IF NOT EXISTS Downloads(Id INTEGER PRIMARY KEY AUTOINCREMENT, Url TEXT NOT NULL, Name TEXT NOT NULL, Destination TEXT NOT NULL, Status TEXT NOT NULL, Progress REAL NOT NULL DEFAULT 0, CreatedUtc TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Library(Id INTEGER PRIMARY KEY AUTOINCREMENT, Path TEXT NOT NULL UNIQUE COLLATE NOCASE, Title TEXT NOT NULL, Artist TEXT NOT NULL, Album TEXT NOT NULL, Genre TEXT NOT NULL DEFAULT '', Year INTEGER, IsFavorite INTEGER NOT NULL DEFAULT 0, AddedUtc TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS History(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Source TEXT NOT NULL, Folder TEXT NOT NULL, Status TEXT NOT NULL, Size INTEGER, CreatedUtc TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Settings(Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
        """;
        await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
    }

    public static async Task AddDownloadAsync(DownloadItem item)
    {
        await using var c = new SqliteConnection(ConnectionString); await c.OpenAsync(); await using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO Downloads(Url,Name,Destination,Status,Progress,CreatedUtc) VALUES($u,$n,$d,$s,$p,$t); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$u", item.Url); cmd.Parameters.AddWithValue("$n", item.Name); cmd.Parameters.AddWithValue("$d", item.Destination); cmd.Parameters.AddWithValue("$s", item.Status.ToString()); cmd.Parameters.AddWithValue("$p", item.Progress); cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O")); item.Id = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    }

    public static async Task UpdateDownloadAsync(DownloadItem item)
    {
        await using var c = new SqliteConnection(ConnectionString); await c.OpenAsync(); await using var cmd = c.CreateCommand(); cmd.CommandText = "UPDATE Downloads SET Status=$s,Progress=$p WHERE Id=$id"; cmd.Parameters.AddWithValue("$s", item.Status.ToString()); cmd.Parameters.AddWithValue("$p", item.Progress); cmd.Parameters.AddWithValue("$id", item.Id); await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<List<LibraryTrack>> LoadLibraryAsync(string search = "")
    {
        var list = new List<LibraryTrack>(); await using var c = new SqliteConnection(ConnectionString); await c.OpenAsync(); await using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT Id,Path,Title,Artist,Album,Genre,Year,IsFavorite FROM Library WHERE Title LIKE $q OR Artist LIKE $q OR Album LIKE $q ORDER BY Title"; cmd.Parameters.AddWithValue("$q", $"%{search}%"); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) list.Add(new LibraryTrack { Id=r.GetInt64(0), Path=r.GetString(1), Title=r.GetString(2), Artist=r.GetString(3), Album=r.GetString(4), Genre=r.GetString(5), Year=r.IsDBNull(6)?null:r.GetInt32(6), IsFavorite=r.GetBoolean(7) }); return list;
    }

    public static async Task ImportTrackAsync(string path)
    {
        await using var c = new SqliteConnection(ConnectionString); await c.OpenAsync(); await using var cmd = c.CreateCommand(); cmd.CommandText = "INSERT OR IGNORE INTO Library(Path,Title,Artist,Album,AddedUtc) VALUES($p,$t,'Desconhecido','Desconhecido',$d)"; cmd.Parameters.AddWithValue("$p", Path.GetFullPath(path)); cmd.Parameters.AddWithValue("$t", Path.GetFileNameWithoutExtension(path)); cmd.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("O")); await cmd.ExecuteNonQueryAsync();
    }

    public static async Task ToggleFavoriteAsync(long id, bool favorite) { await using var c = new SqliteConnection(ConnectionString); await c.OpenAsync(); await using var cmd = c.CreateCommand(); cmd.CommandText="UPDATE Library SET IsFavorite=$f WHERE Id=$id"; cmd.Parameters.AddWithValue("$f", favorite?1:0); cmd.Parameters.AddWithValue("$id",id); await cmd.ExecuteNonQueryAsync(); }
}

public sealed class DirectDownloadService
{
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    public async Task<AudioMetadata> AnalyzeAsync(string value, CancellationToken token)
    {
        if (!SecurityService.IsSafePublicHttpsUrl(value, out var uri) || uri is null || !SecurityService.HasAllowedAudioExtension(uri.AbsolutePath) || !await SecurityService.ResolvesToPublicAddressAsync(uri, token)) throw new InvalidOperationException("Esta fonte não disponibiliza um método de download autorizado.");
        using var request = new HttpRequestMessage(HttpMethod.Head, uri); using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Esta fonte não disponibiliza um método de download autorizado.");
        var media = response.Content.Headers.ContentType?.MediaType ?? ""; if (!media.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) && media != "application/octet-stream") throw new InvalidOperationException("Esta fonte não disponibiliza um método de download autorizado.");
        return new AudioMetadata { Name=Path.GetFileNameWithoutExtension(uri.LocalPath), Source=uri.Host, Format=Path.GetExtension(uri.LocalPath).TrimStart('.').ToUpperInvariant(), Size=response.Content.Headers.ContentLength, SupportsResume=response.Headers.AcceptRanges.Contains("bytes") };
    }

    public async Task DownloadAsync(string value, string destination, IProgress<double> progress, CancellationToken token)
    {
        if (!SecurityService.IsSafePublicHttpsUrl(value, out var uri) || uri is null || !SecurityService.HasAllowedAudioExtension(uri.AbsolutePath) || !await SecurityService.ResolvesToPublicAddressAsync(uri, token)) throw new InvalidOperationException("Esta fonte não disponibiliza um método de download autorizado.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!); var temporary = destination + ".part";
        using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode(); var length=response.Content.Headers.ContentLength;
        await using var input=await response.Content.ReadAsStreamAsync(token); await using var output=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None,81920,true); var buffer=new byte[81920]; long total=0; int read; while((read=await input.ReadAsync(buffer,token))>0){await output.WriteAsync(buffer.AsMemory(0,read),token); total+=read; if(length>0)progress.Report(total*100d/length.Value);} await output.FlushAsync(token); File.Move(temporary,destination,true);
    }
}

public sealed class LocalAudioPlayer
{
    private readonly MediaPlayer _player = new(); public string? CurrentPath { get; private set; }
    public void Load(string path) { if(!File.Exists(path)||!SecurityService.HasAllowedAudioExtension(path)) throw new FileNotFoundException("Arquivo de áudio inválido."); CurrentPath=path; _player.Open(new Uri(path)); }
    public void Play()=>_player.Play(); public void Pause()=>_player.Pause(); public void Stop()=>_player.Stop(); public double Volume { get=>_player.Volume; set=>_player.Volume=Math.Clamp(value,0,1); }
}

public sealed class AudioConverterService
{
    public async Task ConvertAsync(string ffmpegPath,string input,string output,string format,IProgress<double> progress,CancellationToken token)
    {
        if(!File.Exists(ffmpegPath))throw new FileNotFoundException("Selecione uma instalação válida do FFmpeg."); if(!File.Exists(input)||!SecurityService.HasAllowedAudioExtension(input))throw new InvalidDataException("Arquivo de entrada inválido."); if(!new[]{"mp3","wav","flac","m4a","ogg"}.Contains(format))throw new InvalidDataException("Formato de saída inválido.");
        var temp=Path.Combine(Path.GetDirectoryName(output)!,Path.GetFileNameWithoutExtension(output)+".part."+format); var psi=new ProcessStartInfo(ffmpegPath){UseShellExecute=false,RedirectStandardError=true,RedirectStandardOutput=true,CreateNoWindow=true}; psi.ArgumentList.Add("-nostdin"); psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(input); psi.ArgumentList.Add("-vn"); psi.ArgumentList.Add("-progress"); psi.ArgumentList.Add("pipe:1"); psi.ArgumentList.Add("-y"); psi.ArgumentList.Add(temp);
        using var process=Process.Start(psi)??throw new InvalidOperationException("Não foi possível iniciar o FFmpeg."); using var registration=token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch{}}); while(!process.StandardOutput.EndOfStream){token.ThrowIfCancellationRequested(); var line=await process.StandardOutput.ReadLineAsync(token); if(line?.StartsWith("progress=")==true)progress.Report(line.EndsWith("end")?100:50);} await process.WaitForExitAsync(token); if(process.ExitCode!=0){if(File.Exists(temp))File.Delete(temp);throw new InvalidOperationException("A conversão falhou. Consulte os logs.");} File.Move(temp,output,true);
    }
}

public sealed class AppSettings
{
    public string DownloadFolder { get; set; } = AppPaths.DefaultMusic; public int MaxSimultaneousDownloads { get; set; } = 3; public bool AskBeforeReplace { get; set; } = true; public bool CreateSubfolders { get; set; } = true; public bool NotifyCompleted { get; set; } = true; public string Theme { get; set; } = "Escuro"; public string Language { get; set; } = "Português-BR"; public string FfmpegPath { get; set; } = "";
    public static async Task<AppSettings> LoadAsync(){try{if(File.Exists(AppPaths.Settings))return JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(AppPaths.Settings))??new();}catch(Exception ex){LogService.Error(ex);}return new();}
    public Task SaveAsync(){Directory.CreateDirectory(AppPaths.Root);MaxSimultaneousDownloads=Math.Clamp(MaxSimultaneousDownloads,1,10);return File.WriteAllTextAsync(AppPaths.Settings,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));}
}
