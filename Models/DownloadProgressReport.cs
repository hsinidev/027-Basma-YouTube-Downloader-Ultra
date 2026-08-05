using System;

namespace BasmaYouTubeDownloaderUltra.Models
{
    public class DownloadProgressReport
    {
        public string MediaItemId { get; set; } = string.Empty;
        public long BytesTransferred { get; set; }
        public long TotalBytes { get; set; }
        public double CurrentSpeedBytesPerSec { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public double ProgressPercent { get; set; }
        public DownloadStatus Status { get; set; } = DownloadStatus.Downloading;
        public bool IsCompleted { get; set; }
        public bool IsFailed { get; set; }
        public string? ErrorMessage { get; set; }
        public string? OutputFilePath { get; set; }
    }
}
