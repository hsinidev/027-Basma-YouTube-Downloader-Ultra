using System;

namespace BasmaYouTubeDownloaderUltra.Models
{
    public class MediaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = "Unknown Title";
        public string Author { get; set; } = "Unknown Channel";
        public TimeSpan? Duration { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string SelectedFormat { get; set; } = "MP4 Video (4K/8K)";
        public string SelectedQuality { get; set; } = "Best Available";
        public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
        public string StatusText { get; set; } = "Pending in queue";
        public double ProgressPercent { get; set; }
        public long BytesTransferred { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedBytesPerSec { get; set; }
        public TimeSpan Eta { get; set; }
        public string OutputFilePath { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public bool HasSubtitles { get; set; }
        public string SubtitleLanguage { get; set; } = "en";
    }
}
