using System.Threading;
using System.Threading.Tasks;
using BasmaYouTubeDownloaderUltra.Models;

namespace BasmaYouTubeDownloaderUltra.Services
{
    public interface IMetadataTaggingService
    {
        Task EmbedMetadataAsync(string filePath, MediaItem item, CancellationToken cancellationToken = default);
    }
}
