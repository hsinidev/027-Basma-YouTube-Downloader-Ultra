using System;
using System.Windows;
using BasmaYouTubeDownloaderUltra.Services;
using BasmaYouTubeDownloaderUltra.ViewModels;

namespace BasmaYouTubeDownloaderUltra
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Dependency Injection & Services Setup
            IFFmpegService ffmpegService = new FFmpegService();
            IMetadataTaggingService metadataService = new MetadataTaggingService();
            IArchiveTrackerService archiveTracker = new ArchiveTrackerService();

            IYouTubeDownloaderService downloaderService = new YouTubeDownloaderService(
                ffmpegService,
                metadataService,
                archiveTracker
            );

            MainViewModel mainViewModel = new MainViewModel(
                downloaderService,
                ffmpegService,
                archiveTracker
            );

            MainWindow mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }
    }
}
