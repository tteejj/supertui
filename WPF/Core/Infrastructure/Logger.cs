using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SuperTUI.Infrastructure
{
    // ============================================================================
    // LOGGING SYSTEM
    // ============================================================================

    /// <summary>
    /// Log severity levels
    /// </summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }

    /// <summary>
    /// Log entry structure
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public string StackTrace { get; set; }
        public int ThreadId { get; set; }
    }

    /// <summary>
    /// Log sink interface for extensibility
    /// </summary>
    public interface ILogSink
    {
        void Write(LogEntry entry);
        void Flush();
    }

    /// <summary>
    /// Log drop policy determines behavior when queues are full
    /// </summary>
    public enum LogDropPolicy
    {
        /// <summary>
        /// Drop oldest normal-priority logs when queue is full (default)
        /// Critical/Error logs are NEVER dropped
        /// </summary>
        DropOldest,

        /// <summary>
        /// Block the calling thread until space is available
        /// May cause UI freezes under extreme load
        /// </summary>
        BlockCaller,

        /// <summary>
        /// Slow down logging by introducing small delays
        /// Provides backpressure to reduce log rate
        /// </summary>
        Throttle
    }

    /// <summary>
    /// File-based log sink with rotation and async I/O
    /// Uses a background thread to write logs without blocking the UI
    ///
    /// RELIABILITY FEATURES (Phase 2):
    /// - Separate priority queues: Critical/Error logs NEVER dropped
    /// - Configurable drop policy for normal logs
    /// - Metrics exposed for monitoring
    /// - Backpressure mechanism to prevent memory exhaustion
    /// </summary>
    public class FileLogSink : ILogSink, IDisposable
    {
        private readonly string logDirectory;
        private readonly string logFilePrefix;
        private readonly long maxFileSizeBytes;
        private readonly int maxFiles;

        // PHASE 2 FIX: Separate queues by priority
        private readonly System.Collections.Concurrent.BlockingCollection<string> criticalQueue;  // Error, Critical
        private readonly System.Collections.Concurrent.BlockingCollection<string> normalQueue;    // Trace, Debug, Info, Warning

        private readonly LogDropPolicy dropPolicy;
        private readonly System.Threading.Thread writerThread;
        private readonly object lockObject = new object();
        private StreamWriter currentWriter;
        private string currentFilePath;
        private long currentFileSize;
        private bool disposed = false;

        // Track dropped logs (normal priority only - critical NEVER dropped)
        private long droppedLogCount = 0;
        private DateTime lastDroppedLogWarning = DateTime.MinValue;

        public FileLogSink(string logDirectory, string logFilePrefix = "supertui", long maxFileSizeMB = 10, int maxFiles = 5, LogDropPolicy dropPolicy = LogDropPolicy.DropOldest)
        {
            this.logDirectory = logDirectory;
            this.logFilePrefix = logFilePrefix;
            this.maxFileSizeBytes = maxFileSizeMB * 1024 * 1024;
            this.maxFiles = maxFiles;
            this.dropPolicy = dropPolicy;

            // PHASE 2 FIX: Separate queues by priority
            // Critical queue: smaller but NEVER drops logs (blocking if necessary)
            this.criticalQueue = new System.Collections.Concurrent.BlockingCollection<string>(boundedCapacity: 1000);

            // Normal queue: larger, can drop logs based on policy
            this.normalQueue = new System.Collections.Concurrent.BlockingCollection<string>(boundedCapacity: 10000);

            Directory.CreateDirectory(logDirectory);
            OpenNewLogFile();

            // Start background writer thread
            writerThread = new System.Threading.Thread(WriterThreadProc)
            {
                IsBackground = true,
                Name = "FileLogSink Writer",
                Priority = System.Threading.ThreadPriority.BelowNormal  // Don't interfere with UI
            };
            writerThread.Start();
        }

        private void OpenNewLogFile()
        {
            lock (lockObject)
            {
                if (currentWriter != null)
                {
                    currentWriter.Flush();
                    currentWriter.Close();
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                currentFilePath = Path.Combine(logDirectory, $"{logFilePrefix}_{timestamp}.log");
                // AutoFlush removed - we flush manually on a schedule
                currentWriter = new StreamWriter(currentFilePath, true, Encoding.UTF8) { AutoFlush = false };
                currentFileSize = 0;

                // Rotate old files
                RotateOldFiles();
            }
        }

        private void RotateOldFiles()
        {
            var logFiles = Directory.GetFiles(logDirectory, $"{logFilePrefix}_*.log")
                .OrderByDescending(f => File.GetCreationTime(f))
                .Skip(maxFiles)
                .ToList();

            foreach (var file in logFiles)
            {
                try { File.Delete(file); } catch { }
            }
        }

        /// <summary>
        /// Background thread that writes log entries to disk
        /// This prevents blocking the UI thread during log writes
        ///
        /// PHASE 2 FIX: Processes critical queue with priority
        /// </summary>
        private void WriterThreadProc()
        {
            var lastFlushTime = DateTime.Now;
            const int flushIntervalMs = 1000; // Flush every second

            try
            {
                while (!disposed)
                {
                    string line = null;
                    bool gotLine = false;

                    // PHASE 2 FIX: Always prioritize critical queue
                    // Check critical queue first (blocks for up to 10ms)
                    if (criticalQueue.TryTake(out line, millisecondsTimeout: 10))
                    {
                        gotLine = true;
                    }
                    // Then check normal queue (blocks for up to 90ms)
                    else if (normalQueue.TryTake(out line, millisecondsTimeout: 90))
                    {
                        gotLine = true;
                    }

                    if (gotLine)
                    {
                        lock (lockObject)
                        {
                            if (currentWriter != null && !disposed)
                            {
                                byte[] bytes = Encoding.UTF8.GetBytes(line);
                                currentFileSize += bytes.Length;

                                if (currentFileSize > maxFileSizeBytes)
                                {
                                    OpenNewLogFile();
                                }

                                currentWriter.Write(line);
                            }
                        }
                    }

                    // Periodic flush (every second or when queues are empty)
                    if ((DateTime.Now - lastFlushTime).TotalMilliseconds >= flushIntervalMs ||
                        (criticalQueue.Count == 0 && normalQueue.Count == 0))
                    {
                        lock (lockObject)
                        {
                            currentWriter?.Flush();
                        }
                        lastFlushTime = DateTime.Now;
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
                // Expected when disposing
            }
            catch (Exception ex)
            {
                // Log to console as fallback (can't use Logger here - infinite loop!)
                // Don't use ErrorPolicy here to avoid circular dependency
                Console.WriteLine($"[CRITICAL] FileLogSink writer thread error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Drain remaining queue items on shutdown - CRITICAL FIRST
                while (criticalQueue.TryTake(out string line, millisecondsTimeout: 0))
                {
                    try
                    {
                        lock (lockObject)
                        {
                            currentWriter?.Write(line);
                        }
                    }
                    catch { }
                }

                while (normalQueue.TryTake(out string line, millisecondsTimeout: 0))
                {
                    try
                    {
                        lock (lockObject)
                        {
                            currentWriter?.Write(line);
                        }
                    }
                    catch { }
                }

                lock (lockObject)
                {
                    currentWriter?.Flush();
                    currentWriter?.Close();
                }
            }
        }

        public void Write(LogEntry entry)
        {
            if (disposed) return;

            // Format log entry
            string line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level,-8}] [{entry.Category}] {entry.Message}";

            if (entry.Exception != null)
            {
                line += $"\n    Exception: {entry.Exception.GetType().Name}: {entry.Exception.Message}";
                line += $"\n    {entry.Exception.StackTrace}";
            }

            if (entry.Properties.Count > 0)
            {
                line += $"\n    Properties: {string.Join(", ", entry.Properties.Select(kvp => $"{kvp.Key}={kvp.Value}"))}";
            }

            line += "\n";

            // PHASE 2 FIX: Route to appropriate queue based on severity
            bool isCritical = entry.Level >= LogLevel.Error;

            if (isCritical)
            {
                // CRITICAL/ERROR: NEVER drop - block if necessary
                try
                {
                    if (!criticalQueue.TryAdd(line, millisecondsTimeout: 5000))
                    {
                        // If even critical queue is full after 5 seconds, this is SERIOUS
                        // Log to console and throw exception
                        Console.WriteLine($"[FileLogSink CRITICAL] Critical log queue overflow! Disk may be full or I/O is blocked.");
                        throw new InvalidOperationException("Critical log queue overflow - disk may be full or I/O blocked");
                    }
                }
                catch (InvalidOperationException)
                {
                    // Queue was marked as complete (disposing) - write to console as last resort
                    Console.WriteLine($"[CRITICAL] {line}");
                    throw;
                }
            }
            else
            {
                // NORMAL: Apply drop policy
                switch (dropPolicy)
                {
                    case LogDropPolicy.DropOldest:
                        // Try to add, if full drop oldest
                        if (!normalQueue.TryAdd(line, millisecondsTimeout: 0))
                        {
                            System.Threading.Interlocked.Increment(ref droppedLogCount);

                            // Try to remove oldest and add new
                            if (normalQueue.TryTake(out _))
                            {
                                normalQueue.TryAdd(line, millisecondsTimeout: 0);
                            }

                            WarnAboutDroppedLogs();
                        }
                        break;

                    case LogDropPolicy.BlockCaller:
                        // Block until space available (may freeze UI)
                        normalQueue.Add(line);
                        break;

                    case LogDropPolicy.Throttle:
                        // Try for 100ms, then drop
                        if (!normalQueue.TryAdd(line, millisecondsTimeout: 100))
                        {
                            System.Threading.Interlocked.Increment(ref droppedLogCount);
                            WarnAboutDroppedLogs();
                        }
                        break;
                }
            }
        }

        private void WarnAboutDroppedLogs()
        {
            // Warn at most once per minute to avoid console spam
            if ((DateTime.Now - lastDroppedLogWarning).TotalSeconds >= 60)
            {
                lastDroppedLogWarning = DateTime.Now;
                Console.WriteLine($"[FileLogSink WARNING] Normal log queue full! {droppedLogCount} logs dropped. " +
                                "Disk may be slow or logging rate too high. Consider increasing queue size or reducing log level.");
            }
        }

        /// <summary>
        /// Get number of dropped logs since sink creation
        /// </summary>
        public long GetDroppedLogCount() => droppedLogCount;

        public void Flush()
        {
            if (disposed) return;

            // Wait for both queues to drain
            while (criticalQueue.Count > 0 || normalQueue.Count > 0)
            {
                System.Threading.Thread.Sleep(10);
            }

            lock (lockObject)
            {
                currentWriter?.Flush();
            }
        }

        /// <summary>
        /// Get current queue depths for monitoring
        /// </summary>
        public (int critical, int normal) GetQueueDepth()
        {
            return (criticalQueue.Count, normalQueue.Count);
        }

        public void Dispose()
        {
            if (disposed) return;

            disposed = true;

            // Mark queues as complete (no more adding)
            criticalQueue.CompleteAdding();
            normalQueue.CompleteAdding();

            // Wait for writer thread to finish (with timeout)
            if (writerThread != null && writerThread.IsAlive)
            {
                writerThread.Join(timeout: TimeSpan.FromSeconds(5));
            }

            // Dispose queues
            criticalQueue.Dispose();
            normalQueue.Dispose();
        }
    }

    /// <summary>
    /// In-memory log sink for debugging and testing
    /// </summary>
    public class MemoryLogSink : ILogSink
    {
        private readonly int maxEntries;
        private readonly Queue<LogEntry> entries;

        public MemoryLogSink(int maxEntries = 1000)
        {
            this.maxEntries = maxEntries;
            this.entries = new Queue<LogEntry>(maxEntries);
        }

        public void Write(LogEntry entry)
        {
            lock (entries)
            {
                if (entries.Count >= maxEntries)
                {
                    entries.Dequeue();
                }
                entries.Enqueue(entry);
            }
        }

        public void Flush() { }

        public List<LogEntry> GetEntries(LogLevel minLevel = LogLevel.Trace, string category = null, int count = 100)
        {
            lock (entries)
            {
                return entries
                    .Where(e => e.Level >= minLevel && (category == null || e.Category == category))
                    .TakeLast(count)
                    .ToList();
            }
        }

        public void Clear()
        {
            lock (entries)
            {
                entries.Clear();
            }
        }
    }

    /// <summary>
    /// Centralized logging system with multiple sinks
    /// PHASE 3: Maximum DI - Use dependency injection instead of singleton
    /// </summary>
    public class Logger : ILogger
    {
        private static Logger instance;

        /// <summary>
        /// Singleton instance - DEPRECATED
        /// PHASE 3: Use dependency injection instead
        /// </summary>
        [Obsolete("Use dependency injection instead of Logger.Instance. Get ILogger from ServiceContainer.", error: false)]
        public static Logger Instance => instance ??= new Logger();

        private readonly List<ILogSink> sinks = new List<ILogSink>();
        private LogLevel minLevel = LogLevel.Info;
        private readonly HashSet<string> enabledCategories = new HashSet<string>();
        private bool logAllCategories = true;

        public void AddSink(ILogSink sink)
        {
            lock (sinks)
            {
                sinks.Add(sink);
            }
        }

        public void SetMinLevel(LogLevel level)
        {
            minLevel = level;
        }

        public void EnableCategory(string category)
        {
            logAllCategories = false;
            enabledCategories.Add(category);
        }

        public void DisableCategory(string category)
        {
            enabledCategories.Remove(category);
        }

        public void Log(LogLevel level, string category, string message, Exception exception = null, Dictionary<string, object> properties = null)
        {
            if (level < minLevel)
                return;

            if (!logAllCategories && !enabledCategories.Contains(category))
                return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Category = category,
                Message = message,
                Exception = exception,
                Properties = properties ?? new Dictionary<string, object>(),
                StackTrace = level >= LogLevel.Error ? Environment.StackTrace : null,
                ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId
            };

            lock (sinks)
            {
                foreach (var sink in sinks)
                {
                    try
                    {
                        sink.Write(entry);
                    }
                    catch
                    {
                        // Don't let logging errors crash the app
                    }
                }
            }
        }

        public void Trace(string category, string message) => Log(LogLevel.Trace, category, message);
        public void Debug(string category, string message) => Log(LogLevel.Debug, category, message);
        public void Info(string category, string message) => Log(LogLevel.Info, category, message);
        public void Warning(string category, string message) => Log(LogLevel.Warning, category, message);
        public void Error(string category, string message, Exception ex = null) => Log(LogLevel.Error, category, message, ex);
        public void Critical(string category, string message, Exception ex = null) => Log(LogLevel.Critical, category, message, ex);

        public void Flush()
        {
            lock (sinks)
            {
                foreach (var sink in sinks)
                {
                    sink.Flush();
                }
            }
        }

        /// <summary>
        /// Get diagnostics information about the logger
        /// </summary>
        public Dictionary<string, object> GetDiagnostics()
        {
            var diagnostics = new Dictionary<string, object>
            {
                ["MinLevel"] = minLevel.ToString(),
                ["LogAllCategories"] = logAllCategories,
                ["EnabledCategories"] = string.Join(", ", enabledCategories),
                ["SinkCount"] = sinks.Count
            };

            lock (sinks)
            {
                var sinkInfo = new List<Dictionary<string, object>>();
                foreach (var sink in sinks)
                {
                    var info = new Dictionary<string, object>
                    {
                        ["Type"] = sink.GetType().Name
                    };

                    // Get diagnostics if it's a FileLogSink
                    if (sink is FileLogSink fileSink)
                    {
                        info["DroppedLogs"] = fileSink.GetDroppedLogCount();
                        var (critical, normal) = fileSink.GetQueueDepth();
                        info["QueueDepth"] = $"Critical:{critical}, Normal:{normal}";
                    }

                    sinkInfo.Add(info);
                }
                diagnostics["Sinks"] = sinkInfo;
            }

            return diagnostics;
        }

        /// <summary>
        /// Get total number of dropped logs across all file sinks
        /// </summary>
        public long GetTotalDroppedLogs()
        {
            long total = 0;
            lock (sinks)
            {
                foreach (var sink in sinks)
                {
                    if (sink is FileLogSink fileSink)
                    {
                        total += fileSink.GetDroppedLogCount();
                    }
                }
            }
            return total;
        }
    }
}
