namespace BasmaYouTubeDownloaderUltra.Models
{
    public enum DownloadStatus
    {
        Pending,
        Parsing,
        Downloading,
        Muxing,
        Tagging,
        Completed,
        Failed,
        Skipped,
        Cancelled
    }
}
