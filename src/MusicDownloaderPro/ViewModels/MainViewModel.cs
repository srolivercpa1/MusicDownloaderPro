using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicDownloaderPro.Models;
using MusicDownloaderPro.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace MusicDownloaderPro.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DirectDownloadService _downloads = new();
    private readonly LocalAudioPlayer _player = new();
    private readonly AudioConverterService _converter = new();
    private CancellationTokenSource? _operation;
    private CancellationTokenSource? _searchOperation;
    [ObservableProperty] private string searchMessage = "Busque pelo nome da música, artista ou álbum.";
    public int ActiveDownloadCount => DownloadItems.Count(x => x.Status is DownloadStatus.Downloading or DownloadStatus.Queued);
    public int CompletedDownloadCount => DownloadItems.Count(x => x.Status == DownloadStatus.Completed);
    [ObservableProperty] private string librarySizeText = "0 MB";
    private void RefreshMetrics() { OnPropertyChanged(nameof(ActiveDownloadCount)); OnPropertyChanged(nameof(CompletedDownloadCount)); }

    [ObservableProperty] private string currentPage = "Início";
    [ObservableProperty] private string url = "";
    [ObservableProperty] private string statusMessage = "Pronto";
    [ObservableProperty] private AudioMetadata? analyzedAudio;
    [ObservableProperty] private double currentProgress;
    [ObservableProperty] private string librarySearch = "";
    [ObservableProperty] private LibraryTrack? selectedTrack;
    [ObservableProperty] private string conversionInput = "";
    [ObservableProperty] private string conversionFormat = "mp3";
    [ObservableProperty] private string onlineQuery = "";
    [ObservableProperty] private OnlineMusicResult? selectedOnlineResult;
    [ObservableProperty] private bool isSearchingOnline;
    [ObservableProperty] private AppSettings settings = new();
    public ObservableCollection<DownloadItem> DownloadItems { get; } = [];
    public ObservableCollection<LibraryTrack> Library { get; } = [];
    public ObservableCollection<OnlineMusicResult> OnlineResults { get; } = [];
    public string[] Pages { get; } = ["Início","Busca Online","Novo Download","Downloads","Biblioteca","Conversor","Histórico","Favoritos","Configurações","Sobre"];
    public string[] Formats { get; } = ["mp3","wav","flac","m4a","ogg"];

    public MainViewModel() => _ = InitializeAsync();
    private async Task InitializeAsync(){try { Settings=await AppSettings.LoadAsync();await RefreshLibraryAsync(); } catch(Exception ex) { StatusMessage="Não foi possível carregar a biblioteca. Consulte os logs."; LogService.Error(ex); }}

    [RelayCommand] private void Navigate(string? page){if(!string.IsNullOrWhiteSpace(page))CurrentPage=page;}
    [RelayCommand] private async Task SearchOnlineAsync()
    {
        CurrentPage = "Busca Online";
        if (string.IsNullOrWhiteSpace(OnlineQuery))
        {
            SearchMessage = "Digite o nome de uma música, artista ou álbum.";
            return;
        }
        _searchOperation?.Cancel();
        using var current = new CancellationTokenSource();
        _searchOperation = current;
        IsSearchingOnline = true;
        SearchMessage = "Buscando músicas autorizadas…";
        OnlineResults.Clear();
        try
        {
            var service = new OnlineMusicSearchService([new InternetArchiveMusicProvider()]);
            var response = await service.SearchAsync(OnlineQuery, current.Token);
            current.Token.ThrowIfCancellationRequested();
            if (current != _searchOperation) return;
            foreach (var result in response.Results) OnlineResults.Add(result);
            SearchMessage = response.Failures.Count > 0
                ? (OnlineResults.Count > 0 ? $"{OnlineResults.Count} resultado(s). Algumas fontes ficaram indisponíveis." : "Não foi possível consultar as fontes. Verifique sua conexão e pesquise novamente.")
                : (OnlineResults.Count == 0 ? "Nenhuma música com download autorizado foi encontrada." : $"{OnlineResults.Count} resultado(s) encontrado(s).");
        }
        catch (OperationCanceledException)
        {
            if (current == _searchOperation) SearchMessage = "Busca cancelada. Você pode pesquisar novamente.";
        }
        catch (Exception ex)
        {
            if (current == _searchOperation) SearchMessage = "Não foi possível concluir a busca. Verifique sua conexão.";
            LogService.Error(ex);
        }
        finally
        {
            if (current == _searchOperation) { IsSearchingOnline = false; _searchOperation = null; }
        }
    }
    [RelayCommand] private void CancelSearch() => _searchOperation?.Cancel();
    [RelayCommand] private async Task DownloadOnlineResultAsync(OnlineMusicResult? result)
    {
        if (result is not null) SelectedOnlineResult = result;
        var option=SelectedOnlineResult?.DownloadOptions.FirstOrDefault(); if(option is null){StatusMessage="Esta fonte não disponibiliza um método de download autorizado.";return;}
        Url=option.Url.AbsoluteUri; AnalyzedAudio=new AudioMetadata{Name=Path.GetFileNameWithoutExtension(option.FileName),Artist=SelectedOnlineResult!.Artist,Album=SelectedOnlineResult.Album,Source=SelectedOnlineResult.Provider,Format=option.Format,Size=option.Size,SupportsResume=true};
        await StartDownloadAsync();
    }
    [RelayCommand] private void OpenOnlineSource(OnlineMusicResult? result)
    {
        if (result is not null) SelectedOnlineResult = result;
        if (SelectedOnlineResult is null) return;
        try
        {
            if (!SecurityService.IsSafePublicHttpsUrl(SelectedOnlineResult.SourcePage.AbsoluteUri, out _)) return;
            Process.Start(new ProcessStartInfo(SelectedOnlineResult.SourcePage.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) { StatusMessage = "Não foi possível abrir a fonte no navegador."; LogService.Error(ex); }
    }
    [RelayCommand] private async Task AnalyzeAsync()
    {
        try{_operation?.Cancel();_operation=new();StatusMessage="Analisando fonte autorizada...";AnalyzedAudio=await _downloads.AnalyzeAsync(Url,_operation.Token);StatusMessage="Fonte autorizada pronta para download.";}
        catch(Exception ex){AnalyzedAudio=null;StatusMessage=ex.Message;}
    }
    [RelayCommand(CanExecute=nameof(CanStartDownload))] private async Task StartDownloadAsync()
    {
        if(AnalyzedAudio is null)return; var extension="."+AnalyzedAudio.Format.ToLowerInvariant(); var destination=SecurityService.CreateSafeDestination(Settings.DownloadFolder,AnalyzedAudio.Name,extension); if(File.Exists(destination)&&Settings.AskBeforeReplace&&MessageBox.Show("O arquivo já existe. Deseja substituir?","MusicDownloader Pro",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        var item=new DownloadItem{Url=Url,Name=AnalyzedAudio.Name,Destination=destination,Status=DownloadStatus.Queued};DownloadItems.Add(item);RefreshMetrics();await DatabaseService.AddDownloadAsync(item);CurrentPage="Downloads";_operation=new();
        try{item.Status=DownloadStatus.Downloading;RefreshMetrics();StatusMessage="Baixando...";var progress=new Progress<double>(p=>{item.Progress=p;CurrentProgress=p;OnPropertyChanged(nameof(DownloadItems));});await _downloads.DownloadAsync(Url,destination,progress,_operation.Token);item.Progress=100;item.Status=DownloadStatus.Completed;StatusMessage="Download concluído.";await DatabaseService.ImportTrackAsync(destination);await RefreshLibraryAsync();}
        catch(OperationCanceledException){item.Status=DownloadStatus.Cancelled;StatusMessage="Download cancelado.";}
        catch(Exception ex){item.Status=DownloadStatus.Failed;StatusMessage=ex.Message;LogService.Error(ex);}finally{await DatabaseService.UpdateDownloadAsync(item);OnPropertyChanged(nameof(DownloadItems));RefreshMetrics();}
    }
    private bool CanStartDownload()=>AnalyzedAudio is not null;
    partial void OnAnalyzedAudioChanged(AudioMetadata? value)=>StartDownloadCommand.NotifyCanExecuteChanged();
    [RelayCommand] private void Cancel()=>_operation?.Cancel();
    [RelayCommand] private async Task ImportFilesAsync(){var dialog=new OpenFileDialog{Multiselect=true,Filter="Áudios|*.mp3;*.wav;*.flac;*.m4a;*.ogg"};if(dialog.ShowDialog()!=true)return;foreach(var file in dialog.FileNames)await DatabaseService.ImportTrackAsync(file);await RefreshLibraryAsync();}
    [RelayCommand] private async Task RefreshLibraryAsync()
    {
        Library.Clear();
        foreach (var track in await DatabaseService.LoadLibraryAsync(LibrarySearch)) Library.Add(track);
        var paths = Library.Select(x => x.Path).ToArray();
        var bytes = await Task.Run(() => paths.Sum(path => { try { return new FileInfo(path).Length; } catch (IOException) { return 0L; } catch (UnauthorizedAccessException) { return 0L; } }));
        LibrarySizeText = bytes >= 1073741824 ? $"{bytes / 1073741824d:F1} GB" : $"{bytes / 1048576d:F1} MB";
    }
    [RelayCommand] private void Play(){if(SelectedTrack is null)return;_player.Load(SelectedTrack.Path);_player.Play();StatusMessage=$"Reproduzindo: {SelectedTrack.Title}";}
    [RelayCommand] private void Pause()=>_player.Pause();
    [RelayCommand] private async Task ToggleFavoriteAsync(){if(SelectedTrack is null)return;SelectedTrack.IsFavorite=!SelectedTrack.IsFavorite;await DatabaseService.ToggleFavoriteAsync(SelectedTrack.Id,SelectedTrack.IsFavorite);await RefreshLibraryAsync();}
    [RelayCommand] private void OpenFolder(){var path=SelectedTrack?.Path??Settings.DownloadFolder;var folder=File.Exists(path)?Path.GetDirectoryName(path)!:path;if(Directory.Exists(folder))Process.Start(new ProcessStartInfo("explorer.exe",folder){UseShellExecute=true});}
    [RelayCommand] private void SelectConversionInput(){var dialog=new OpenFileDialog{Filter="Áudios|*.mp3;*.wav;*.flac;*.m4a;*.ogg"};if(dialog.ShowDialog()==true)ConversionInput=dialog.FileName;}
    [RelayCommand] private async Task ConvertAsync(){if(string.IsNullOrWhiteSpace(ConversionInput))return;var output=Path.Combine(Settings.DownloadFolder,Path.GetFileNameWithoutExtension(ConversionInput)+"."+ConversionFormat);_operation=new();try{StatusMessage="Convertendo...";await _converter.ConvertAsync(Settings.FfmpegPath,ConversionInput,output,ConversionFormat,new Progress<double>(p=>CurrentProgress=p),_operation.Token);StatusMessage="Conversão concluída.";await DatabaseService.ImportTrackAsync(output);await RefreshLibraryAsync();}catch(Exception ex){StatusMessage=ex.Message;LogService.Error(ex);}}
    [RelayCommand] private async Task SaveSettingsAsync(){await Settings.SaveAsync();StatusMessage="Configurações salvas.";}
}
