using System;
using System.Collections.Generic;
using System.Linq;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Performance counter for monitoring operation duration and performance.
    /// Tracks timing samples and provides statistics (average, min, max).
    /// </summary>
    public class PerformanceCounter
    {
        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private readonly Queue<TimeSpan> samples = new Queue<TimeSpan>();
        private readonly int maxSamples;

        /// <summary>
        /// Name of this performance counter
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Duration of the last recorded operation
        /// </summary>
        public TimeSpan LastDuration { get; private set; }

        /// <summary>
        /// Average duration across all samples
        /// </summary>
        public TimeSpan AverageDuration => samples.Count > 0
            ? TimeSpan.FromTicks((long)samples.Average(s => s.Ticks))
            : TimeSpan.Zero;

        /// <summary>
        /// Minimum duration in all samples
        /// </summary>
        public TimeSpan MinDuration => samples.Count > 0
            ? TimeSpan.FromTicks(samples.Min(s => s.Ticks))
            : TimeSpan.Zero;

        /// <summary>
        /// Maximum duration in all samples
        /// </summary>
        public TimeSpan MaxDuration => samples.Count > 0
            ? TimeSpan.FromTicks(samples.Max(s => s.Ticks))
            : TimeSpan.Zero;

        /// <summary>
        /// Number of samples currently recorded
        /// </summary>
        public int SampleCount => samples.Count;

        /// <summary>
        /// Creates a new performance counter with the specified name and sample limit
        /// </summary>
        /// <param name="name">Name of this counter</param>
        /// <param name="maxSamples">Maximum number of samples to keep (default: 100)</param>
        public PerformanceCounter(string name, int maxSamples = 100)
        {
            Name = name;
            this.maxSamples = maxSamples;
        }

        /// <summary>
        /// Start timing an operation
        /// </summary>
        public void Start()
        {
            stopwatch.Restart();
        }

        /// <summary>
        /// Stop timing and record the duration
        /// </summary>
        public void Stop()
        {
            stopwatch.Stop();
            LastDuration = stopwatch.Elapsed;

            if (samples.Count >= maxSamples)
            {
                samples.Dequeue();
            }
            samples.Enqueue(LastDuration);
        }

        /// <summary>
        /// Reset all samples and clear statistics
        /// </summary>
        public void Reset()
        {
            samples.Clear();
            LastDuration = TimeSpan.Zero;
        }

        /// <summary>
        /// Returns a string representation of this counter's statistics
        /// </summary>
        public override string ToString()
        {
            return $"{Name}: Last={LastDuration.TotalMilliseconds:F2}ms, " +
                   $"Avg={AverageDuration.TotalMilliseconds:F2}ms, " +
                   $"Samples={SampleCount}";
        }
    }
}
