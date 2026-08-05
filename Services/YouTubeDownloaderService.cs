using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BasmaYouTubeDownloaderUltra.Helpers;
using BasmaYouTubeDownloaderUltra.Models;
using YoutubeDLSharp;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;

namespace BasmaYouTubeDownloaderUltra.Services
{
    public class YouTubeDownloaderService : IYouTubeDownloaderService
    {
        private readonly YoutubeClient _youtubeClient;
        private readonly IFFmpegService _ffmpegService;
        private readonly IMetadataTaggingService _metadataService;
        private readonly IArchiveTrackerService _archiveTracker;
        private static readonly HttpClient _httpClient = new HttpClient();

        public YouTubeDownloaderService(
            IFFmpegService ffmpegService,
            IMetadataTaggingService metadataService,
            IArchiveTrackerService archiveTracker)
        {
            _youtubeClient = new YoutubeClient();
            _ffmpegService = ffmpegService;
            _metadataService = metadataService;
            _archiveTracker = archiveTracker;
        }

        public async Task<List<MediaItem>> AnalyzeUrlAsync(string url, CancellationToken cancellationToken = default)
        {
            var results = new List<MediaItem>();
            url = url.Trim();

            if (string.IsNullOrWhiteSpace(url))
                return results;

            // Initialize archive tracker
            await _archiveTracker.InitializeAsync();

            // Check if URL is playlist
            bool isPlaylist = url.Contains("list=", StringComparison.OrdinalIgnoreCase) ||
                              url.Contains("/playlist?", StringComparison.OrdinalIgnoreCase);

            if (isPlaylist)
            {
                try
                {
                    var playlistVideos = await _youtubeClient.Playlists.GetVideosAsync(url, cancellationToken);
                    foreach (var vid in playlistVideos)
                    {
                        string vidId = vid.Id.Value;
                        bool archived = _archiveTracker.IsArchived(vidId);

                        results.Add(new MediaItem
                        {
                            Id = vidId,
                            Url = vid.Url,
                            Title = vid.Title,
                            Author = vid.Author.ChannelTitle,
                            Duration = vid.Duration,
                            ThumbnailUrl = vid.Thumbnails.GetWithHighestResolution().Url,
                            Status = archived ? DownloadStatus.Skipped : DownloadStatus.Pending,
                            StatusText = archived ? "Skipped (In Archive)" : "Pending in queue",
                            IsSelected = !archived
                        });
                    }

                    if (results.Count > 0)
                        return results;
                }
                catch
                {
                    // Fallback to single video processing if playlist parse fails
                }
            }

            // Single video parse
            try
            {
                var video = await _youtubeClient.Videos.GetAsync(url, cancellationToken);
                string vidId = video.Id.Value;
                bool archived = _archiveTracker.IsArchived(vidId);

                results.Add(new MediaItem
                {
                    Id = vidId,
                    Url = video.Url,
                    Title = video.Title,
                    Author = video.Author.ChannelTitle,
                    Duration = video.Duration,
                    ThumbnailUrl = video.Thumbnails.GetWithHighestResolution().Url,
                    Status = archived ? DownloadStatus.Skipped : DownloadStatus.Pending,
                    StatusText = archived ? "Skipped (In Archive)" : "Pending in queue",
                    IsSelected = !archived
                });
            }
            catch (Exception ex)
            {
                // Fallback attempt using YoutubeDLSharp metadata parsing if available
                if (_ffmpegService.IsYtDlpAvailable && !string.IsNullOrEmpty(_ffmpegService.YtDlpBinaryPath))
                {
                    try
                    {
                        var ytdl = new YoutubeDL
                        {
                            YoutubeDLPath = _ffmpegService.YtDlpBinaryPath,
                            FFmpegPath = _ffmpegService.FFmpegBinaryPath ?? "ffmpeg.exe"
                        };

                        var fetchResult = await ytdl.RunVideoDataFetch(url, cancellationToken);
                        if (fetchResult.Success && fetchResult.Data != null)
                        {
                            var data = fetchResult.Data;
                            string vidId = data.ID ?? Guid.NewGuid().ToString("N");
                            bool archived = _archiveTracker.IsArchived(vidId);

                            results.Add(new MediaItem
                            {
                                Id = vidId,
                                Url = data.Url ?? url,
                                Title = data.Title ?? "YouTube Video",
                                Author = data.Uploader ?? "Unknown Channel",
                                Duration = data.Duration.HasValue ? TimeSpan.FromSeconds(data.Duration.Value) : null,
                                ThumbnailUrl = data.Thumbnail ?? string.Empty,
                                Status = archived ? DownloadStatus.Skipped : DownloadStatus.Pending,
                                StatusText = archived ? "Skipped (In Archive)" : "Pending in queue",
                                IsSelected = !archived
                            });
                        }
                    }
                    catch
                    {
                        throw new Exception($"Failed to parse media link: {ex.Message}");
                    }
                }
                else
                {
                    throw new Exception($"Failed to parse media link: {ex.Message}");
                }
            }

            return results;
        }

