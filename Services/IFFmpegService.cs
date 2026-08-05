using System.Threading;
using System.Threading.Tasks;

namespace BasmaYouTubeDownloaderUltra.Services
{
    public interface IFFmpegService
    {
        string? FFmpegBinaryPath { get; }
        string? YtDlpBinaryPath { get; }
        bool IsFFmpegAvailable { get; }
        bool IsYtDlpAvailable { get; }

        Task DiscoverBinariesAsync();
        Task MuxVideoAndAudioAsync(string videoPath, string audioPath, string outputPath, CancellationToken cancellationToken = default);
        Task ConvertAudioAsync(string inputPath, string outputPath, string targetFormat, int bitrateKbps = 320, CancellationToken cancellationToken = default);
    }
}
