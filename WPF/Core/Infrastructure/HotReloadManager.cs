using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Manages hot reload functionality for development.
    /// Watches source files and triggers reload events when changes are detected.
    /// </summary>
    public class HotReloadManager : IHotReloadManager
    {
        private static HotReloadManager instance;
        private static readonly object lockObject = new object();

        private List<FileSystemWatcher> watchers;
        private bool isEnabled;
        private Timer debounceTimer;
        private HashSet<string> pendingChanges;
        private readonly object pendingLock = new object();

        public event Action<string> FileChanged;
        public event Action<IEnumerable<string>> BatchChanged;

        public bool IsEnabled => isEnabled;

        public static HotReloadManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObject)
                    {
                        if (instance == null)
                        {
                            instance = new HotReloadManager();
                        }
                    }
                }
                return instance;
            }
        }

        private HotReloadManager()
        {
            watchers = new List<FileSystemWatcher>();
            pendingChanges = new HashSet<string>();
            isEnabled = false;
        }

        /// <summary>
        /// Enable hot reload and watch specified directories
        /// </summary>
        public void Enable(params string[] watchPaths)
        {
            if (isEnabled)
            {
                Logger.Instance.Warning("HotReload", "Hot reload already enabled");
                return;
            }

            try
            {
                foreach (var path in watchPaths)
                {
                    if (!Directory.Exists(path))
                    {
                        Logger.Instance.Warning("HotReload", $"Watch path does not exist: {path}");
                        continue;
                    }

                    var watcher = new FileSystemWatcher(path)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                        Filter = "*.cs",
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true
                    };

                    watcher.Changed += OnFileChanged;
                    watcher.Created += OnFileChanged;
                    watcher.Deleted += OnFileChanged;
                    watcher.Renamed += OnFileRenamed;

                    watchers.Add(watcher);
                    Logger.Instance.Info("HotReload", $"Watching: {path}");
                }

                isEnabled = true;
                Logger.Instance.Info("HotReload", "Hot reload enabled");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("HotReload", $"Failed to enable hot reload: {ex.Message}");
                Disable();
            }
        }

        /// <summary>
        /// Disable hot reload and stop watching files
        /// </summary>
        public void Disable()
        {
            if (!isEnabled)
                return;

            foreach (var watcher in watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnFileChanged;
                watcher.Created -= OnFileChanged;
                watcher.Deleted -= OnFileChanged;
                watcher.Renamed -= OnFileRenamed;
                watcher.Dispose();
            }

            watchers.Clear();
            debounceTimer?.Dispose();
            debounceTimer = null;

            lock (pendingLock)
            {
                pendingChanges.Clear();
            }

            isEnabled = false;
            Logger.Instance.Info("HotReload", "Hot reload disabled");
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (ShouldIgnoreFile(e.FullPath))
                return;

            Logger.Instance.Debug("HotReload", $"File changed: {e.Name}");

            lock (pendingLock)
            {
                pendingChanges.Add(e.FullPath);
            }

            // Debounce - wait for changes to settle
            if (debounceTimer == null)
            {
                debounceTimer = new Timer(ProcessPendingChanges, null, 500, Timeout.Infinite);
            }
            else
            {
                debounceTimer.Change(500, Timeout.Infinite);
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (ShouldIgnoreFile(e.FullPath))
                return;

            Logger.Instance.Debug("HotReload", $"File renamed: {e.OldName} → {e.Name}");

            lock (pendingLock)
            {
                pendingChanges.Add(e.FullPath);
            }

            if (debounceTimer == null)
            {
                debounceTimer = new Timer(ProcessPendingChanges, null, 500, Timeout.Infinite);
            }
            else
            {
                debounceTimer.Change(500, Timeout.Infinite);
            }
        }

        private void ProcessPendingChanges(object state)
        {
            List<string> changes;

            lock (pendingLock)
            {
                if (pendingChanges.Count == 0)
                    return;

                changes = pendingChanges.ToList();
                pendingChanges.Clear();
            }

            Logger.Instance.Info("HotReload", $"Processing {changes.Count} file changes");

            try
            {
                // Trigger individual file changed events
                foreach (var file in changes)
                {
                    FileChanged?.Invoke(file);
                }

                // Trigger batch changed event
                BatchChanged?.Invoke(changes);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("HotReload", $"Error processing changes: {ex.Message}");
            }
        }

        private bool ShouldIgnoreFile(string path)
        {
            var fileName = Path.GetFileName(path);

            // Ignore temporary files
            if (fileName.StartsWith(".") || fileName.EndsWith("~") || fileName.Contains(".tmp"))
                return true;

            // Ignore IDE files
            if (fileName.EndsWith(".suo") || fileName.EndsWith(".user"))
                return true;

            // Ignore bin/obj directories
            if (path.Contains("\\bin\\") || path.Contains("\\obj\\") ||
                path.Contains("/bin/") || path.Contains("/obj/"))
                return true;

            return false;
        }

        /// <summary>
        /// Watch a specific file or directory
        /// </summary>
        public void WatchPath(string path, string filter = "*.cs")
        {
            if (!isEnabled)
            {
                Logger.Instance.Warning("HotReload", "Hot reload not enabled. Call Enable() first.");
                return;
            }

            try
            {
                bool isDirectory = Directory.Exists(path);
                string watchPath = isDirectory ? path : Path.GetDirectoryName(path);
                string watchFilter = isDirectory ? filter : Path.GetFileName(path);

                var watcher = new FileSystemWatcher(watchPath)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    Filter = watchFilter,
                    IncludeSubdirectories = isDirectory,
                    EnableRaisingEvents = true
                };

                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileChanged;
                watcher.Deleted += OnFileChanged;

                watchers.Add(watcher);
                Logger.Instance.Info("HotReload", $"Added watch: {watchPath} ({watchFilter})");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("HotReload", $"Failed to watch path: {ex.Message}");
            }
        }

        /// <summary>
        /// Get statistics about hot reload activity
        /// </summary>
        public HotReloadStats GetStatistics()
        {
            return new HotReloadStats
            {
                IsEnabled = isEnabled,
                WatchedPaths = watchers.Count,
                PendingChanges = pendingChanges.Count
            };
        }

        public void Dispose()
        {
            Disable();
        }
    }

    /// <summary>
    /// Hot reload statistics
    /// </summary>
    public class HotReloadStats
    {
        public bool IsEnabled { get; set; }
        public int WatchedPaths { get; set; }
        public int PendingChanges { get; set; }
    }
}