        public async Task DownloadItemAsync(
            MediaItem item,
            DownloadSettings settings,
            ChannelWriter<DownloadProgressReport> progressWriter,
            CancellationToken cancellationToken = default)
        {
            var speedometer = new SpeedometerCalculator();

            try
            {
                // Check Archive Tracker
                if (settings.UseArchiveTracker && _archiveTracker.IsArchived(item.Id))
                {
                    item.Status = DownloadStatus.Skipped;
                    item.StatusText = "Skipped (Already in Archive)";
                    await ReportProgressAsync(progressWriter, item, "Skipped (Already in Archive)", 100);
                    return;
                }

                item.Status = DownloadStatus.Downloading;
                item.StatusText = "Fetching streams...";
                await ReportProgressAsync(progressWriter, item, "Fetching stream manifest...", 0);

                Directory.CreateDirectory(settings.OutputFolder);
                string sanitizedTitle = SanitizeFileName(item.Title);

                bool isAudioOnly = item.SelectedFormat.Contains("Audio", StringComparison.OrdinalIgnoreCase) ||
                                   item.SelectedFormat.Contains("MP3", StringComparison.OrdinalIgnoreCase) ||
                                   item.SelectedFormat.Contains("FLAC", StringComparison.OrdinalIgnoreCase) ||
                                   item.SelectedFormat.Contains("AAC", StringComparison.OrdinalIgnoreCase);

                if (isAudioOnly)
                {
                    await DownloadAudioAsync(item, settings, sanitizedTitle, speedometer, progressWriter, cancellationToken);
                }
                else
                {
                    await DownloadVideoAsync(item, settings, sanitizedTitle, speedometer, progressWriter, cancellationToken);
                }

                // Auto Embed Subtitles if requested
                if (settings.DownloadSubtitles)
                {
                    await DownloadSubtitlesAsync(item, settings, sanitizedTitle, cancellationToken);
                }

                // Record in Archive Tracker
                if (settings.UseArchiveTracker)
                {
                    await _archiveTracker.RecordDownloadedAsync(item.Id);
                }

                item.Status = DownloadStatus.Completed;
                item.StatusText = "Completed";
                item.ProgressPercent = 100;
                await ReportProgressAsync(progressWriter, item, "Completed successfully", 100, isCompleted: true);
            }
            catch (OperationCanceledException)
            {
                item.Status = DownloadStatus.Cancelled;
                item.StatusText = "Cancelled";
                await ReportProgressAsync(progressWriter, item, "Cancelled by user", item.ProgressPercent, isFailed: true, error: "Cancelled");
            }
            catch (Exception ex)
            {
                item.Status = DownloadStatus.Failed;
                item.StatusText = $"Failed: {ex.Message}";
                item.ErrorMessage = ex.Message;
                await ReportProgressAsync(progressWriter, item, $"Failed: {ex.Message}", item.ProgressPercent, isFailed: true, error: ex.Message);
            }
        }

