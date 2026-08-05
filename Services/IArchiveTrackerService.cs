using System.Threading.Tasks;

namespace BasmaYouTubeDownloaderUltra.Services
{
    public interface IArchiveTrackerService
    {
        string ArchiveFilePath { get; set; }
        Task InitializeAsync(string? customPath = null);
        bool IsArchived(string videoId);
        Task RecordDownloadedAsync(string videoId);
        Task<int> GetArchivedCountAsync();
    }
}
