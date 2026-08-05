using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BasmaYouTubeDownloaderUltra.Services
{
    public class ArchiveTrackerService : IArchiveTrackerService
    {
        private readonly HashSet<string> _archivedIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        public string ArchiveFilePath { get; set; } = "downloaded_archive.txt";

        public async Task InitializeAsync(string? customPath = null)
        {
            if (!string.IsNullOrWhiteSpace(customPath))
            {
                ArchiveFilePath = customPath;
            }

            await _fileLock.WaitAsync();
            try
            {
                _archivedIds.Clear();
                if (File.Exists(ArchiveFilePath))
                {
                    string[] lines = await File.ReadAllLinesAsync(ArchiveFilePath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        // yt-dlp archive format is: "youtube <video_id>" or just "<video_id>"
                        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        string videoId = parts.Length > 1 ? parts[1] : parts[0];
                        _archivedIds.Add(videoId);
                    }
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public bool IsArchived(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return false;
            lock (_archivedIds)
            {
                return _archivedIds.Contains(videoId);
            }
        }

        public async Task RecordDownloadedAsync(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return;

            await _fileLock.WaitAsync();
            try
            {
                if (!_archivedIds.Contains(videoId))
                {
                    _archivedIds.Add(videoId);
                    string entry = $"youtube {videoId}{Environment.NewLine}";
                    await File.AppendAllTextAsync(ArchiveFilePath, entry);
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public Task<int> GetArchivedCountAsync()
        {
            lock (_archivedIds)
            {
                return Task.FromResult(_archivedIds.Count);
            }
        }
    }
}