        private async Task DownloadVideoAsync(
            MediaItem item,
            DownloadSettings settings,
            string sanitizedTitle,
            SpeedometerCalculator speedometer,
            ChannelWriter<DownloadProgressReport> progressWriter,
            CancellationToken cancellationToken)
        {
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(item.Id, cancellationToken);

            // Select Video Stream (4K, 8K, 1080p, 720p or highest available)
            var videoStreams = streamManifest.GetVideoOnlyStreams().OrderByDescending(s => s.VideoQuality).ToList();
            IVideoStreamInfo? videoStreamInfo = null;

            if (item.SelectedFormat.Contains("4K", StringComparison.OrdinalIgnoreCase) ||
                item.SelectedFormat.Contains("8K", StringComparison.OrdinalIgnoreCase))
            {
                videoStreamInfo = videoStreams.FirstOrDefault();
            }
            else if (item.SelectedFormat.Contains("1080", StringComparison.OrdinalIgnoreCase))
            {
                videoStreamInfo = videoStreams.FirstOrDefault(s => s.VideoQuality.Label.Contains("1080")) ?? videoStreams.FirstOrDefault();
            }
            else if (item.SelectedFormat.Contains("720", StringComparison.OrdinalIgnoreCase))
            {
                videoStreamInfo = videoStreams.FirstOrDefault(s => s.VideoQuality.Label.Contains("720")) ?? videoStreams.FirstOrDefault();
            }
            else
            {
                videoStreamInfo = videoStreams.FirstOrDefault();
            }

            var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            if (videoStreamInfo == null || audioStreamInfo == null)
            {
                // Fallback to muxed stream
                var muxedStream = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
                if (muxedStream != null)
                {
                    string singleOutputPath = Path.Combine(settings.OutputFolder, $"{sanitizedTitle}.mp4");
                    item.OutputFilePath = singleOutputPath;

                    var singleProgress = new Progress<double>(async p =>
                    {
                        item.ProgressPercent = p * 100;
                        item.BytesTransferred = (long)(p * muxedStream.Size.Bytes);
                        item.TotalBytes = muxedStream.Size.Bytes;

                        speedometer.AddSample(item.BytesTransferred);
                        item.SpeedBytesPerSec = speedometer.CalculateSpeedBytesPerSecond();
                        item.Eta = speedometer.CalculateEta(item.BytesTransferred, item.TotalBytes);

                        await ReportProgressAsync(progressWriter, item, $"Downloading: {item.ProgressPercent:0.0}%", item.ProgressPercent);
                    });

                    await _youtubeClient.Videos.Streams.DownloadAsync(muxedStream, singleOutputPath, singleProgress, cancellationToken);
                    
                    if (settings.AutoEmbedMetadata)
                    {
                        await _metadataService.EmbedMetadataAsync(singleOutputPath, item, cancellationToken);
                    }
                    return;
                }

                throw new Exception("No suitable video/audio streams found for this video.");
            }

            // Download Video Stream & Audio Stream into Temp Directory
            string tempDir = Path.Combine(Path.GetTempPath(), "BasmaDownloaderTemp", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            string tempVideoPath = Path.Combine(tempDir, $"video.{videoStreamInfo.Container.Name}");
            string tempAudioPath = Path.Combine(tempDir, $"audio.{audioStreamInfo.Container.Name}");
            string finalOutputPath = Path.Combine(settings.OutputFolder, $"{sanitizedTitle}.mp4");
            item.OutputFilePath = finalOutputPath;

            long totalStreamBytes = videoStreamInfo.Size.Bytes + audioStreamInfo.Size.Bytes;
            item.TotalBytes = totalStreamBytes;

            // 1. Download Video Track (0% - 70%)
            item.StatusText = "Downloading video stream...";
            var videoProgress = new Progress<double>(async p =>
            {
                long currentVideoBytes = (long)(p * videoStreamInfo.Size.Bytes);
                item.BytesTransferred = currentVideoBytes;
                item.ProgressPercent = p * 70;

                speedometer.AddSample(item.BytesTransferred);
                item.SpeedBytesPerSec = speedometer.CalculateSpeedBytesPerSecond();
                item.Eta = speedometer.CalculateEta(item.BytesTransferred, item.TotalBytes);

                await ReportProgressAsync(progressWriter, item, $"Video Stream: {p * 100:0.0}%", item.ProgressPercent);
            });

            await _youtubeClient.Videos.Streams.DownloadAsync(videoStreamInfo, tempVideoPath, videoProgress, cancellationToken);

            // 2. Download Audio Track (70% - 90%)
            item.StatusText = "Downloading audio stream...";
            var audioProgress = new Progress<double>(async p =>
            {
                long currentAudioBytes = (long)(p * audioStreamInfo.Size.Bytes);
                item.BytesTransferred = videoStreamInfo.Size.Bytes + currentAudioBytes;
                item.ProgressPercent = 70 + (p * 20);

                speedometer.AddSample(item.BytesTransferred);
                item.SpeedBytesPerSec = speedometer.CalculateSpeedBytesPerSecond();
                item.Eta = speedometer.CalculateEta(item.BytesTransferred, item.TotalBytes);

                await ReportProgressAsync(progressWriter, item, $"Audio Stream: {p * 100:0.0}%", item.ProgressPercent);
            });

            await _youtubeClient.Videos.Streams.DownloadAsync(audioStreamInfo, tempAudioPath, audioProgress, cancellationToken);

            // 3. Mux Streams using FFmpeg (90% - 98%)
            item.Status = DownloadStatus.Muxing;
            item.StatusText = "Multiplexing video & audio with FFmpeg...";
            item.ProgressPercent = 92;
            await ReportProgressAsync(progressWriter, item, "Multiplexing with FFmpeg...", 92);

            await _ffmpegService.MuxVideoAndAudioAsync(tempVideoPath, tempAudioPath, finalOutputPath, cancellationToken);

            // Cleanup temp files
            try { Directory.Delete(tempDir, true); } catch { }

            // 4. Tag Metadata (98% - 100%)
            if (settings.AutoEmbedMetadata)
            {
                item.Status = DownloadStatus.Tagging;
                item.StatusText = "Injecting metadata & thumbnail artwork...";
                item.ProgressPercent = 98;
                await ReportProgressAsync(progressWriter, item, "Injecting metadata...", 98);

                await _metadataService.EmbedMetadataAsync(finalOutputPath, item, cancellationToken);
            }
        }

        private async Task DownloadAudioAsync(
            MediaItem item,
            DownloadSettings settings,
            string sanitizedTitle,
            SpeedometerCalculator speedometer,
            ChannelWriter<DownloadProgressReport> progressWriter,
            CancellationToken cancellationToken)
        {
            var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(item.Id, cancellationToken);
            var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            if (audioStreamInfo == null)
            {
                throw new Exception("No audio stream found for extraction.");
            }

            string extension = item.SelectedFormat switch
            {
                var f when f.Contains("FLAC", StringComparison.OrdinalIgnoreCase) => "flac",
                var f when f.Contains("AAC", StringComparison.OrdinalIgnoreCase) => "m4a",
                _ => "mp3"
            };

            string tempDir = Path.Combine(Path.GetTempPath(), "BasmaDownloaderTemp", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            string tempRawAudioPath = Path.Combine(tempDir, $"raw_audio.{audioStreamInfo.Container.Name}");
            string finalOutputPath = Path.Combine(settings.OutputFolder, $"{sanitizedTitle}.{extension}");
            item.OutputFilePath = finalOutputPath;
            item.TotalBytes = audioStreamInfo.Size.Bytes;

            // 1. Download raw audio track (0% - 85%)
            item.StatusText = "Downloading high-bitrate audio stream...";
            var audioProgress = new Progress<double>(async p =>
            {
                item.BytesTransferred = (long)(p * audioStreamInfo.Size.Bytes);
                item.ProgressPercent = p * 85;

                speedometer.AddSample(item.BytesTransferred);
                item.SpeedBytesPerSec = speedometer.CalculateSpeedBytesPerSecond();
                item.Eta = speedometer.CalculateEta(item.BytesTransferred, item.TotalBytes);

                await ReportProgressAsync(progressWriter, item, $"Downloading Audio: {p * 100:0.0}%", item.ProgressPercent);
            });

            await _youtubeClient.Videos.Streams.DownloadAsync(audioStreamInfo, tempRawAudioPath, audioProgress, cancellationToken);

            // 2. Convert to Target Format via FFmpeg (85% - 95%)
            item.Status = DownloadStatus.Muxing;
            item.StatusText = $"Converting audio to {extension.ToUpperInvariant()} (320 kbps)...";
            item.ProgressPercent = 88;
            await ReportProgressAsync(progressWriter, item, $"Encoding {extension.ToUpperInvariant()}...", 88);

            await _ffmpegService.ConvertAudioAsync(tempRawAudioPath, finalOutputPath, extension, 320, cancellationToken);

            // Cleanup temp
            try { Directory.Delete(tempDir, true); } catch { }

            // 3. Embed TagLib Metadata & Cover Art (95% - 100%)
            if (settings.AutoEmbedMetadata)
            {
                item.Status = DownloadStatus.Tagging;
                item.StatusText = "Embedding ID3 metadata & high-res cover art...";
                item.ProgressPercent = 96;
                await ReportProgressAsync(progressWriter, item, "Embedding ID3 tags & cover art...", 96);

                await _metadataService.EmbedMetadataAsync(finalOutputPath, item, cancellationToken);
            }
        }

        private async Task DownloadSubtitlesAsync(
            MediaItem item,
            DownloadSettings settings,
            string sanitizedTitle,
            CancellationToken cancellationToken)
        {
            try
            {
                var trackManifest = await _youtubeClient.Videos.ClosedCaptions.GetManifestAsync(item.Id, cancellationToken);
                var trackInfo = trackManifest.Tracks.FirstOrDefault(t => t.Language.Code.Equals(settings.SubtitleLanguage, StringComparison.OrdinalIgnoreCase))
                                ?? trackManifest.Tracks.FirstOrDefault();

                if (trackInfo != null)
                {
                    string subPath = Path.Combine(settings.OutputFolder, $"{sanitizedTitle}.vtt");
                    await _youtubeClient.Videos.ClosedCaptions.DownloadAsync(trackInfo, subPath, null, cancellationToken);
                    item.HasSubtitles = true;
                }
            }
            catch
            {
                // Soft fail for subtitles
            }
        }

        private static async ValueTask ReportProgressAsync(
            ChannelWriter<DownloadProgressReport> progressWriter,
            MediaItem item,
            string statusText,
            double progressPercent,
            bool isCompleted = false,
            bool isFailed = false,
            string? error = null)
        {
            var report = new DownloadProgressReport
            {
                MediaItemId = item.Id,
                BytesTransferred = item.BytesTransferred,
                TotalBytes = item.TotalBytes,
                CurrentSpeedBytesPerSec = item.SpeedBytesPerSec,
                EstimatedTimeRemaining = item.Eta,
                StatusText = statusText,
                ProgressPercent = progressPercent,
                Status = item.Status,
                IsCompleted = isCompleted,
                IsFailed = isFailed,
                ErrorMessage = error,
                OutputFilePath = item.OutputFilePath
            };

            await progressWriter.WriteAsync(report);
        }

        private static string SanitizeFileName(string fileName)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"[{0}]", invalidChars);
            return Regex.Replace(fileName, invalidRegStr, "_").Trim();
        }
    }
}
