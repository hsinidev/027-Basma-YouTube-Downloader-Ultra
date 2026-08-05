using System;

using BasmaYouTubeDownloaderUltra.Helpers;
using BasmaYouTubeDownloaderUltra.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BasmaYouTubeDownloaderUltra.ViewModels
{
    public partial class MediaItemViewModel : ObservableObject
    {
        public MediaItem Model { get; }

        public string Id => Model.Id;
        public string Url => Model.Url;
        public string Title => Model.Title;
        public string Author => Model.Author;
        public string ThumbnailUrl => Model.ThumbnailUrl;

        public string DurationFormatted => Model.Duration.HasValue
            ? FileSizeFormatter.FormatTimeSpan(Model.Duration.Value)
            : "--:--";

        [ObservableProperty]
        private string _selectedFormat = "MP4 Video (4K/8K)";

        partial void OnSelectedFormatChanged(string value)
        {
            Model.SelectedFormat = value;
        }

        [ObservableProperty]
        private string _selectedQuality = "Best Available";

        partial void OnSelectedQualityChanged(string value)
        {
            Model.SelectedQuality = value;
        }

        [ObservableProperty]
        private DownloadStatus _status = DownloadStatus.Pending;

        partial void OnStatusChanged(DownloadStatus value)
        {
            Model.Status = value;
            OnPropertyChanged(nameof(StatusBadgeColor));
        }

        [ObservableProperty]
        private string _statusText = "Pending in queue";

        [ObservableProperty]
        private double _progressPercent;

        [ObservableProperty]
        private string _speedFormatted = "0 KB/s";

        [ObservableProperty]
        private string _etaFormatted = "00:00";

        [ObservableProperty]
        private string _bytesFormatted = "0 B / 0 B";

        [ObservableProperty]
        private bool _isSelected = true;

        partial void OnIsSelectedChanged(bool value)
        {
            Model.IsSelected = value;
        }

        [ObservableProperty]
        private string _outputFilePath = string.Empty;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _hasSubtitles;

        public string StatusBadgeColor => Status switch
        {
            DownloadStatus.Pending => "#94A3B8",
            DownloadStatus.Parsing => "#F59E0B",
            DownloadStatus.Downloading => "#E11D48",
            DownloadStatus.Muxing => "#8B5CF6",
            DownloadStatus.Tagging => "#3B82F6",
            DownloadStatus.Completed => "#10B981",
            DownloadStatus.Failed => "#EF4444",
            DownloadStatus.Skipped => "#64748B",
            DownloadStatus.Cancelled => "#64748B",
            _ => "#94A3B8"
        };

        public MediaItemViewModel(MediaItem model)
        {
            Model = model;
            _selectedFormat = model.SelectedFormat;
            _selectedQuality = model.SelectedQuality;
            _status = model.Status;
            _statusText = model.StatusText;
            _progressPercent = model.ProgressPercent;
            _isSelected = model.IsSelected;
            _outputFilePath = model.OutputFilePath;
            _errorMessage = model.ErrorMessage;
            _hasSubtitles = model.HasSubtitles;
        }

        public void UpdateProgress(DownloadProgressReport report)
        {
            Status = report.Status;
            StatusText = report.StatusText;
            ProgressPercent = report.ProgressPercent;
            SpeedFormatted = FileSizeFormatter.FormatSpeed(report.CurrentSpeedBytesPerSec);
            EtaFormatted = FileSizeFormatter.FormatTimeSpan(report.EstimatedTimeRemaining);
            
            if (report.TotalBytes > 0)
            {
                BytesFormatted = $"{FileSizeFormatter.FormatBytes(report.BytesTransferred)} / {FileSizeFormatter.FormatBytes(report.TotalBytes)}";
            }
            else if (report.BytesTransferred > 0)
            {
                BytesFormatted = FileSizeFormatter.FormatBytes(report.BytesTransferred);
            }

            if (!string.IsNullOrEmpty(report.OutputFilePath))
            {
                OutputFilePath = report.OutputFilePath;
            }

            if (report.IsFailed && !string.IsNullOrEmpty(report.ErrorMessage))
            {
                ErrorMessage = report.ErrorMessage;
            }
        }
    }
}
