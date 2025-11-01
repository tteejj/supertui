using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Generic base class for services with JSON file persistence
    /// Provides common functionality: debounced auto-save, atomic writes, backups, thread-safety
    /// Template method pattern: subclasses implement GetDataToSave/SetLoadedData
    /// </summary>
    /// <typeparam name="T">Data transfer object type for serialization</typeparam>
    public abstract class JsonPersistenceService<T> : IDisposable where T : class
    {
        protected readonly string filePath;
        protected readonly ILogger logger;
        protected readonly object lockObject = new object();

        // Save debouncing
        private Timer saveTimer;
        private volatile bool pendingSave = false;
        private const int SAVE_DEBOUNCE_MS = 500;

        /// <summary>
        /// Constructor for JSON persistence service
        /// </summary>
        /// <param name="filePath">Full path to JSON data file</param>
        /// <param name="logger">Logger instance (optional)</param>
        protected JsonPersistenceService(string filePath, ILogger logger)
        {
            this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            this.logger = logger; // Null logger is allowed

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                logger?.Info(GetServiceName(), $"Created data directory: {directory}");
            }

            // Initialize save timer
            saveTimer = new Timer(SaveTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        #region Template Methods (must be implemented by subclasses)

        /// <summary>
        /// Get data to save to file (called within lock)
        /// </summary>
        protected abstract T GetDataToSave();

        /// <summary>
        /// Set loaded data (called within lock)
        /// Subclass should deserialize and rebuild any indexes
        /// </summary>
        protected abstract void SetLoadedData(T data);

        /// <summary>
        /// Get service name for logging
        /// </summary>
        protected abstract string GetServiceName();

        #endregion

        #region Save Operations

        /// <summary>
        /// Schedule a debounced save operation (500ms delay)
        /// Thread-safe, can be called multiple times (only last call within window triggers save)
        /// </summary>
        protected void ScheduleSave()
        {
            pendingSave = true;
            saveTimer?.Change(SAVE_DEBOUNCE_MS, Timeout.Infinite);
        }

        /// <summary>
        /// Timer callback for debounced save
        /// </summary>
        private void SaveTimerCallback(object state)
        {
            if (pendingSave)
            {
                pendingSave = false;
                Task.Run(async () => await SaveToFileAsync());
            }
        }

        /// <summary>
        /// Save data to JSON file asynchronously
        /// Creates timestamped backups (keeps last 5)
        /// Uses atomic write pattern (temp file → rename)
        /// </summary>
        protected async Task SaveToFileAsync()
        {
            try
            {
                // Create timestamped backup before saving
                if (File.Exists(filePath))
                {
                    var backupPath = $"{filePath}.{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                    await Task.Run(() => File.Copy(filePath, backupPath, overwrite: true));

                    // Keep only last 5 backups
                    var backupDir = Path.GetDirectoryName(filePath);
                    var fileName = Path.GetFileName(filePath);
                    var backupFiles = Directory.GetFiles(backupDir, $"{fileName}.*.bak")
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .Skip(5)
                        .ToList();

                    foreach (var oldBackup in backupFiles)
                    {
                        try
                        {
                            File.Delete(oldBackup);
                        }
                        catch (Exception ex)
                        {
                            ErrorHandlingPolicy.Handle(
                                ErrorCategory.IO,
                                ex,
                                $"Deleting old backup file '{oldBackup}'",
                                logger);
                        }
                    }
                }

                // Get data to save (inside lock to ensure consistency)
                T dataToSave;
                lock (lockObject)
                {
                    dataToSave = GetDataToSave();
                }

                // Serialize to JSON
                var json = await Task.Run(() => JsonSerializer.Serialize(dataToSave, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

                // Atomic write: temp → rename pattern
                string tempFile = filePath + ".tmp";
                await Task.Run(() => File.WriteAllText(tempFile, json));

                // Use Move for first save (file doesn't exist), Replace for subsequent saves
                if (!File.Exists(filePath))
                {
                    await Task.Run(() => File.Move(tempFile, filePath));
                }
                else
                {
                    await Task.Run(() => File.Replace(tempFile, filePath, filePath + ".bak"));
                }

                logger?.Debug(GetServiceName(), $"Saved data to {filePath}");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Saving {GetServiceName()} data to '{filePath}'",
                    logger);
            }
        }

        /// <summary>
        /// Save data to JSON file synchronously (used in Dispose)
        /// Creates timestamped backups (keeps last 5)
        /// Uses atomic write pattern (temp file → rename)
        /// </summary>
        protected void SaveToFileSync()
        {
            try
            {
                // Create timestamped backup before saving
                if (File.Exists(filePath))
                {
                    var backupPath = $"{filePath}.{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                    File.Copy(filePath, backupPath, overwrite: true);

                    // Keep only last 5 backups
                    var backupDir = Path.GetDirectoryName(filePath);
                    var fileName = Path.GetFileName(filePath);
                    var backupFiles = Directory.GetFiles(backupDir, $"{fileName}.*.bak")
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .Skip(5)
                        .ToList();

                    foreach (var oldBackup in backupFiles)
                    {
                        try
                        {
                            File.Delete(oldBackup);
                        }
                        catch (Exception ex)
                        {
                            ErrorHandlingPolicy.Handle(
                                ErrorCategory.IO,
                                ex,
                                $"Deleting old backup file '{oldBackup}'",
                                logger);
                        }
                    }
                }

                // Get data to save (inside lock to ensure consistency)
                T dataToSave;
                lock (lockObject)
                {
                    dataToSave = GetDataToSave();
                }

                // Serialize to JSON
                var json = JsonSerializer.Serialize(dataToSave, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Atomic write: temp → rename pattern
                string tempFile = filePath + ".tmp";
                File.WriteAllText(tempFile, json);

                // Use Move for first save (file doesn't exist), Replace for subsequent saves
                if (!File.Exists(filePath))
                {
                    File.Move(tempFile, filePath);
                }
                else
                {
                    File.Replace(tempFile, filePath, filePath + ".bak");
                }

                logger?.Debug(GetServiceName(), $"Saved data to {filePath}");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Saving {GetServiceName()} data to '{filePath}'",
                    logger);
            }
        }

        #endregion

        #region Load Operations

        /// <summary>
        /// Load data from JSON file
        /// Calls SetLoadedData template method to deserialize
        /// Thread-safe with lock
        /// </summary>
        protected void LoadFromFile()
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    logger?.Info(GetServiceName(), "No existing data file found, starting fresh");
                    return;
                }

                var json = File.ReadAllText(filePath);
                var loadedData = JsonSerializer.Deserialize<T>(json);

                lock (lockObject)
                {
                    SetLoadedData(loadedData);
                }

                logger?.Info(GetServiceName(), $"Loaded data from {filePath}");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Loading {GetServiceName()} data from '{filePath}'",
                    logger);
            }
        }

        /// <summary>
        /// Reload data from file (useful for external changes)
        /// </summary>
        public virtual void Reload()
        {
            LoadFromFile();
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose resources: flush pending saves, dispose timer
        /// Thread-safe
        /// </summary>
        public virtual void Dispose()
        {
            if (saveTimer != null)
            {
                lock (lockObject)
                {
                    // Ensure any pending save is executed before disposal
                    if (pendingSave)
                    {
                        SaveToFileSync();  // Use synchronous save to avoid deadlock
                    }

                    saveTimer.Dispose();
                    saveTimer = null;
                }
            }
        }

        #endregion
    }
}
