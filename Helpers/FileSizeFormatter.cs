using System;

namespace BasmaYouTubeDownloaderUltra.Helpers
{
    public static class FileSizeFormatter
    {
        private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB" };

        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "0 B";
            if (bytes == 0) return "0 B";

            int mag = (int)Math.Log(bytes, 1024);
            mag = Math.Min(mag, SizeSuffixes.Length - 1);

            decimal adjustedSize = (decimal)bytes / (1L << (mag * 10));
            return $"{adjustedSize:0.##} {SizeSuffixes[mag]}";
        }

        public static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "0 KB/s";
            if (bytesPerSecond >= 1024 * 1024)
            {
                return $"{(bytesPerSecond / (1024 * 1024)):0.00} MB/s";
            }
            return $"{(bytesPerSecond / 1024):0.0} KB/s";
        }

        public static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalSeconds <= 0 || double.IsInfinity(timeSpan.TotalSeconds) || double.IsNaN(timeSpan.TotalSeconds))
            {
                return "00:00";
            }

            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }

            return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
    }
}
