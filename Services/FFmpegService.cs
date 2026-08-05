using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;

namespace BasmaYouTubeDownloaderUltra.Services
{
    public class FFmpegService : IFFmpegService
    {
        public string? FFmpegBinaryPath { get; private set; }
        public string? YtDlpBinaryPath { get; private set; }
        public bool IsFFmpegAvailable => !string.IsNullOrEmpty(FFmpegBinaryPath) && File.Exists(FFmpegBinaryPath);
        public bool IsYtDlpAvailable => !string.IsNullOrEmpty(YtDlpBinaryPath) && File.Exists(YtDlpBinaryPath);

        public Task DiscoverBinariesAsync()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Search locations for FFmpeg
            string[] ffmpegLocations = {
                Path.Combine(baseDir, "ffmpeg.exe"),
                Path.Combine(baseDir, "bin", "ffmpeg.exe"),
                Path.Combine(baseDir, "ffmpeg", "bin", "ffmpeg.exe"),
                "ffmpeg.exe"
            };

            foreach (var loc in ffmpegLocations)
            {
                if (File.Exists(loc))
                {
                    FFmpegBinaryPath = Path.GetFullPath(loc);
                    var folder = Path.GetDirectoryName(FFmpegBinaryPath);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        GlobalFFOptions.Configure(new FFOptions { BinaryFolder = folder });
                    }
                    break;
                }
            }

            if (string.IsNullOrEmpty(FFmpegBinaryPath))
            {
                string? systemFFmpeg = FindOnSystemPath("ffmpeg.exe");
                if (!string.IsNullOrEmpty(systemFFmpeg))
                {
                    FFmpegBinaryPath = systemFFmpeg;
                    var folder = Path.GetDirectoryName(FFmpegBinaryPath);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        GlobalFFOptions.Configure(new FFOptions { BinaryFolder = folder });
                    }
                }
            }

            // Search locations for yt-dlp
            string[] ytDlpLocations = {
                Path.Combine(baseDir, "yt-dlp.exe"),
                Path.Combine(baseDir, "bin", "yt-dlp.exe"),
                "yt-dlp.exe"
            };

            foreach (var loc in ytDlpLocations)
            {
                if (File.Exists(loc))
                {
                    YtDlpBinaryPath = Path.GetFullPath(loc);
                    break;
                }
            }

            if (string.IsNullOrEmpty(YtDlpBinaryPath))
            {
                YtDlpBinaryPath = FindOnSystemPath("yt-dlp.exe");
            }

            return Task.CompletedTask;
        }

        public async Task MuxVideoAndAudioAsync(string videoPath, string audioPath, string outputPath, CancellationToken cancellationToken = default)
        {
            if (IsFFmpegAvailable && !string.IsNullOrEmpty(FFmpegBinaryPath))
            {
                // Use FFmpeg directly via Process for maximum stream compatibility
                string args = $"-y -i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac -b:a 256k -movflags +faststart \"{outputPath}\"";
                await RunProcessAsync(FFmpegBinaryPath, args, cancellationToken);
            }
            else
            {
                // Fallback attempt using FFMpegCore
                await FFMpegArguments
                    .FromFileInput(videoPath)
                    .AddFileInput(audioPath)
                    .OutputToFile(outputPath, true, options => options.CopyChannel())
                    .ProcessAsynchronously();
            }
        }

        public async Task ConvertAudioAsync(string inputPath, string outputPath, string targetFormat, int bitrateKbps = 320, CancellationToken cancellationToken = default)
        {
            if (!IsFFmpegAvailable || string.IsNullOrEmpty(FFmpegBinaryPath))
            {
                // If ffmpeg is missing, simply copy file if paths differ
                if (inputPath != outputPath && File.Exists(inputPath))
                {
                    File.Copy(inputPath, outputPath, true);
                }
                return;
            }

            string codecArgs = targetFormat.ToLowerInvariant() switch
            {
                "mp3" => $"-c:a libmp3lame -b:a {bitrateKbps}k",
                "flac" => "-c:a flac",
                "aac" => $"-c:a aac -b:a {bitrateKbps}k",
                "wav" => "-c:a pcm_s16le",
                _ => $"-c:a libmp3lame -b:a {bitrateKbps}k"
            };

            string args = $"-y -i \"{inputPath}\" -vn {codecArgs} \"{outputPath}\"";
            await RunProcessAsync(FFmpegBinaryPath, args, cancellationToken);
        }

        private static async Task RunProcessAsync(string exePath, string args, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => {
                try { proc.Kill(); } catch { }
                tcs.TrySetCanceled();
            }))
            {
                await Task.WhenAny(proc.WaitForExitAsync(cancellationToken), tcs.Task);
            }
        }

        private static string? FindOnSystemPath(string exeName)
        {
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return null;

            string[] paths = pathEnv.Split(Path.PathSeparator);
            foreach (var p in paths)
            {
                try
                {
                    string fullPath = Path.Combine(p.Trim(), exeName);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
                catch { }
            }
            return null;
        }
    }
}
