using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BasmaYouTubeDownloaderUltra.Models;
using TagLib;

namespace BasmaYouTubeDownloaderUltra.Services
{
    public class MetadataTaggingService : IMetadataTaggingService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task EmbedMetadataAsync(string filePath, MediaItem item, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                return;

            try
            {
                using var tagFile = TagLib.File.Create(filePath);

                // Write metadata fields
                tagFile.Tag.Title = item.Title;
                tagFile.Tag.Performers = new[] { item.Author };
                tagFile.Tag.AlbumArtists = new[] { item.Author };
                tagFile.Tag.Album = "Basma Ultra Downloader Workstation";
                tagFile.Tag.Year = (uint)DateTime.Now.Year;
                tagFile.Tag.Comment = "Downloaded with Basma YouTube Downloader Ultra Pro (.NET 8)";

                // Embed thumbnail cover art if available
                if (!string.IsNullOrWhiteSpace(item.ThumbnailUrl))
                {
                    try
                    {
                        byte[] imageBytes = await _httpClient.GetByteArrayAsync(item.ThumbnailUrl, cancellationToken);
                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            var picture = new TagLib.Picture
                            {
                                Type = TagLib.PictureType.FrontCover,
                                MimeType = "image/jpeg",
                                Description = "Cover Art",
                                Data = new TagLib.ByteVector(imageBytes)
                            };

                            tagFile.Tag.Pictures = new TagLib.IPicture[] { picture };
                        }
                    }
                    catch
                    {
                        // Ignore thumbnail fetch failure, metadata tags still saved
                    }
                }

                tagFile.Save();
            }
            catch
            {
                // Soft fail if taglib cannot process file format
            }
        }
    }
}
