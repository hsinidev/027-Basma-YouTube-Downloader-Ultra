using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using BasmaYouTubeDownloaderUltra.Helpers;
using BasmaYouTubeDownloaderUltra.Models;
using BasmaYouTubeDownloaderUltra.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasmaYouTubeDownloaderUltra.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IYouTubeDownloaderService _downloaderService;
        private readonly IFFmpegService _ffmpegService;
        private readonly IArchiveTrackerService _archiveTracker;
        private readonly Channel<DownloadProgressReport> _telemetryChannel;
        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private string _urlInput = string.Empty;

        [ObservableProperty]
        private ObservableCollection<MediaItemViewModel> _queue = new();

        [ObservableProperty]
        private string _statusText = "Ready for workstation links";

        [ObservableProperty]
        private string _totalSpeedFormatted = "0 KB/s";

        [ObservableProperty]
        private int _activeWorkersCount;

        [ObservableProperty]
        private int _completedCount;

        [ObservableProperty]
        private int _skippedCount;

        [ObservableProperty]
        private int _failedCount;

        [ObservableProperty]
        private int _totalQueueCount;

        [ObservableProperty]
        private int _archiveCount;

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private bool _isFFmpegInstalled;

        [ObservableProperty]
        private bool _isYtDlpInstalled;

        [ObservableProperty]
        private DownloadSettings _settings = new();

        [ObservableProperty]
        private string _selectedGlobalFormat = "MP4 Video (4K/8K)";

        partial void OnSelectedGlobalFormatChanged(string value)
        {
            foreach (var item in Queue)
            {
                if (item.Status == DownloadStatus.Pending)
                {
                    item.SelectedFormat = value;
                }
            }
        }

        public List<string> AvailableFormats { get; } = new()
        {
            "MP4 Video (4K/8K)",
            "MP4 Video (1080p)",
            "MP4 Video (720p)",
            "MP3 Audio (320 kbps)",
            "FLAC Audio (Lossless)",
            "AAC Audio (256 kbps)"
        };

        public MainViewModel(
            IYouTubeDownloaderService downloaderService,
            IFFmpegService ffmpegService,
            IArchiveTrackerService archiveTracker)
        {
            _downloaderService = downloaderService;
            _ffmpegService = ffmpegService;
            _archiveTracker = archiveTracker;

            _telemetryChannel = Channel.CreateUnbounded<DownloadProgressReport>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });

            // Start telemetry reader loop
            Task.Run(ReadTelemetryChannelAsync);

            // Initialize binaries & archive status
            Task.Run(InitializeWorkstationAsync);
        }

        private async Task InitializeWorkstationAsync()
        {
            await _ffmpegService.DiscoverBinariesAsync();
            await _archiveTracker.InitializeAsync();

            IsFFmpegInstalled = _ffmpegService.IsFFmpegAvailable;
            IsYtDlpInstalled = _ffmpegService.IsYtDlpAvailable;
            ArchiveCount = await _archiveTracker.GetArchivedCountAsync();
        }

        [RelayCommand]
        private async Task AnalyzeUrlAsync()
        {
            if (string.IsNullOrWhiteSpace(UrlInput))
            {
                StatusText = "Please enter a valid YouTube video, playlist, or channel URL.";
                return;
            }

            IsAnalyzing = true;
            StatusText = "Analyzing media link...";

            try
            {
                var items = await _downloaderService.AnalyzeUrlAsync(UrlInput);
                if (items.Count == 0)
                {
                    StatusText = "No videos or streams found for this URL.";
                    return;
                }

                foreach (var item in items)
                {
                    // Check if already in queue
                    if (!Queue.Any(q => q.Id == item.Id))
                    {
                        item.SelectedFormat = SelectedGlobalFormat;
                        var vm = new MediaItemViewModel(item);
                        Queue.Add(vm);
                    }
                }

                UpdateQueueCounts();
                StatusText = $"Loaded {items.Count} media item(s) into workstation queue.";
                UrlInput = string.Empty;
            }
            catch (Exception ex)
            {
                StatusText = $"Analysis Error: {ex.Message}";
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        private async Task StartDownloadQueueAsync()
        {
            var pendingItems = Queue.Where(q => q.IsSelected && (q.Status == DownloadStatus.Pending || q.Status == DownloadStatus.Failed)).ToList();
            if (pendingItems.Count == 0)
            {
                StatusText = "No pending items selected for download.";
                return;
            }

            IsDownloading = true;
            _cts = new CancellationTokenSource();
            StatusText = $"Processing download queue ({pendingItems.Count} item(s))...";

            var semaphore = new SemaphoreSlim(Math.Max(1, Settings.MaxConcurrentDownloads));
            var tasks = new List<Task>();

            foreach (var item in pendingItems)
            {
                await semaphore.WaitAsync(_cts.Token);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        ActiveWorkersCount++;
                        await _downloaderService.DownloadItemAsync(
                            item.Model,
                            Settings,
                            _telemetryChannel.Writer,
                            _cts.Token
                        );
                    }
                    finally
                    {
                        ActiveWorkersCount--;
                        semaphore.Release();
                    }
                }, _cts.Token));
            }

            try
            {
                await Task.WhenAll(tasks);
                StatusText = "Download queue completed.";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Download queue cancelled by user.";
            }
            catch (Exception ex)
            {
                StatusText = $"Queue Error: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
                ActiveWorkersCount = 0;
                TotalSpeedFormatted = "0 KB/s";
                ArchiveCount = await _archiveTracker.GetArchivedCountAsync();
                UpdateQueueCounts();
            }
        }

        [RelayCommand]
        private void CancelQueue()
        {
            _cts?.Cancel();
            StatusText = "Cancelling active download worker threads...";
        }

        [RelayCommand]
        private void ClearQueue()
        {
            if (IsDownloading) return;
            Queue.Clear();
            UpdateQueueCounts();
            StatusText = "Workstation queue cleared.";
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in Queue)
                item.IsSelected = true;
        }

        [RelayCommand]
        private void UnselectAll()
        {
            foreach (var item in Queue)
                item.IsSelected = false;
        }

        [RelayCommand]
        private void BrowseOutputFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Basma Downloader Output Directory",
                InitialDirectory = Settings.OutputFolder
            };

            if (dialog.ShowDialog() == true)
            {
                Settings.OutputFolder = dialog.FolderName;
                OnPropertyChanged(nameof(Settings));
                StatusText = $"Output folder set to: {Settings.OutputFolder}";
            }
        }

        [RelayCommand]
        private void OpenOutputFolder()
        {
            try
            {
                Directory.CreateDirectory(Settings.OutputFolder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = Settings.OutputFolder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Failed to open folder: {ex.Message}";
            }
        }

        [RelayCommand]
        private void RemoveItem(MediaItemViewModel item)
        {
            if (item != null && Queue.Contains(item))
            {
                Queue.Remove(item);
                UpdateQueueCounts();
            }
        }

        [RelayCommand]
        private void OpenDownloadedFile(MediaItemViewModel item)
        {
            if (item != null && !string.IsNullOrEmpty(item.OutputFilePath) && File.Exists(item.OutputFilePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.OutputFilePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    StatusText = $"Failed to play file: {ex.Message}";
                }
            }
        }

        private async Task ReadTelemetryChannelAsync()
        {
            var reader = _telemetryChannel.Reader;
            double aggregateSpeed = 0;

            while (await reader.WaitToReadAsync())
            {
                aggregateSpeed = 0;
                while (reader.TryRead(out var report))
                {
                    aggregateSpeed += report.CurrentSpeedBytesPerSec;

                    // Dispatch progress update to UI thread safely
                    if (Application.Current != null)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var target = Queue.FirstOrDefault(q => q.Id == report.MediaItemId);
                            target?.UpdateProgress(report);
                            UpdateQueueCounts();
                        });
                    }
                }

                if (Application.Current != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        TotalSpeedFormatted = FileSizeFormatter.FormatSpeed(aggregateSpeed);
                    });
                }
            }
        }

        private void UpdateQueueCounts()
        {
            TotalQueueCount = Queue.Count;
            CompletedCount = Queue.Count(q => q.Status == DownloadStatus.Completed);
            SkippedCount = Queue.Count(q => q.Status == DownloadStatus.Skipped);
            FailedCount = Queue.Count(q => q.Status == DownloadStatus.Failed);
        }
    }
}
