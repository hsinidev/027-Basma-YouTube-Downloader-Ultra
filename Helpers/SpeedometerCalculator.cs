using System;
using System.Collections.Generic;
using System.Linq;

namespace BasmaYouTubeDownloaderUltra.Helpers
{
    public class SpeedometerCalculator
    {
        private readonly Queue<(DateTime timestamp, long bytes)> _samples = new();
        private readonly TimeSpan _sampleWindow = TimeSpan.FromSeconds(3);
        private readonly object _lock = new();

        public void AddSample(long currentTotalBytes)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                _samples.Enqueue((now, currentTotalBytes));

                while (_samples.Count > 0 && (now - _samples.Peek().timestamp) > _sampleWindow)
                {
                    _samples.Dequeue();
                }
            }
        }

        public double CalculateSpeedBytesPerSecond()
        {
            lock (_lock)
            {
                if (_samples.Count < 2) return 0;

                var oldest = _samples.Peek();
                var newest = _samples.Last();

                double elapsedSeconds = (newest.timestamp - oldest.timestamp).TotalSeconds;
                if (elapsedSeconds <= 0) return 0;

                long bytesDifference = newest.bytes - oldest.bytes;
                if (bytesDifference < 0) return 0;

                return bytesDifference / elapsedSeconds;
            }
        }

        public TimeSpan CalculateEta(long currentBytes, long totalBytes)
        {
            if (totalBytes <= 0 || currentBytes >= totalBytes) return TimeSpan.Zero;

            double speed = CalculateSpeedBytesPerSecond();
            if (speed <= 0) return TimeSpan.Zero;

            long remainingBytes = totalBytes - currentBytes;
            double remainingSeconds = remainingBytes / speed;

            if (remainingSeconds > 86400 * 7) // More than 7 days
                return TimeSpan.FromDays(7);

            return TimeSpan.FromSeconds(remainingSeconds);
        }

        public void Reset()
        {
            lock (_lock)
            {
                _samples.Clear();
            }
        }
    }
}
