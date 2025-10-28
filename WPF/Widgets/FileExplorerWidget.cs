using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Events;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

// File extension security classifications
using SafeExtensions = System.Collections.Generic.HashSet<string>;
using DangerousExtensions = System.Collections.Generic.HashSet<string>;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// File explorer widget for navigating directories and viewing files.
    /// Supports navigation, file selection, and opening files.
    ///
    /// SECURITY FEATURES:
    /// - Integration with SecurityManager for path validation
    /// - Dangerous file type detection and confirmation
    /// - Safe file extensions allowlist
    /// - All file operations logged for audit
    /// </summary>
    public class FileExplorerWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;
        private readonly ISecurityManager security;

        private StandardWidgetFrame frame;
        private string currentPath;
        private ListBox fileListBox;
        private TextBlock pathLabel;
        private TextBlock statusLabel;
        private List<FileSystemInfo> currentItems;
        private Theme theme;

        // File extension security classifications
        private static readonly SafeExtensions SafeFileExtensions = new SafeExtensions(StringComparer.OrdinalIgnoreCase)
        {
            // Text files
            ".txt", ".md", ".log", ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf",
            // Documents
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp",
            // Images
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".ico", ".webp",
            // Media
            ".mp3", ".mp4", ".avi", ".mov", ".mkv", ".flac", ".ogg", ".wav", ".m4a",
            // Archives (view only, not extract)
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2",
            // Code files (read-only viewing)
            ".cs", ".fs", ".vb", ".java", ".py", ".js", ".ts", ".html", ".css", ".scss",
            ".cpp", ".c", ".h", ".rs", ".go", ".rb", ".php", ".sh"
        };

        private static readonly DangerousExtensions DangerousFileExtensions = new DangerousExtensions(StringComparer.OrdinalIgnoreCase)
        {
            // Executables
            ".exe", ".com", ".bat", ".cmd", ".msi", ".scr", ".pif",
            // Scripts
            ".ps1", ".psm1", ".psd1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh",
            // System files
            ".sys", ".drv", ".dll", ".ocx",
            // Other dangerous
            ".reg", ".hta", ".cpl", ".msc", ".jar", ".app", ".deb", ".rpm"
        };

        // DI constructor (used by WidgetFactory - no optional parameters)
        public FileExplorerWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config,
            ISecurityManager security)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.security = security ?? throw new ArgumentNullException(nameof(security));

            WidgetName = "File Explorer";
            // Get initial path from config, fallback to current directory
            currentPath = config.Get("FileExplorer.InitialPath", Directory.GetCurrentDirectory());
            currentItems = new List<FileSystemInfo>();
        }

        // Manual constructor with initialPath (for direct instantiation)
        public FileExplorerWidget(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config,
            ISecurityManager security,
            string initialPath)
            : this(logger, themeManager, config, security)
        {
            // Override the path from config with explicit initialPath
            currentPath = initialPath ?? Directory.GetCurrentDirectory();
        }


        public override void Initialize()
        {
            try
            {
                theme = themeManager.CurrentTheme;
                BuildUI();
                LoadDirectory(currentPath);
                logger.Info("FileExplorer", "Widget initialized");
            }
            catch (Exception ex)
            {
                logger.Error("FileExplorer", $"Initialization failed: {ex.Message}", ex);
                throw; // Re-throw to let ErrorBoundary handle it
            }
        }

        private void BuildUI()
        {
            // Create standard frame
            frame = new StandardWidgetFrame(themeManager)
            {
                Title = "FILE EXPLORER"
            };
            frame.SetStandardShortcuts("Enter: Open", "Backspace: Up", "Del: Delete", "F5: Refresh", "?: Help");

            var mainPanel = new DockPanel
            {
                Background = new SolidColorBrush(theme.Background),
                LastChildFill = true,
                Margin = new Thickness(15)
            };

            // Path label
            pathLabel = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.Info),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            DockPanel.SetDock(pathLabel, Dock.Top);
            mainPanel.Children.Add(pathLabel);

            // Status bar
            statusLabel = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 5, 0, 0)
            };
            DockPanel.SetDock(statusLabel, Dock.Bottom);
            mainPanel.Children.Add(statusLabel);

            // File list
            fileListBox = new ListBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            fileListBox.KeyDown += FileListBox_KeyDown;
            fileListBox.MouseDoubleClick += FileListBox_MouseDoubleClick;

            mainPanel.Children.Add(fileListBox);

            frame.Content = mainPanel;
            Content = frame;
        }

        private void LoadDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    UpdateStatus($"Directory not found: {path}", theme.Error);
                    return;
                }

                currentPath = path;
                pathLabel.Text = $"Path: {currentPath}";
                currentItems.Clear();
                fileListBox.Items.Clear();

                var dirInfo = new DirectoryInfo(currentPath);

                // Add parent directory if not root
                if (dirInfo.Parent != null)
                {
                    currentItems.Add(dirInfo.Parent);
                    fileListBox.Items.Add(FormatItem(dirInfo.Parent, true));
                }

                // Add directories
                var dirs = dirInfo.GetDirectories().OrderBy(d => d.Name).ToList();
                foreach (var dir in dirs)
                {
                    if (!dir.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        currentItems.Add(dir);
                        fileListBox.Items.Add(FormatItem(dir, false));
                    }
                }

                // Add files
                var files = dirInfo.GetFiles().OrderBy(f => f.Name).ToList();
                foreach (var file in files)
                {
                    if (!file.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        currentItems.Add(file);
                        fileListBox.Items.Add(FormatItem(file, false));
                    }
                }

                UpdateStatus($"{dirs.Count} directories, {files.Count} files", theme.ForegroundSecondary);

                // Publish directory changed event
                EventBus.Publish(new DirectoryChangedEvent
                {
                    OldPath = null,
                    NewPath = currentPath,
                    ChangedAt = DateTime.Now
                });

                logger.Info("FileExplorer", $"Loaded directory: {currentPath}");
            }
            catch (UnauthorizedAccessException)
            {
                UpdateStatus("Access denied", theme.Error);
                logger.Error("FileExplorer", $"Access denied: {path}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", theme.Error);
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Widget,
                    ex,
                    $"Loading directory: {path}");
            }
        }

        private string FormatItem(FileSystemInfo item, bool isParent)
        {
            if (isParent)
                return "📁 ..";

            if (item is DirectoryInfo)
                return $"📁 {item.Name}";

            var file = item as FileInfo;
            var size = FormatFileSize(file.Length);
            var icon = GetFileIcon(file.Extension);
            return $"{icon} {file.Name} ({size})";
        }

        private string GetFileIcon(string extension)
        {
            switch (extension.ToLower())
            {
                case ".txt": case ".md": case ".log": return "📄";
                case ".cs": case ".ps1": case ".psm1": case ".psd1": case ".js": case ".py": return "📝";
                case ".exe": case ".dll": case ".bin": return "⚙️";
                case ".zip": case ".rar": case ".7z": case ".tar": case ".gz": return "📦";
                case ".jpg": case ".jpeg": case ".png": case ".gif": case ".bmp": return "🖼️";
                case ".mp3": case ".wav": case ".flac": case ".ogg": return "🎵";
                case ".mp4": case ".avi": case ".mkv": case ".mov": return "🎬";
                case ".pdf": return "📕";
                case ".json": case ".xml": case ".yaml": case ".yml": case ".toml": return "🔧";
                default: return "📄";
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }

        private void FileListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OpenSelectedItem();
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                NavigateUp();
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                LoadDirectory(currentPath);
                e.Handled = true;
            }
        }

        private void FileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedItem();
        }

        private void OpenSelectedItem()
        {
            if (fileListBox.SelectedIndex < 0 || fileListBox.SelectedIndex >= currentItems.Count)
                return;

            var item = currentItems[fileListBox.SelectedIndex];

            // Handle directory navigation
            if (item is DirectoryInfo dir)
            {
                LoadDirectory(dir.FullName);
                return;
            }

            // Handle file opening with security checks
            if (item is FileInfo file)
            {
                OpenFile(file);
            }
        }

        /// <summary>
        /// Opens a file with security validation and user confirmation for dangerous files.
        /// </summary>
        /// <param name="file">File to open</param>
        private void OpenFile(FileInfo file)
        {
            try
            {
                // Step 1: Security validation via SecurityManager
                if (!security.ValidateFileAccess(file.FullName, checkWrite: false))
                {
                    UpdateStatus("Access denied by security policy", theme.Error);
                    logger.Warning("FileExplorer",
                        $"File access denied by security policy: {file.FullName}");
                    return;
                }

                string extension = file.Extension.ToLowerInvariant();

                // Step 2: Check if file type is dangerous - BLOCK EXECUTION (security hardening)
                if (DangerousFileExtensions.Contains(extension))
                {
                    // SECURITY POLICY: Block dangerous file execution entirely
                    MessageBox.Show(
                        $"Execution of {extension} files is blocked for security.\n\n" +
                        $"File: {file.Name}\n\n" +
                        "This file type can run code on your computer and is not allowed to be opened from the file explorer. " +
                        "If you need to access this file, use Windows Explorer or another trusted application.",
                        "Security Policy - Execution Blocked",
                        MessageBoxButton.OK,
                        MessageBoxImage.Stop);

                    UpdateStatus($"BLOCKED: {file.Name} (dangerous file type)", theme.Error);
                    logger.Warning("FileExplorer",
                        $"SECURITY: Blocked execution attempt on dangerous file: {file.FullName} (extension: {extension})");
                    return;
                }

                // Step 3: Check if file type is recognized as safe
                else if (!SafeFileExtensions.Contains(extension))
                {
                    // Unknown extension - show generic warning
                    var result = ShowUnknownFileWarning(file);
                    if (result != MessageBoxResult.Yes)
                    {
                        UpdateStatus($"Cancelled: {file.Name}", theme.ForegroundSecondary);
                        return;
                    }

                    logger.Info("FileExplorer",
                        $"User confirmed opening unknown file type: {file.FullName} (extension: {extension})");
                }

                // Step 4: Publish file selected event (other widgets can listen)
                EventBus.Publish(new FileSelectedEvent
                {
                    FilePath = file.FullName,
                    FileName = file.Name,
                    FileSize = file.Length,
                    SelectedAt = DateTime.Now
                });

                // Step 5: Open file with default application
                Process.Start(new ProcessStartInfo
                {
                    FileName = file.FullName,
                    UseShellExecute = true
                });

                UpdateStatus($"Opened: {file.Name}", theme.Success);
                logger.Info("FileExplorer", $"Successfully opened file: {file.FullName}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Cannot open: {ex.Message}", theme.Error);
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Widget,
                    ex,
                    $"Opening file: {file.FullName}");
            }
        }

        /// <summary>
        /// Shows a security warning for dangerous file types.
        /// </summary>
        private MessageBoxResult ShowDangerousFileWarning(FileInfo file)
        {
            string message =
                $"⚠️ SECURITY WARNING ⚠️\n\n" +
                $"File: {file.Name}\n" +
                $"Type: {file.Extension.ToUpperInvariant()} (Executable/Script)\n" +
                $"Size: {FormatFileSize(file.Length)}\n" +
                $"Path: {file.DirectoryName}\n\n" +
                $"This file type can execute code on your computer.\n" +
                $"Only open files from trusted sources.\n\n" +
                $"Do you want to open this file?";

            return MessageBox.Show(
                message,
                "Security Warning - Dangerous File Type",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No  // Default to NO for safety
            );
        }

        /// <summary>
        /// Shows a warning for unknown file types.
        /// </summary>
        private MessageBoxResult ShowUnknownFileWarning(FileInfo file)
        {
            string message =
                $"Unknown File Type\n\n" +
                $"File: {file.Name}\n" +
                $"Extension: {file.Extension}\n" +
                $"Size: {FormatFileSize(file.Length)}\n\n" +
                $"This file type is not recognized as safe.\n" +
                $"Opening it may be unsafe.\n\n" +
                $"Do you want to open this file?";

            return MessageBox.Show(
                message,
                "Unknown File Type",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No
            );
        }

        private void NavigateUp()
        {
            var dirInfo = new DirectoryInfo(currentPath);
            if (dirInfo.Parent != null)
            {
                LoadDirectory(dirInfo.Parent.FullName);
            }
        }

        private void UpdateStatus(string message, Color color)
        {
            statusLabel.Text = message;
            statusLabel.Foreground = new SolidColorBrush(color);
        }

        public override Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["CurrentPath"] = currentPath
            };
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            if (state.TryGetValue("CurrentPath", out var path))
            {
                LoadDirectory(path.ToString());
            }
        }

        protected override void OnDispose()
        {
            fileListBox.KeyDown -= FileListBox_KeyDown;
            fileListBox.MouseDoubleClick -= FileListBox_MouseDoubleClick;
            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            theme = themeManager.CurrentTheme;
            BuildUI();
            LoadDirectory(currentPath);
        }
    }
}
