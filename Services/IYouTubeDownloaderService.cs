using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BasmaYouTubeDownloaderUltra.Models;

namespace BasmaYouTubeDownloaderUltra.Services
{
    public interface IYouTubeDownloaderService
    {
        Task<List<MediaItem>> AnalyzeUrlAsync(string url, CancellationToken cancellationToken = default);
        Task DownloadItemAsync(
            MediaItem item,
            DownloadSettings settings,
            ChannelWriter<DownloadProgressReport> progressWriter,
            CancellationToken cancellationToken = default);
    }
}
