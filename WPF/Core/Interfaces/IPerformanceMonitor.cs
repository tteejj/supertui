using System.Collections.Generic;
using SuperTUI.Extensions;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for performance monitoring - enables tracking operation performance
    /// </summary>
    public interface IPerformanceMonitor
    {
        /// <summary>
        /// Get or create a performance counter by name
        /// </summary>
        PerformanceCounter GetCounter(string name);

        /// <summary>
        /// Start tracking an operation
        /// </summary>
        void StartOperation(string name);

        /// <summary>
        /// Stop tracking an operation and record duration
        /// </summary>
        void StopOperation(string name);

        /// <summary>
        /// Get all performance counters
        /// </summary>
        Dictionary<string, PerformanceCounter> GetAllCounters();

        /// <summary>
        /// Reset all counters
        /// </summary>
        void ResetAll();

        /// <summary>
        /// Generate a performance report
        /// </summary>
        string GenerateReport();
    }
}
