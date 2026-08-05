using System;
using System.IO;

namespace BasmaYouTubeDownloaderUltra.Models
{
    public class DownloadSettings
    {
        public string OutputFolder { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "BasmaDownloads"
        );

        public int MaxConcurrentDownloads { get; set; } = 3;
        public bool AutoEmbedMetadata { get; set; } = true;
        public bool AutoEmbedThumbnail { get; set; } = true;
        public bool DownloadSubtitles { get; set; } = false;
        public string SubtitleLanguage { get; set; } = "en";
        public bool UseArchiveTracker { get; set; } = true;
        public string ArchiveFilePath { get; set; } = "downloaded_archive.txt";
        public string DefaultFormat { get; set; } = "MP4 Video (4K/8K)";
        public string AudioFormat { get; set; } = "MP3 Audio (320 kbps)";
        public string VideoFormat { get; set; } = "Best Available (4K/8K)";
        public string? FFmpegPath { get; set; }
        public string? YtDlpPath { get; set; }
    }
}
