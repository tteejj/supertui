using System;
using System.Collections.Generic;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for hot-reload functionality - enables watching files for changes
    /// </summary>
    public interface IHotReloadManager : IDisposable
    {
        /// <summary>
        /// Event fired when a single file changes
        /// </summary>
        event Action<string> FileChanged;

        /// <summary>
        /// Event fired when multiple files change (batched)
        /// </summary>
        event Action<IEnumerable<string>> BatchChanged;

        /// <summary>
        /// Gets whether hot-reload is currently enabled
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Start watching specified directories for changes
        /// </summary>
        void Start(IEnumerable<string> watchDirectories, string filePattern = "*.cs");

        /// <summary>
        /// Stop watching for changes
        /// </summary>
        void Stop();

        /// <summary>
        /// Enable hot-reload
        /// </summary>
        void Enable();

        /// <summary>
        /// Disable hot-reload
        /// </summary>
        void Disable();

        /// <summary>
        /// Set debounce delay in milliseconds
        /// </summary>
        void SetDebounceDelay(int milliseconds);
    }
}
