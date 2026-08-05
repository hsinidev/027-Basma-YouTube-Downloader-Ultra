namespace BasmaYouTubeDownloaderUltra.Models
{
    public class QualityOption
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsAudioOnly { get; set; }
        public int BitrateKbps { get; set; }
        public int Height { get; set; }

        public override string ToString() => Name;
    }
}
