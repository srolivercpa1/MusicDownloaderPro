namespace MusicDownloaderPro.Models;

public enum DownloadStatus { Queued, Analyzing, Downloading, Paused, Completed, Failed, Cancelled }

public sealed class AudioMetadata
{
    public string Name { get; init; } = "Não informado";
    public string Artist { get; init; } = "Não informado";
    public string Album { get; init; } = "Não informado";
    public string Source { get; init; } = "";
    public string Format { get; init; } = "";
    public long? Size { get; init; }
    public bool SupportsResume { get; init; }
}

public sealed class DownloadItem
{
    public long Id { get; set; }
    public string Url { get; set; } = "";
    public string Name { get; set; } = "";
    public string Destination { get; set; } = "";
    public DownloadStatus Status { get; set; }
    public double Progress { get; set; }
    public string StatusText => Status switch { DownloadStatus.Queued => "Na fila", DownloadStatus.Analyzing => "Analisando", DownloadStatus.Downloading => "Baixando", DownloadStatus.Paused => "Pausado", DownloadStatus.Completed => "Concluído", DownloadStatus.Failed => "Falhou", DownloadStatus.Cancelled => "Cancelado", _ => Status.ToString() };
}

public sealed class LibraryTrack
{
    public long Id { get; set; }
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "Desconhecido";
    public string Album { get; set; } = "Desconhecido";
    public string Genre { get; set; } = "";
    public int? Year { get; set; }
    public bool IsFavorite { get; set; }
}
