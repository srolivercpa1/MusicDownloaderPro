using System.IO;
using System.Windows;
using MusicDownloaderPro.Services;

namespace MusicDownloaderPro;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogService.Error(args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) => { LogService.Error(args.Exception); MessageBox.Show("Ocorreu um erro inesperado. Consulte os logs.", "MusicDownloader Pro"); args.Handled = true; };
        Directory.CreateDirectory(AppPaths.Root);
        Directory.CreateDirectory(AppPaths.Logs);
        await DatabaseService.InitializeAsync();
        new MainWindow().Show();
    }
}
