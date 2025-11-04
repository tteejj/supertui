using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Extensions;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Enum to track what action should be performed when a file is selected from FileBrowser
    /// </summary>
    public enum FileAction
    {
        Open,   // O: Open file in editor
        Delete, // D: Delete the selected file
        Edit    // E: Open file in editor (same as Open)
    }

    /// <summary>
    /// PRODUCTION-QUALITY NOTES PANE
    /// Full CRUD: Create, Read, Update, Delete
    /// Features: Auto-save, fuzzy search, rename, file watcher, command palette
    /// Keyboard-first navigation with terminal aesthetic
    /// Note: Markdown rendering not yet implemented (plain text editor only)
    /// </summary>
    public class NotesPane : PaneBase
    {
        #region Fields

        private readonly IConfigurationManager config;
        private readonly IEventBus eventBus;
        private Action<Core.Events.TaskSelectedEvent> taskSelectedHandler;
        private Action<Core.Events.ProjectSelectedEvent> projectSelectedHandler;
        private Action<Core.Events.FileSelectedEvent> fileSelectedHandler;
        private Action<Core.Events.CommandExecutedFromPaletteEvent> commandExecutedHandler;
        private Action<Core.Events.RefreshRequestedEvent> refreshRequestedHandler;

        // UI Components - Two-mode layout
        private Grid mainLayout;
        private ListBox notesListBox;
        private TextBox noteEditor;
        private TextBlock statusBar;

        // State
        private string currentNotesFolder;
        private List<NoteMetadata> allNotes = new List<NoteMetadata>();
        private List<NoteMetadata> filteredNotes = new List<NoteMetadata>();
        private NoteMetadata currentNote;
        private FileSystemWatcher fileWatcher;
        private bool hasUnsavedChanges;
        private bool isLoadingNote;
        private FileAction pendingFileAction = FileAction.Open; // Track intent when opening FileBrowser

        // Simple notepad-style editor - no modal editing

        // Command palette
        private Border commandPaletteBorder;
        private TextBox commandInput;
        private ListBox commandList;
        private bool isCommandPaletteVisible;

        // Debouncing
        private DispatcherTimer searchDebounceTimer;
        private DispatcherTimer autoSaveDebounceTimer;

        // Cancellation for async operations
        private CancellationTokenSource disposalCancellation;
        private CancellationTokenSource stateRestoreCancellation;

        // State synchronization lock to prevent race conditions
        private readonly object stateLock = new object();

        // Constants
        private const int SEARCH_DEBOUNCE_MS = 150;
        private const int AUTOSAVE_DEBOUNCE_MS = 1000;
        private const string BACKUP_EXTENSION = ".bak";

        // Events
        public event EventHandler<string> FileBrowserRequested;

        #endregion

        #region Constructor

        public NotesPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager config,
            IEventBus eventBus)
            : base(logger, themeManager, projectContext)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            PaneName = "Notes";
            PaneIcon = "📝";

            // Initialize cancellation token
            disposalCancellation = new CancellationTokenSource();

            // Initialize timers
            searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SEARCH_DEBOUNCE_MS)
            };
            searchDebounceTimer.Tick += (s, e) =>
            {
                searchDebounceTimer.Stop();
                FilterNotes();
            };

            autoSaveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(AUTOSAVE_DEBOUNCE_MS)
            };
            autoSaveDebounceTimer.Tick += async (s, e) =>
            {
                autoSaveDebounceTimer.Stop();

                // Check if disposed before auto-saving
                if (!disposalCancellation.IsCancellationRequested)
                {
                    try
                    {
                        await AutoSaveCurrentNoteAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during disposal, ignore
                    }
                }
            };
        }

        #endregion

        #region Build UI

        protected override UIElement BuildContent()
        {
            // Get theme colors
            var theme = themeManager.CurrentTheme;
            var bgBrush = new SolidColorBrush(theme.Background);
            var fgBrush = new SolidColorBrush(theme.Foreground);
            var borderBrush = new SolidColorBrush(theme.Border);
            var borderActiveBrush = new SolidColorBrush(theme.BorderActive);

            mainLayout = new Grid();
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status bar

            // Notes list (shown when not editing)
            BuildNotesListView();
            Grid.SetRow(notesListBox, 0);
            mainLayout.Children.Add(notesListBox);

            // Editor (shown when editing, hidden by default)
            BuildEditorView();
            noteEditor.Visibility = Visibility.Collapsed;
            Grid.SetRow(noteEditor, 0);
            mainLayout.Children.Add(noteEditor);

            // TUI-style status line at bottom (like vim/emacs)
            var statusLineContainer = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = borderBrush,
                Padding = new Thickness(8, 4, 8, 4)
            };

            statusBar = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.Primary),
                Text = GetStatusLineText()
            };
            statusLineContainer.Child = statusBar;
            Grid.SetRow(statusLineContainer, 1);
            mainLayout.Children.Add(statusLineContainer);

            // Set up keyboard shortcuts
            this.PreviewKeyDown += OnPreviewKeyDown;

            // Initialize notes
            UpdateNotesFolder();
            LoadAllNotes();
            SetupFileWatcher();

            return mainLayout;
        }

        private void BuildNotesListView()
        {
            var theme = themeManager.CurrentTheme;
            var fgBrush = new SolidColorBrush(theme.Foreground);
            var transparentBrush = new SolidColorBrush(Colors.Transparent);
            var accentBrush = new SolidColorBrush(theme.Primary);

            notesListBox = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 20,
                Foreground = fgBrush,
                Background = transparentBrush,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(24)
            };
            notesListBox.SelectionChanged += OnNoteSelected;

            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, transparentBrush));
            itemStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, fgBrush));
            itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 8, 12, 8)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));

            notesListBox.ItemContainerStyle = itemStyle;

            VirtualizingPanel.SetIsVirtualizing(notesListBox, true);
            VirtualizingPanel.SetVirtualizationMode(notesListBox, VirtualizationMode.Recycling);
        }

        private void BuildEditorView()
        {
            var theme = themeManager.CurrentTheme;
            var fgBrush = new SolidColorBrush(theme.Foreground);
            var transparentBrush = new SolidColorBrush(Colors.Transparent);

            noteEditor = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Foreground = fgBrush,
                Background = transparentBrush,
                CaretBrush = fgBrush, // Explicit cursor color for visibility
                BorderThickness = new Thickness(0),
                Padding = new Thickness(24),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            noteEditor.TextChanged += OnEditorTextChanged;
            noteEditor.ApplyFocusStyling(themeManager);
        }

        private Border BuildCommandPalette()
        {
            var theme = themeManager.CurrentTheme;
            var bgBrush = new SolidColorBrush(theme.Background);
            var fgBrush = new SolidColorBrush(theme.Foreground);
            var borderBrush = new SolidColorBrush(theme.BorderActive);
            var transparentBrush = new SolidColorBrush(Colors.Transparent);

            var border = new Border
            {
                Width = 500,
                MaxHeight = 300,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 60, 0, 0),
                Background = bgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(0)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Command input
            commandInput = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Foreground = fgBrush,
                Background = transparentBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8),
                Height = 36
            };
            commandInput.TextChanged += OnCommandInputChanged;
            commandInput.PreviewKeyDown += OnCommandInputKeyDown;
            commandInput.ApplyFocusStyling(themeManager);

            Grid.SetRow(commandInput, 0);
            grid.Children.Add(commandInput);

            // Command list
            commandList = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Foreground = fgBrush,
                Background = transparentBrush,
                BorderThickness = new Thickness(0),
                MaxHeight = 240
            };
            commandList.PreviewKeyDown += OnCommandListKeyDown;
            commandList.MouseDoubleClick += (s, e) =>
            {
                if (commandList.SelectedItem is ListBoxItem item)
                {
                    ExecuteCommand(item.Tag as string);
                }
            };

            var cmdItemStyle = new Style(typeof(ListBoxItem));
            cmdItemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, transparentBrush));
            cmdItemStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, fgBrush));
            cmdItemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 6, 12, 6)));
            cmdItemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            commandList.ItemContainerStyle = cmdItemStyle;

            Grid.SetRow(commandList, 1);
            grid.Children.Add(commandList);

            border.Child = grid;
            return border;
        }

        #endregion

        #region Notes Management

        private void UpdateNotesFolder()
        {
            if (projectContext.CurrentProject != null)
            {
                var projectName = projectContext.CurrentProject.Name;
                var baseNotesPath = config.Get<string>("NotesFolder") ??
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SuperTUI", "Notes");

                currentNotesFolder = Path.Combine(baseNotesPath, SanitizeFolderName(projectName));
            }
            else
            {
                currentNotesFolder = config.Get<string>("NotesFolder") ??
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SuperTUI", "Notes");
            }

            // Create folder if it doesn't exist
            if (!Directory.Exists(currentNotesFolder))
            {
                try
                {
                    Directory.CreateDirectory(currentNotesFolder);
                    Log($"Created notes folder: {currentNotesFolder}");
                    ShowStatus($"Notes folder created: {currentNotesFolder}");
                }
                catch (Exception ex)
                {
                    ErrorHandlingPolicy.Handle(
                        ErrorCategory.IO,
                        ex,
                        $"Creating notes folder '{currentNotesFolder}'",
                        logger);

                    ShowStatus($"ERROR: Could not create notes folder - {ex.Message}", isError: true);

                    // Fallback to a known-good path
                    try
                    {
                        var fallbackPath = Path.Combine(Path.GetTempPath(), "SuperTUI", "Notes");
                        Directory.CreateDirectory(fallbackPath);
                        currentNotesFolder = fallbackPath;
                        Log($"Using fallback notes folder: {fallbackPath}", LogLevel.Warning);
                        ShowStatus($"Using temporary notes folder: {fallbackPath}", isError: false);
                    }
                    catch (Exception fallbackEx)
                    {
                        ErrorHandlingPolicy.Handle(
                            ErrorCategory.IO,
                            fallbackEx,
                            "Creating fallback notes folder in temp directory",
                            logger);

                        ShowStatus($"CRITICAL: Cannot create notes folder anywhere", isError: true);
                    }
                }
            }
            else
            {
                Log($"Using existing notes folder: {currentNotesFolder}");
            }
        }

        private void LoadAllNotes()
        {
            allNotes.Clear();

            if (!Directory.Exists(currentNotesFolder))
            {
                UpdateNotesList();
                return;
            }

            try
            {
                var noteFiles = Directory.GetFiles(currentNotesFolder, "*.md")
                    .Concat(Directory.GetFiles(currentNotesFolder, "*.txt"))
                    .Select(path => new FileInfo(path))
                    .Where(f => !f.Name.EndsWith(BACKUP_EXTENSION))
                    .OrderByDescending(f => f.LastWriteTime);

                foreach (var file in noteFiles)
                {
                    allNotes.Add(new NoteMetadata
                    {
                        Name = Path.GetFileNameWithoutExtension(file.Name),
                        FullPath = file.FullName,
                        LastModified = file.LastWriteTime,
                        Extension = file.Extension
                    });
                }

                Log($"Loaded {allNotes.Count} notes from {currentNotesFolder}");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Loading notes from folder '{currentNotesFolder}'",
                    logger);

                ShowStatus($"ERROR: Failed to load notes", isError: true);
            }

            FilterNotes();
        }

        private void FilterNotes()
        {
            // No search - just show all notes sorted by last modified
            filteredNotes = allNotes.ToList();
            UpdateNotesList();
        }

        private void UpdateNotesList()
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                var selectedNote = currentNote;
                notesListBox.Items.Clear();

                if (!filteredNotes.Any())
                {
                    var placeholder = new TextBlock
                    {
                        Text = allNotes.Any() ? "No matching notes" : "No notes yet\nPress A to create one",
                        FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                        FontSize = 18,
                        FontStyle = FontStyles.Italic,
                        Opacity = 0.5,
                        TextAlignment = TextAlignment.Center,
                        Padding = new Thickness(12)
                    };
                    notesListBox.Items.Add(placeholder);
                    return;
                }

                foreach (var note in filteredNotes)
                {
                    var stackPanel = new StackPanel();

                    var nameBlock = new TextBlock
                    {
                        FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                        FontSize = 18,
                        FontWeight = FontWeights.Bold
                    };

                    // Add unsaved indicator
                    string displayName = note.Name;
                    if (currentNote == note && hasUnsavedChanges)
                    {
                        displayName = "* " + displayName;
                    }

                    nameBlock.Text = displayName;

                    stackPanel.Children.Add(nameBlock);

                    var infoBlock = new TextBlock
                    {
                        Text = $"{note.LastModified:MMM d, HH:mm} • {note.Extension}",
                        FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                        FontSize = 8,
                        Opacity = 0.6,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    stackPanel.Children.Add(infoBlock);

                    var item = new ListBoxItem
                    {
                        Content = stackPanel,
                        Tag = note
                    };

                    notesListBox.Items.Add(item);

                    if (selectedNote != null && note.FullPath == selectedNote.FullPath)
                    {
                        notesListBox.SelectedItem = item;
                    }
                }
            });
        }

        private async void OnNoteSelected(object sender, SelectionChangedEventArgs e)
        {
            if (isLoadingNote) return;

            if (notesListBox.SelectedItem is ListBoxItem item && item.Tag is NoteMetadata note)
            {
                await LoadNoteAsync(note);
            }
        }

        private async Task LoadNoteAsync(NoteMetadata note)
        {
            // Auto-save current note if needed (no prompts)
            if (hasUnsavedChanges && currentNote != null)
            {
                await SaveCurrentNoteAsync();
            }

            isLoadingNote = true;

            try
            {
                if (!File.Exists(note.FullPath))
                {
                    ShowStatus($"Note file not found: {note.Name}", isError: true);
                    LoadAllNotes();
                    return;
                }

                // Load content
                var content = await Task.Run(() => File.ReadAllText(note.FullPath), disposalCancellation.Token);

                if (disposalCancellation.IsCancellationRequested)
                    return;

                // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher
                // EventBus may call LoadNoteAsync from a background thread where Application.Current is null
                this.Dispatcher.Invoke(() =>
                {
                    if (!disposalCancellation.IsCancellationRequested)
                    {
                        // Unsubscribe from TextChanged to prevent race condition
                        // Setting noteEditor.Text triggers TextChanged, which would set hasUnsavedChanges = true
                        // We unsubscribe, set the text, then resubscribe to avoid this
                        noteEditor.TextChanged -= OnEditorTextChanged;

                        try
                        {
                            // Lock currentNote assignment to prevent race conditions
                            lock (stateLock)
                            {
                                currentNote = note;
                            }
                            noteEditor.Text = content;
                            hasUnsavedChanges = false;

                            // Switch to editor mode
                            notesListBox.Visibility = Visibility.Collapsed;
                            noteEditor.Visibility = Visibility.Visible;

                            UpdateStatusBar();
                        }
                        finally
                        {
                            // Resubscribe to TextChanged
                            noteEditor.TextChanged += OnEditorTextChanged;
                        }

                        // Focus AFTER resubscribing to avoid focus events during setup
                        System.Windows.Input.Keyboard.Focus(noteEditor);
                    }
                });

                Log($"Loaded note: {note.Name}");
            }
            catch (OperationCanceledException)
            {
                // Expected during disposal
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Loading note from '{note.FullPath}'",
                    logger);

                ShowStatus($"Failed to load note: {note.Name}", isError: true);
            }
            finally
            {
                isLoadingNote = false;
            }
        }

        #endregion

        #region CRUD Operations

        private async void CreateNewNote()
        {
            // Auto-save current note if needed (no prompts)
            if (hasUnsavedChanges && currentNote != null)
            {
                await SaveCurrentNoteAsync();
            }

            // Create temp file immediately with timestamp
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var tempFileName = $"temp_{timestamp}.txt";
            var tempFilePath = Path.Combine(currentNotesFolder, tempFileName);

            try
            {
                // Create empty file
                await Task.Run(() => File.WriteAllText(tempFilePath, ""), disposalCancellation.Token);

                // Create note metadata
                currentNote = new NoteMetadata
                {
                    Name = Path.GetFileNameWithoutExtension(tempFileName),
                    FullPath = tempFilePath,
                    LastModified = DateTime.Now,
                    Extension = ".txt"
                };

                // Switch to editor mode
                noteEditor.Text = "";
                hasUnsavedChanges = false;
                notesListBox.Visibility = Visibility.Collapsed;
                noteEditor.Visibility = Visibility.Visible;
                System.Windows.Input.Keyboard.Focus(noteEditor);

                // Add to notes list
                allNotes.Insert(0, currentNote);
                FilterNotes();

                UpdateStatusBar();
                ShowStatus($"New note: {tempFileName}");

                Log($"Created new temp file: {tempFileName}");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Creating new temp note file in '{currentNotesFolder}'",
                    logger);

                ShowStatus($"Failed to create note", isError: true);
            }
        }

        private async Task CreateNoteFileAsync(string noteName)
        {
            try
            {
                var fileName = SanitizeFileName(noteName) + ".md";
                var fullPath = Path.Combine(currentNotesFolder, fileName);

                if (File.Exists(fullPath))
                {
                    MessageBox.Show(
                        $"A note named '{noteName}' already exists",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await Task.Run(() => File.WriteAllText(fullPath, $"# {noteName}\n\n"), disposalCancellation.Token);

                // Check if cancelled before updating UI
                if (disposalCancellation.IsCancellationRequested)
                    return;

                var note = new NoteMetadata
                {
                    Name = noteName,
                    FullPath = fullPath,
                    LastModified = DateTime.Now,
                    Extension = ".md"
                };

                allNotes.Insert(0, note);
                FilterNotes();

                // Select the new note
                var newItem = notesListBox.Items.OfType<ListBoxItem>()
                    .FirstOrDefault(i => i.Tag is NoteMetadata n && n.FullPath == fullPath);
                if (newItem != null)
                {
                    notesListBox.SelectedItem = newItem;
                }

                Log($"Created note: {noteName}");
                ShowStatus($"Created note: {noteName}");
            }
            catch (OperationCanceledException)
            {
                // Expected during disposal, ignore
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Creating note file '{noteName}.md'",
                    logger);

                ShowStatus($"ERROR: Failed to create note", isError: true);
                MessageBox.Show(
                    $"Failed to create note: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task SaveCurrentNoteAsync()
        {
            if (currentNote == null || !hasUnsavedChanges) return;

            try
            {
                var content = noteEditor.Text;

                // Atomic save: write to temp file, then rename
                var tempPath = currentNote.FullPath + ".tmp";
                await Task.Run(() =>
                {
                    File.WriteAllText(tempPath, content);

                    // Optional: create backup
                    if (File.Exists(currentNote.FullPath))
                    {
                        File.Copy(currentNote.FullPath, currentNote.FullPath + BACKUP_EXTENSION, true);
                    }

                    File.Move(tempPath, currentNote.FullPath, true);
                }, disposalCancellation.Token);

                // Check if cancelled before updating UI
                if (disposalCancellation.IsCancellationRequested)
                    return;

                currentNote.LastModified = DateTime.Now;
                hasUnsavedChanges = false;
                UpdateStatusBar();
                UpdateNotesList();

                Log($"Saved note: {currentNote.Name}");
                ShowStatus($"Saved: {currentNote.Name}");
            }
            catch (OperationCanceledException)
            {
                // Expected during disposal, ignore
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Saving note '{currentNote.Name}' to '{currentNote.FullPath}'",
                    logger);

                ShowStatus($"ERROR: Failed to save note", isError: true);
                MessageBox.Show(
                    $"Failed to save note: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task AutoSaveCurrentNoteAsync()
        {
            if (currentNote != null && hasUnsavedChanges)
            {
                await SaveCurrentNoteAsync();
            }
        }

        private async void DeleteCurrentNote()
        {
            if (currentNote == null) return;

            var noteName = currentNote.Name;

            try
            {
                await Task.Run(() =>
                {
                    File.Delete(currentNote.FullPath);

                    // Also delete backup if it exists
                    var backupPath = currentNote.FullPath + BACKUP_EXTENSION;
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                }, disposalCancellation.Token);

                if (disposalCancellation.IsCancellationRequested)
                    return;

                Log($"Deleted note: {noteName}");
                ShowStatus($"Deleted: {noteName}");

                allNotes.Remove(currentNote);
                // Lock currentNote assignment to prevent race conditions
                lock (stateLock)
                {
                    currentNote = null;
                }
                hasUnsavedChanges = false;

                // Return to notes list
                noteEditor.Visibility = Visibility.Collapsed;
                notesListBox.Visibility = Visibility.Visible;
                System.Windows.Input.Keyboard.Focus(notesListBox);

                FilterNotes();
            }
            catch (OperationCanceledException)
            {
                // Expected during disposal
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Deleting note '{noteName}'",
                    logger);

                ShowStatus($"Failed to delete note: {noteName}", isError: true);
            }
        }

        private void OpenExternalNote(FileAction action = FileAction.Open)
        {
            // Store the intended action for when the file is selected
            pendingFileAction = action;

            // Request FileBrowser from parent
            FileBrowserRequested?.Invoke(this, currentNotesFolder);

            // Show appropriate status message based on action
            string statusMessage = action switch
            {
                FileAction.Delete => "Select a .txt file to delete...",
                FileAction.Edit => "Select a .txt file to edit...",
                _ => "Select a .txt file to open..."
            };
            ShowStatus(statusMessage);
            Log($"OpenExternalNote: Requested FileBrowser for action: {action}");
        }

        public void OnFileBrowserFileSelected(string filePath)
        {
            // Called by parent when FileBrowser selects a file
            if (!File.Exists(filePath) || !filePath.EndsWith(".txt"))
            {
                ShowStatus($"Invalid file: {Path.GetFileName(filePath)}");
                return;
            }

            try
            {
                // Load the external note
                var noteName = Path.GetFileNameWithoutExtension(filePath);
                var note = new NoteMetadata
                {
                    Name = noteName,
                    FullPath = filePath,
                    LastModified = File.GetLastWriteTime(filePath),
                    Extension = Path.GetExtension(filePath)
                };

                currentNote = note;
                hasUnsavedChanges = false;
                noteEditor.Text = File.ReadAllText(filePath);
                ShowStatus($"Opened: {noteName}");
                Log($"Loaded external note: {filePath}");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Loading external note from '{filePath}'",
                    logger);

                ShowStatus($"ERROR: {ex.Message}");
            }
        }

        private async void CloseNoteEditor()
        {
            if (currentNote == null) return;

            // Auto-save if needed (no prompts)
            if (hasUnsavedChanges)
            {
                await SaveCurrentNoteAsync();
            }

            // Switch back to notes list mode
            // Lock currentNote assignment to prevent race conditions
            lock (stateLock)
            {
                currentNote = null;
            }
            hasUnsavedChanges = false;
            noteEditor.Visibility = Visibility.Collapsed;
            notesListBox.Visibility = Visibility.Visible;
            System.Windows.Input.Keyboard.Focus(notesListBox);

            UpdateStatusBar();
        }

        private void SaveAsTempFile()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var tempFileName = $"temp_{timestamp}.txt";
                var tempFilePath = Path.Combine(currentNotesFolder, tempFileName);

                File.WriteAllText(tempFilePath, noteEditor.Text);

                // Update current note metadata
                currentNote.Name = tempFileName;
                currentNote.FullPath = tempFilePath;
                currentNote.LastModified = DateTime.Now;
                hasUnsavedChanges = false;

                Log($"Saved as temp file: {tempFileName}");
                ShowStatus($"Saved as temporary file: {tempFileName}");

                // Reload notes list to show the new temp file
                LoadAllNotes();
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Saving note as temporary file in '{currentNotesFolder}'",
                    logger);

                ShowStatus($"ERROR: Failed to save temp file", isError: true);
                MessageBox.Show(
                    $"Failed to save temp file: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RenameCurrentNote()
        {
            if (currentNote == null) return;

            // Prompt for new name
            var theme = themeManager.CurrentTheme;
            var dialog = new Window
            {
                Title = "Rename Note",
                Width = 400,
                Height = 120,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(theme.Background)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var nameInput = new TextBox
            {
                Text = currentNote.Name,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Margin = new Thickness(16),
                Padding = new Thickness(8),
                Background = new SolidColorBrush(theme.Background),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1)
            };
            nameInput.ApplyFocusStyling(themeManager);

            Grid.SetRow(nameInput, 0);
            grid.Children.Add(nameInput);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 0, 16, 16)
            };

            var renameBtn = new Button
            {
                Content = "Rename",
                Width = 80,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(8, 4, 8, 4),
                FontFamily = new FontFamily("JetBrains Mono, Consolas")
            };
            renameBtn.Click += async (s, e) =>
            {
                var newName = nameInput.Text.Trim();
                if (string.IsNullOrEmpty(newName))
                {
                    MessageBox.Show("Note name cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                dialog.DialogResult = true;
                dialog.Close();
                await RenameNoteFileAsync(newName);
            };

            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 80,
                Padding = new Thickness(8, 4, 8, 4),
                FontFamily = new FontFamily("JetBrains Mono, Consolas")
            };
            cancelBtn.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(renameBtn);
            buttonPanel.Children.Add(cancelBtn);
            Grid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            nameInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) renameBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (e.Key == Key.Escape) dialog.Close();
            };

            nameInput.SelectAll();
            System.Windows.Input.Keyboard.Focus(nameInput);
            dialog.ShowDialog();
        }

        private async Task RenameNoteFileAsync(string newName)
        {
            if (currentNote == null) return;

            try
            {
                var oldPath = currentNote.FullPath;
                var newFileName = SanitizeFileName(newName) + currentNote.Extension;
                var newPath = Path.Combine(Path.GetDirectoryName(oldPath), newFileName);

                if (File.Exists(newPath) && newPath != oldPath)
                {
                    MessageBox.Show(
                        $"A note named '{newName}' already exists",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await Task.Run(() => File.Move(oldPath, newPath), disposalCancellation.Token);

                // Check if cancelled before updating UI
                if (disposalCancellation.IsCancellationRequested)
                    return;

                currentNote.Name = newName;
                currentNote.FullPath = newPath;
                currentNote.LastModified = DateTime.Now;

                UpdateStatusBar();
                FilterNotes();

                Log($"Renamed note to: {newName}");
                ShowStatus($"Renamed to: {newName}");
            }
            catch (OperationCanceledException)
            {
                // Expected during disposal, ignore
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.IO,
                    ex,
                    $"Renaming note from '{currentNote.Name}' to '{newName}'",
                    logger);

                ShowStatus($"ERROR: Failed to rename note", isError: true);
                MessageBox.Show(
                    $"Failed to rename note: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Event Handlers

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            searchDebounceTimer.Stop();
            searchDebounceTimer.Start();
        }

        private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoadingNote || currentNote == null) return;

            hasUnsavedChanges = true;
            UpdateStatusBar();

            // Trigger auto-save
            autoSaveDebounceTimer.Stop();
            autoSaveDebounceTimer.Start();
        }

        private void OnNotesListKeyDown(object sender, KeyEventArgs e)
        {
            // Arrow keys for navigation
            // Other keys handled in OnPreviewKeyDown
        }

        private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Shortcuts in editor are now handled via ShortcutManager
            // Let the TextBox handle everything (arrow keys, typing, etc.)
        }

        // Removed all modal mode handlers and vim navigation - no longer needed
        // NotesPane now uses simple notepad-style editing without modes

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Check if user is typing in editor FIRST (before any shortcut processing)
            // This prevents shortcuts from blocking text input
            bool isTypingInEditor = noteEditor != null &&
                                   noteEditor.IsFocused &&
                                   noteEditor.Visibility == Visibility.Visible;

            // Handle critical editor shortcuts that work even while typing
            // IMPORTANT: Handle Ctrl+Shift+S BEFORE Ctrl+S to avoid blocking rename
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                // Ctrl+Shift+S: Rename/Save As (works while typing)
                logger?.Log(LogLevel.Debug, "NotesPane", "Ctrl+Shift+S pressed - calling RenameCurrentNote");
                if (currentNote != null)
                {
                    RenameCurrentNote();
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.S && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+S (WITHOUT Shift): Save current note (works while typing)
                if (currentNote != null && hasUnsavedChanges)
                {
                    _ = SaveCurrentNoteAsync();
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Escape && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                // Esc: Close editor (works while typing)
                if (noteEditor != null && noteEditor.Visibility == Visibility.Visible && currentNote != null)
                {
                    CloseNoteEditor();
                    e.Handled = true;
                    return;
                }
            }

            // If user is typing in editor, DON'T process pane shortcuts at all
            // Let them type normally without shortcuts interfering
            if (isTypingInEditor)
            {
                // User is typing - don't mark as handled, let editor receive the key
                return;
            }

            // Check ShortcutManager for pane shortcuts (A, E, D, O, W, Enter, etc.)
            // Only when NOT typing in editor
            var shortcuts = ShortcutManager.Instance;
            if (shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers, null, PaneName))
            {
                e.Handled = true;
                return;
            }
        }

        // Old handler code preserved for reference (now using ShortcutManager)
        private void OnPreviewKeyDown_Old(object sender, KeyEventArgs e)
        {
            if (System.Windows.Input.Keyboard.FocusedElement is TextBox)
            {
                return; // Let the TextBox handle the key
            }

            if (e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.A:
                        CreateNewNote();
                        e.Handled = true;
                        break;
                    case Key.O:
                        OpenExternalNote();
                        e.Handled = true;
                        break;
                    case Key.D:
                        if (currentNote != null)
                        {
                            DeleteCurrentNote();
                            e.Handled = true;
                        }
                        break;
                    case Key.W:
                        // Save current note
                        if (currentNote != null && hasUnsavedChanges)
                        {
                            _ = SaveCurrentNoteAsync();
                            e.Handled = true;
                        }
                        break;
                    case Key.E:
                    case Key.Enter:
                        // Open selected note for editing or focus editor if note already open
                        if (notesListBox.SelectedItem is NoteMetadata note)
                        {
                            _ = LoadNoteAsync(note);
                            e.Handled = true;
                        }
                        else if (currentNote != null)
                        {
                            System.Windows.Input.Keyboard.Focus(noteEditor);
                            e.Handled = true;
                        }
                        break;
                }
            }
        }

        #endregion

        #region Command Palette

        private void ShowCommandPalette()
        {
            isCommandPaletteVisible = true;
            commandPaletteBorder.Visibility = Visibility.Visible;
            commandInput.Text = "";
            System.Windows.Input.Keyboard.Focus(commandInput);
            PopulateCommands("");
        }

        private void HideCommandPalette()
        {
            isCommandPaletteVisible = false;
            commandPaletteBorder.Visibility = Visibility.Collapsed;
            System.Windows.Input.Keyboard.Focus(noteEditor);
        }

        private void PopulateCommands(string query)
        {
            commandList.Items.Clear();

            var commands = new[]
            {
                new { Name = "new", Description = "Create new note (Ctrl+N)" },
                new { Name = "save", Description = "Save current note (Ctrl+S)" },
                new { Name = "delete", Description = "Delete current note (Ctrl+D)" },
                new { Name = "rename", Description = "Rename current note (F2)" },
                new { Name = "search", Description = "Focus search box (Ctrl+F)" },
                new { Name = "export", Description = "Export note (coming soon)" }
            };

            var filtered = string.IsNullOrEmpty(query)
                ? commands
                : commands
                    .Select(c => new { Command = c, Score = CalculateFuzzyScore(query.ToLower(), c.Name) })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Select(x => x.Command);

            foreach (var cmd in filtered)
            {
                var stackPanel = new StackPanel();

                var nameBlock = new TextBlock
                {
                    Text = cmd.Name,
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold
                };
                stackPanel.Children.Add(nameBlock);

                var descBlock = new TextBlock
                {
                    Text = cmd.Description,
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 18,
                    Opacity = 0.6,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                stackPanel.Children.Add(descBlock);

                var item = new ListBoxItem
                {
                    Content = stackPanel,
                    Tag = cmd.Name
                };

                commandList.Items.Add(item);
            }

            if (commandList.Items.Count > 0)
            {
                commandList.SelectedIndex = 0;
            }
        }

        private void OnCommandInputChanged(object sender, TextChangedEventArgs e)
        {
            PopulateCommands(commandInput.Text);
        }

        private void OnCommandInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                System.Windows.Input.Keyboard.Focus(commandList);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (commandList.SelectedItem is ListBoxItem item)
                {
                    ExecuteCommand(item.Tag as string);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                HideCommandPalette();
                e.Handled = true;
            }
        }

        private void OnCommandListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (commandList.SelectedItem is ListBoxItem item)
                {
                    ExecuteCommand(item.Tag as string);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                HideCommandPalette();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && commandList.SelectedIndex == 0)
            {
                // Return focus to input when at top of list
                System.Windows.Input.Keyboard.Focus(commandInput);
                e.Handled = true;
            }
        }

        private void ExecuteCommand(string command)
        {
            HideCommandPalette();

            switch (command)
            {
                case "new":
                    CreateNewNote();
                    break;

                case "save":
                    _ = SaveCurrentNoteAsync();
                    break;

                case "delete":
                    DeleteCurrentNote();
                    break;

                case "rename":
                    RenameCurrentNote();
                    break;

                case "export":
                    ShowStatus("Export feature coming soon");
                    break;
            }
        }

        #endregion

        #region File Watcher

        private void SetupFileWatcher()
        {
            if (!Directory.Exists(currentNotesFolder)) return;

            try
            {
                fileWatcher?.Dispose();

                fileWatcher = new FileSystemWatcher(currentNotesFolder)
                {
                    Filter = "*.*",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                fileWatcher.Created += OnFileSystemChanged;
                fileWatcher.Deleted += OnFileSystemChanged;
                fileWatcher.Renamed += OnFileSystemChanged;
                fileWatcher.Changed += OnFileSystemChanged;

                Log("File watcher enabled");
            }
            catch (Exception ex)
            {
                ErrorHandlingPolicy.Handle(
                    ErrorCategory.Internal,
                    ex,
                    $"Setting up file watcher for '{currentNotesFolder}'",
                    logger);
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce file system events
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(() =>
            {
                if (e.Name.EndsWith(".tmp") || e.Name.EndsWith(BACKUP_EXTENSION))
                    return;

                Log($"File system change detected: {e.ChangeType} - {e.Name}");

                // Reload notes list, but preserve current note if still editing
                var wasCurrentNoteAffected = currentNote != null && e.FullPath == currentNote.FullPath;

                if (wasCurrentNoteAffected && e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    ShowStatus($"WARNING: Current note was deleted externally", isError: true);
                }

                LoadAllNotes();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        #endregion

        #region Status & Helpers

        private void UpdateStatusBar()
        {
            if (statusBar != null)
            {
                statusBar.Text = GetStatusLineText();
            }
        }

        /// <summary>
        /// Get context-aware status line text based on current pane state
        /// Shows available keyboard actions like vim/emacs status lines
        /// </summary>
        private string GetStatusLineText()
        {
            // When editing a note
            if (currentNote != null && noteEditor != null && noteEditor.Visibility == Visibility.Visible)
            {
                return "[Ctrl+S] Save  [Ctrl+N] New Note  [Ctrl+D] Delete  [Esc] Exit";
            }

            // When selecting from list
            if (notesListBox != null && notesListBox.SelectedItem != null)
            {
                return "[Enter] Open  [Ctrl+N] New  [Ctrl+D] Delete";
            }

            // Default state (no selection)
            return "[A] New Note  [O] Open External  [E] Edit  [?] Help";
        }

        private void ShowStatus(string message, bool isError = false)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                var theme = themeManager.CurrentTheme;
                statusBar.Text = message;

                if (isError)
                {
                    statusBar.Foreground = new SolidColorBrush(theme.Error);
                }
                else
                {
                    statusBar.Foreground = new SolidColorBrush(theme.Foreground);
                }

                // Reset status after 3 seconds
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    UpdateStatusBar();
                    statusBar.Foreground = new SolidColorBrush(theme.Foreground);
                };
                timer.Start();
            });
        }

        private int CalculateFuzzyScore(string query, string target)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target))
                return 0;

            int score = 0;
            int queryIndex = 0;
            int consecutiveMatches = 0;
            bool previousWasMatch = false;

            for (int targetIndex = 0; targetIndex < target.Length && queryIndex < query.Length; targetIndex++)
            {
                if (query[queryIndex] == target[targetIndex])
                {
                    score += 10;

                    if (previousWasMatch)
                    {
                        consecutiveMatches++;
                        score += consecutiveMatches * 5;
                    }
                    else
                    {
                        consecutiveMatches = 1;
                    }

                    if (targetIndex == 0 || target[targetIndex - 1] == ' ')
                    {
                        score += 20;
                    }

                    previousWasMatch = true;
                    queryIndex++;
                }
                else
                {
                    previousWasMatch = false;
                    consecutiveMatches = 0;
                }
            }

            if (queryIndex != query.Length)
                return 0;

            score -= (target.Length - query.Length) * 2;
            return Math.Max(0, score);
        }

        private string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        private string SanitizeFolderName(string name)
        {
            return SanitizeFileName(name);
        }

        private void AddHighlightedText(TextBlock textBlock, string text, string searchQuery)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var theme = themeManager.CurrentTheme;
            var highlightBrush = new SolidColorBrush(theme.Success);
            var normalBrush = new SolidColorBrush(theme.Foreground);

            int queryIndex = 0;
            for (int i = 0; i < text.Length && queryIndex < searchQuery.Length; i++)
            {
                bool isMatch = char.ToLower(text[i]) == char.ToLower(searchQuery[queryIndex]);

                var run = new System.Windows.Documents.Run(text[i].ToString())
                {
                    Foreground = isMatch ? highlightBrush : normalBrush,
                    FontWeight = isMatch ? FontWeights.Bold : FontWeights.Normal
                };

                if (isMatch) queryIndex++;
                textBlock.Inlines.Add(run);
            }
        }

        #endregion

        #region Overrides

        public override void Initialize()
        {
            base.Initialize();

            // Register pane-specific shortcuts with ShortcutManager (migrated from hardcoded handlers)
            RegisterPaneShortcuts();

            // Subscribe to theme changes
            themeManager.ThemeChanged += OnThemeChanged;

            // Subscribe to TaskSelectedEvent for cross-pane communication
            taskSelectedHandler = OnTaskSelected;
            eventBus.Subscribe(taskSelectedHandler);

            // Subscribe to ProjectSelectedEvent for project context awareness
            projectSelectedHandler = OnProjectSelected;
            eventBus.Subscribe(projectSelectedHandler);

            // Subscribe to FileSelectedEvent for seamless file browsing → note editing workflow
            fileSelectedHandler = OnFileSelected;
            eventBus.Subscribe(fileSelectedHandler);
            logger.Log(LogLevel.Debug, "NotesPane", "Subscribed to FileSelectedEvent via EventBus");

            // Subscribe to CommandExecutedFromPaletteEvent for command palette coordination
            commandExecutedHandler = OnCommandExecutedFromPalette;
            eventBus.Subscribe(commandExecutedHandler);

            // Subscribe to RefreshRequestedEvent for global refresh (Ctrl+R)
            refreshRequestedHandler = OnRefreshRequested;
            eventBus.Subscribe(refreshRequestedHandler);

            // Set initial focus to notes list for keyboard-first navigation
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(() =>
            {
                if (notesListBox != null) System.Windows.Input.Keyboard.Focus(notesListBox);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Register all NotesPane shortcuts with ShortcutManager
        /// These shortcuts only execute when this pane is focused
        /// </summary>
        private void RegisterPaneShortcuts()
        {
            var shortcuts = ShortcutManager.Instance;

            // Escape: Close editor
            shortcuts.RegisterForPane(PaneName, Key.Escape, ModifierKeys.None,
                () =>
                {
                    if (noteEditor != null && noteEditor.Visibility == Visibility.Visible && currentNote != null)
                        CloseNoteEditor();
                },
                "Close editor");

            // Ctrl+S: Save note
            shortcuts.RegisterForPane(PaneName, Key.S, ModifierKeys.Control,
                () =>
                {
                    if (currentNote != null && hasUnsavedChanges)
                        _ = SaveCurrentNoteAsync();
                },
                "Save current note");

            // Shift+; (Shift+OemSemicolon): Show command palette
            shortcuts.RegisterForPane(PaneName, Key.OemSemicolon, ModifierKeys.Shift,
                () =>
                {
                    if (!noteEditor.IsFocused || noteEditor.SelectionStart == 0)
                        ShowCommandPalette();
                },
                "Show command palette");

            // A (no modifiers): Create new note
            shortcuts.RegisterForPane(PaneName, Key.A, ModifierKeys.None,
                () => CreateNewNote(),
                "Create new note");

            // O (no modifiers): Open external note
            shortcuts.RegisterForPane(PaneName, Key.O, ModifierKeys.None,
                () => OpenExternalNote(FileAction.Open),
                "Open external note");

            // D (no modifiers): Open FileBrowser to select and delete a note
            shortcuts.RegisterForPane(PaneName, Key.D, ModifierKeys.None,
                () => OpenExternalNote(FileAction.Delete),
                "Open file browser to delete note");

            // W (no modifiers): Save note
            shortcuts.RegisterForPane(PaneName, Key.W, ModifierKeys.None,
                () => { if (currentNote != null && hasUnsavedChanges) _ = SaveCurrentNoteAsync(); },
                "Save current note");

            // E (no modifiers): Open FileBrowser to select and edit a note
            shortcuts.RegisterForPane(PaneName, Key.E, ModifierKeys.None,
                () => OpenExternalNote(FileAction.Edit),
                "Open file browser to edit note");

            // Ctrl+Shift+S: Save As (rename current note)
            shortcuts.RegisterForPane(PaneName, Key.S, ModifierKeys.Control | ModifierKeys.Shift,
                () => {
                    if (currentNote != null)
                    {
                        RenameCurrentNote();
                    }
                },
                "Save As (rename note)");

            // Enter (no modifiers): Edit note
            shortcuts.RegisterForPane(PaneName, Key.Enter, ModifierKeys.None,
                () => {
                    // If editor is visible, just focus it
                    if (noteEditor != null && noteEditor.Visibility == Visibility.Visible && currentNote != null)
                    {
                        if (noteEditor != null) System.Windows.Input.Keyboard.Focus(noteEditor);
                        return;
                    }

                    // Otherwise, try to load selected note from list
                    if (notesListBox != null && notesListBox.SelectedIndex >= 0 && notesListBox.SelectedIndex < notesListBox.Items.Count)
                    {
                        var item = notesListBox.Items[notesListBox.SelectedIndex];
                        if (item is ListBoxItem listBoxItem && listBoxItem.Tag is NoteMetadata note)
                        {
                            _ = LoadNoteAsync(note);
                        }
                    }
                },
                "Edit note");
        }

        /// <summary>
        /// Override to handle when pane gains focus - focus appropriate control
        /// Added state locking to prevent race conditions during focus restoration
        /// </summary>
        protected override void OnPaneGainedFocus()
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(() => {
                lock (stateLock)
                {
                    // Determine which control should have focus based on current state
                    if (noteEditor != null &&
                        noteEditor.Visibility == Visibility.Visible &&
                        currentNote != null)
                    {
                        // If editing a note, return focus to editor
                        System.Windows.Input.Keyboard.Focus(noteEditor);
                    }
                    else if (notesListBox != null)
                    {
                        // Default: focus the notes list
                        System.Windows.Input.Keyboard.Focus(notesListBox);
                    }
                }
            }, DispatcherPriority.Render);
        }

        private void OnTaskSelected(Core.Events.TaskSelectedEvent evt)
        {
            // Task selected - could filter notes in the future
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(() =>
            {
                if (evt.Task != null)
                {
                    Log($"Task selected: {evt.Task.Title}");
                }
            });
        }

        /// <summary>
        /// Handle ProjectSelectedEvent from other panes (e.g., ProjectsPane)
        /// Switches the notes folder to the project's folder
        /// </summary>
        private void OnProjectSelected(Core.Events.ProjectSelectedEvent evt)
        {
            if (evt?.Project == null) return;

            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(async () =>
            {
                logger.Log(LogLevel.Info, "NotesPane",
                    $"Project selected from {evt.SourceWidget}: {evt.Project.Name}");

                // Save current note if needed before switching
                if (hasUnsavedChanges && currentNote != null)
                {
                    await SaveCurrentNoteAsync();
                }

                // The project context will be updated by PaneBase
                // which will trigger OnProjectContextChanged
                // That method will call UpdateNotesFolder() and reload notes

                // Show status message
                ShowStatus($"Switched to project: {evt.Project.Name}");
            });
        }

        /// <summary>
        /// Handle CommandExecutedFromPaletteEvent from command palette
        /// Opens new note editor when "New Note" command is executed
        /// </summary>
        private void OnCommandExecutedFromPalette(Core.Events.CommandExecutedFromPaletteEvent evt)
        {
            if (evt == null) return;

            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                // Check if command is related to note creation
                var commandLower = evt.CommandName?.ToLower() ?? "";
                if (commandLower == "notes" || commandLower == "new note" || commandLower == "create note")
                {
                    logger.Log(LogLevel.Info, "NotesPane",
                        $"Command palette executed '{evt.CommandName}' - opening new note editor");

                    // Open new note editor
                    CreateNewNote();
                }
            });
        }

        /// <summary>
        /// Handle FileSelectedEvent from FileBrowserPane
        /// Opens text files (.md, .txt) in the editor, shows message for other types
        /// Transfer focus SYNCHRONOUSLY before async operations to prevent race condition
        /// </summary>
        private void OnFileSelected(Core.Events.FileSelectedEvent evt)
        {
            logger?.Log(LogLevel.Debug, "NotesPane", $"OnFileSelected called with FilePath: {evt?.FilePath}");
            if (evt == null || string.IsNullOrEmpty(evt.FilePath))
            {
                logger?.Log(LogLevel.Warning, "NotesPane", "OnFileSelected: event or FilePath is null/empty");
                return;
            }

            logger?.Log(LogLevel.Debug, "NotesPane", $"this.Dispatcher is null: {this.Dispatcher == null}");

            // Queue async file operations on UI thread
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher
            // EventBus may call this from a background thread where Application.Current is null
            var task = this.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    logger.Log(LogLevel.Info, "NotesPane",
                        $"File selected from FileBrowser: {evt.FileName}");

                // Get file extension
                var extension = Path.GetExtension(evt.FilePath)?.ToLowerInvariant();

                // Check if it's a text file we can operate on
                if (extension == ".md" || extension == ".txt")
                {
                    try
                    {
                        // Handle the action based on what the user requested (O/D/E)
                        if (pendingFileAction == FileAction.Delete)
                        {
                            // DELETE ACTION: Confirm and delete the selected file
                            var fileName = Path.GetFileName(evt.FilePath);
                            var result = MessageBox.Show(
                                $"Are you sure you want to delete '{fileName}'?",
                                "Confirm Delete",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (result == MessageBoxResult.Yes)
                            {
                                try
                                {
                                    File.Delete(evt.FilePath);

                                    // Also delete backup if it exists
                                    var backupPath = evt.FilePath + BACKUP_EXTENSION;
                                    if (File.Exists(backupPath))
                                    {
                                        File.Delete(backupPath);
                                    }

                                    Log($"Deleted file from FileBrowser: {evt.FilePath}");
                                    ShowStatus($"Deleted: {fileName}");

                                    // Remove from notes list if it's in there
                                    var noteToRemove = allNotes.FirstOrDefault(n => n.FullPath == evt.FilePath);
                                    if (noteToRemove != null)
                                    {
                                        allNotes.Remove(noteToRemove);
                                        FilterNotes();
                                    }
                                }
                                catch (Exception deleteEx)
                                {
                                    ErrorHandlingPolicy.Handle(
                                        ErrorCategory.IO,
                                        deleteEx,
                                        $"Deleting file from FileBrowser: '{evt.FilePath}'",
                                        logger);

                                    ShowStatus($"ERROR: Failed to delete file", isError: true);
                                }
                            }

                            // Close FileBrowser after delete operation (whether confirmed or cancelled)
                            eventBus?.Publish(new Core.Events.CloseFileBrowserEvent
                            {
                                Reason = "Delete operation completed",
                                RequestingPane = PaneName
                            });
                        }
                        else
                        {
                            // OPEN/EDIT ACTION: Load the file into the editor
                            // Save current note if needed before switching
                            if (hasUnsavedChanges && currentNote != null)
                            {
                                // Save and restore focus around MessageBox
                                var previousFocus = Keyboard.FocusedElement;

                                var result = MessageBox.Show(
                                    $"Save changes to '{currentNote.Name}'?",
                                    "Unsaved Changes",
                                    MessageBoxButton.YesNoCancel,
                                    MessageBoxImage.Question);

                                if (previousFocus is UIElement element)
                                {
                                    System.Windows.Input.Keyboard.Focus(element);
                                }

                                if (result == MessageBoxResult.Cancel)
                                {
                                    return; // Don't open new file
                                }
                                else if (result == MessageBoxResult.Yes)
                                {
                                    await SaveCurrentNoteAsync();
                                }
                            }

                            // Load the selected file
                            var note = new NoteMetadata
                            {
                                Name = Path.GetFileNameWithoutExtension(evt.FilePath),
                                FullPath = evt.FilePath,
                                LastModified = File.GetLastWriteTime(evt.FilePath),
                                Extension = extension
                            };

                            await LoadNoteAsync(note);
                            ShowStatus($"Opened: {evt.FileName}");
                            Log($"Loaded file from FileBrowser: {evt.FilePath}");

                            // CRITICAL: Publish event to request FileBrowser closure and focus this pane
                            // This ensures the user can see the note they just opened
                            eventBus?.Publish(new Core.Events.CloseFileBrowserEvent
                            {
                                Reason = "File opened in NotesPane",
                                RequestingPane = PaneName
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorHandlingPolicy.Handle(
                            ErrorCategory.IO,
                            ex,
                            $"Handling file from FileBrowser: '{evt.FilePath}'",
                            logger);

                        ShowStatus($"ERROR: Failed to process file", isError: true);
                    }
                }
                else
                {
                    // Not a text file we can open
                    ShowStatus($"Cannot open {extension} files in notes editor", isError: false);
                    Log($"File type {extension} not supported in NotesPane");
                }
                }
                catch (Exception ex)
                {
                    logger?.Log(LogLevel.Error, "NotesPane", $"OnFileSelected: Unhandled exception in async handler: {ex.Message}");
                    logger?.Log(LogLevel.Error, "NotesPane", $"Stack trace: {ex.StackTrace}");
                    ShowStatus($"ERROR: Failed to process file selection", isError: true);
                }
            });

            if (task != null)
            {
                logger?.Log(LogLevel.Debug, "NotesPane", $"InvokeAsync task created, Status: {task.Status}");
                task.Task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        logger?.Log(LogLevel.Error, "NotesPane", $"InvokeAsync task faulted: {t.Exception?.Message}");
                        logger?.Log(LogLevel.Error, "NotesPane", $"Stack trace: {t.Exception?.StackTrace}");
                    }
                    else
                    {
                        logger?.Log(LogLevel.Debug, "NotesPane", $"InvokeAsync task completed successfully");
                    }
                }, System.Threading.Tasks.TaskScheduler.Default);
            }
            else
            {
                logger?.Log(LogLevel.Error, "NotesPane", "InvokeAsync returned null - Application.Current or Dispatcher is null");
            }
        }

        protected override void OnProjectContextChanged(object sender, ProjectContextChangedEventArgs e)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(async () =>
            {
                if (hasUnsavedChanges && currentNote != null)
                {
                    await SaveCurrentNoteAsync();
                }

                UpdateNotesFolder();
                LoadAllNotes();
                SetupFileWatcher();

                // Lock currentNote assignment to prevent race conditions
                lock (stateLock)
                {
                    currentNote = null;
                }
                hasUnsavedChanges = false;
                noteEditor.Text = "Project context changed\n\nSelect a note or create a new one";
                noteEditor.IsEnabled = false;
                UpdateStatusBar();
            });
        }

        public override PaneState SaveState()
        {
            var state = new Dictionary<string, object>
            {
                // Basic state
                ["SelectedNotePath"] = currentNote?.FullPath,
                ["SelectedNoteName"] = currentNote?.Name,
                ["SelectedNoteIndex"] = notesListBox?.SelectedIndex ?? -1,
                ["ScrollPosition"] = GetScrollPosition(),

                // FOCUS MEMORY - Track exact user state
                ["FocusedControl"] = GetCurrentFocusedControl(),
                ["IsEditorFocused"] = noteEditor?.IsFocused ?? false,

                // EDITOR STATE - Remember everything about the editor
                ["EditorText"] = noteEditor?.Text,
                ["EditorCursorPos"] = noteEditor?.CaretIndex ?? 0,
                ["EditorSelectionStart"] = noteEditor?.SelectionStart ?? 0,
                ["EditorSelectionLength"] = noteEditor?.SelectionLength ?? 0,
                ["EditorScrollPosition"] = GetEditorScrollPosition(),
                ["HasUnsavedChanges"] = hasUnsavedChanges,

                // COMMAND PALETTE STATE
                ["IsCommandPaletteVisible"] = isCommandPaletteVisible,
                ["CommandText"] = commandInput?.Text,
                ["CommandCursorPos"] = commandInput?.CaretIndex ?? 0,

                // LIST STATE
                ["NotesListScrollPosition"] = GetNotesListScrollPosition(),
                ["FilteredNoteCount"] = filteredNotes?.Count ?? 0,

                // AUTO-SAVE STATE
                ["LastAutoSaveTime"] = lastAutoSaveTime
            };

            return new PaneState
            {
                PaneType = "NotesPane",
                CustomData = state
            };
        }

        private string GetCurrentFocusedControl()
        {
            if (noteEditor?.IsFocused == true) return "Editor";
            if (notesListBox?.IsFocused == true) return "NotesList";
            return "None";
        }

        private double GetEditorScrollPosition()
        {
            if (noteEditor == null) return 0;
            // TextBox doesn't have built-in scroll position, but we can approximate
            // based on line index
            var lineIndex = noteEditor.GetLineIndexFromCharacterIndex(noteEditor.CaretIndex);
            return lineIndex;
        }

        private double GetNotesListScrollPosition()
        {
            if (notesListBox == null) return 0;
            var scrollViewer = FindVisualChild<ScrollViewer>(notesListBox);
            return scrollViewer?.VerticalOffset ?? 0;
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private DateTime lastAutoSaveTime = DateTime.MinValue;

        /// <summary>
        /// Helper to safely extract values from deserialized JSON Dictionary
        /// Handles both direct values and JsonElement objects
        /// </summary>
        private T GetValueOrDefault<T>(Dictionary<string, object> data, string key, T defaultValue = default)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            // Handle JsonElement from System.Text.Json deserialization
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                try
                {
                    if (typeof(T) == typeof(string))
                        return (T)(object)jsonElement.GetString();
                    if (typeof(T) == typeof(int))
                        return (T)(object)jsonElement.GetInt32();
                    if (typeof(T) == typeof(bool))
                        return (T)(object)jsonElement.GetBoolean();
                    if (typeof(T) == typeof(double))
                        return (T)(object)jsonElement.GetDouble();
                    if (typeof(T) == typeof(DateTime))
                        return (T)(object)jsonElement.GetDateTime();

                    // Fallback: try to deserialize
                    return System.Text.Json.JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                }
                catch
                {
                    return defaultValue;
                }
            }

            // Direct value - try to cast/convert
            try
            {
                if (value is T directValue)
                    return directValue;

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public override void RestoreState(PaneState state)
        {
            if (state?.CustomData == null) return;

            var data = state.CustomData as Dictionary<string, object>;
            if (data == null) return;

            // Cancel any in-flight restoration
            stateRestoreCancellation?.Cancel();
            stateRestoreCancellation = new CancellationTokenSource();

            // Store for deferred restoration
            pendingRestoreData = data;

            // Restore unsaved changes flag
            hasUnsavedChanges = GetValueOrDefault(data, "HasUnsavedChanges", false);

            // Use dispatcher to ensure UI is ready
            // CRITICAL: Use Render priority to ensure restoration completes BEFORE focus operations
            // Focus operations run at Input priority (5), so Render (7) ensures state is restored first
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await RestoreDetailedStateAsync(data, stateRestoreCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected during workspace switch
                }
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private Dictionary<string, object> pendingRestoreData;

        /// <summary>
        /// Restore detailed pane state with focus memory
        /// Broken into smaller focused methods for clarity
        /// </summary>
        private async Task RestoreDetailedStateAsync(Dictionary<string, object> data, CancellationToken token)
        {
            try
            {
                // Restore note selection
                await RestoreNoteSelectionAsync(data, token);

                // Restore editor state
                await RestoreEditorStateAsync(data, token);

                // Restore command palette state
                await RestoreCommandPaletteStateAsync(data, token);

                // Restore scroll positions
                await RestoreScrollPositionsAsync(data, token);

                // Small delay to ensure layout complete
                await Task.Delay(50, token);

                // Restore focus
                await RestoreFocusToControlAsync(data, token);
            }
            catch (OperationCanceledException)
            {
                logger?.Log(LogLevel.Debug, "NotesPane", "State restoration cancelled");
            }
            catch (Exception ex)
            {
                logger?.Log(LogLevel.Error, "NotesPane", $"Error in RestoreDetailedStateAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore note selection by index or path
        /// </summary>
        private async Task RestoreNoteSelectionAsync(Dictionary<string, object> data, CancellationToken token)
        {
            // Restore note selection by index first (more reliable)
            var index = GetValueOrDefault(data, "SelectedNoteIndex", -1);
            if (index >= 0 && index < notesListBox.Items.Count)
            {
                token.ThrowIfCancellationRequested();
                notesListBox.SelectedIndex = index;

                // Load the selected note
                if (notesListBox.SelectedItem is ListBoxItem item && item.Tag is NoteMetadata note)
                {
                    await LoadNoteAsync(note);
                }
            }
            // Fallback to path-based selection
            else
            {
                var notePathStr = GetValueOrDefault<string>(data, "SelectedNotePath", null);
                if (!string.IsNullOrEmpty(notePathStr))
                {
                    token.ThrowIfCancellationRequested();
                    // Check if it's an unsaved note
                    var noteName = GetValueOrDefault<string>(data, "SelectedNoteName", null);
                    if (noteName != null && notePathStr == null)
                    {
                        // Create unsaved note placeholder
                        lock (stateLock)
                        {
                            currentNote = new NoteMetadata
                            {
                                Name = noteName,
                                FullPath = null
                            };
                        }
                        noteEditor.IsEnabled = true;
                    }
                    else if (File.Exists(notePathStr))
                    {
                        var note = allNotes.FirstOrDefault(n => n.FullPath == notePathStr);
                        if (note != null)
                        {
                            await LoadNoteAsync(note);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Restore editor text, cursor position, and selection
        /// </summary>
        private Task RestoreEditorStateAsync(Dictionary<string, object> data, CancellationToken token)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            return this.Dispatcher.InvokeAsync(() =>
            {
                lock (stateLock)
                {
                    if (token.IsCancellationRequested) return;

                    var editorText = GetValueOrDefault<string>(data, "EditorText", null);
                    if (editorText != null)
                    {
                        noteEditor.Text = editorText;

                        // Restore cursor and selection
                        noteEditor.CaretIndex = GetValueOrDefault(data, "EditorCursorPos", 0);
                        noteEditor.SelectionStart = GetValueOrDefault(data, "EditorSelectionStart", 0);
                        noteEditor.SelectionLength = GetValueOrDefault(data, "EditorSelectionLength", 0);

                        // Restore scroll position
                        var lineIndex = GetValueOrDefault(data, "EditorScrollPosition", 0);
                        if (lineIndex > 0)
                        {
                            var charIndex = noteEditor.GetCharacterIndexFromLineIndex(lineIndex);
                            noteEditor.ScrollToLine(lineIndex);
                        }
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Render).Task;
        }

        /// <summary>
        /// Restore command palette state if it was visible
        /// </summary>
        private Task RestoreCommandPaletteStateAsync(Dictionary<string, object> data, CancellationToken token)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            return this.Dispatcher.InvokeAsync(() =>
            {
                lock (stateLock)
                {
                    if (token.IsCancellationRequested) return;

                    if (GetValueOrDefault(data, "IsCommandPaletteVisible", false))
                    {
                        ShowCommandPalette();

                        commandInput.Text = GetValueOrDefault<string>(data, "CommandText", "");
                        commandInput.CaretIndex = GetValueOrDefault(data, "CommandCursorPos", 0);
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Render).Task;
        }

        /// <summary>
        /// Restore scroll positions for list and main view
        /// </summary>
        private Task RestoreScrollPositionsAsync(Dictionary<string, object> data, CancellationToken token)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            return this.Dispatcher.InvokeAsync(() =>
            {
                lock (stateLock)
                {
                    if (token.IsCancellationRequested) return;

                    // Restore list scroll position
                    var listScroll = GetValueOrDefault(data, "NotesListScrollPosition", 0.0);
                    if (listScroll > 0)
                    {
                        var scrollViewer = FindVisualChild<ScrollViewer>(notesListBox);
                        scrollViewer?.ScrollToVerticalOffset(listScroll);
                    }

                    // Restore main scroll position
                    var scrollPos = GetValueOrDefault(data, "ScrollPosition", 0.0);
                    if (scrollPos > 0)
                    {
                        SetScrollPosition(scrollPos);
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Render).Task;
        }

        /// <summary>
        /// Restore focus to the control that was focused before
        /// </summary>
        private Task RestoreFocusToControlAsync(Dictionary<string, object> data, CancellationToken token)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            return this.Dispatcher.InvokeAsync(() =>
            {
                lock (stateLock)
                {
                    if (token.IsCancellationRequested) return;

                    var focusedControl = GetValueOrDefault<string>(data, "FocusedControl", "NotesList");

                    switch (focusedControl)
                    {
                        case "Editor":
                            if (noteEditor != null && noteEditor.Visibility == Visibility.Visible)
                            {
                                System.Windows.Input.Keyboard.Focus(noteEditor);
                            }
                            break;
                        case "NotesList":
                        default:
                            if (notesListBox != null)
                            {
                                System.Windows.Input.Keyboard.Focus(notesListBox);
                            }
                            break;
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Render).Task;
        }

        private double GetScrollPosition()
        {
            if (noteEditor == null) return 0;

            var scrollViewer = FindScrollViewer(noteEditor);
            return scrollViewer?.VerticalOffset ?? 0;
        }

        private void SetScrollPosition(object scrollPos)
        {
            if (noteEditor == null || scrollPos == null) return;

            var offset = Convert.ToDouble(scrollPos);
            var scrollViewer = FindScrollViewer(noteEditor);
            scrollViewer?.ScrollToVerticalOffset(offset);
        }

        private ScrollViewer FindScrollViewer(DependencyObject obj)
        {
            if (obj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is ScrollViewer scrollViewer)
                    return scrollViewer;

                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.Invoke(() =>
            {
                ApplyTheme();
            });
        }

        private void ApplyTheme()
        {
            var theme = themeManager.CurrentTheme;

            var bgBrush = new SolidColorBrush(theme.Background);
            var fgBrush = new SolidColorBrush(theme.Foreground);
            var borderBrush = new SolidColorBrush(theme.Border);
            var accentBrush = new SolidColorBrush(theme.Primary);
            var surfaceBrush = new SolidColorBrush(theme.Surface);

            // Update all controls - use transparent backgrounds to show single pane background
            if (notesListBox != null)
            {
                notesListBox.Foreground = fgBrush;
                notesListBox.Background = Brushes.Transparent;
            }

            if (noteEditor != null)
            {
                noteEditor.Foreground = fgBrush;
                noteEditor.Background = Brushes.Transparent;
            }

            if (statusBar != null)
            {
                statusBar.Foreground = accentBrush;

                // Update status line container
                if (statusBar.Parent is Border statusContainer)
                {
                    statusContainer.Background = surfaceBrush;
                    statusContainer.BorderBrush = borderBrush;
                }
            }

            this.InvalidateVisual();
        }

        protected override void OnDispose()
        {
            // Cancel all async operations FIRST
            disposalCancellation?.Cancel();
            stateRestoreCancellation?.Cancel();

            // Unsubscribe from event bus to prevent memory leaks
            if (taskSelectedHandler != null)
            {
                eventBus.Unsubscribe(taskSelectedHandler);
            }

            if (projectSelectedHandler != null)
            {
                eventBus.Unsubscribe(projectSelectedHandler);
            }

            if (fileSelectedHandler != null)
            {
                eventBus.Unsubscribe(fileSelectedHandler);
            }

            if (commandExecutedHandler != null)
            {
                eventBus.Unsubscribe(commandExecutedHandler);
            }

            if (refreshRequestedHandler != null)
            {
                eventBus.Unsubscribe(refreshRequestedHandler);
            }

            // Unsubscribe from theme changes
            themeManager.ThemeChanged -= OnThemeChanged;

            // Unsubscribe from editor events to prevent memory leak
            if (noteEditor != null)
            {
                noteEditor.TextChanged -= OnEditorTextChanged;
            }

            // Save on close if needed (synchronous to ensure it completes)
            if (hasUnsavedChanges && currentNote != null && !disposalCancellation.IsCancellationRequested)
            {
                try
                {
                    File.WriteAllText(currentNote.FullPath, noteEditor.Text);
                    Log($"Auto-saved note on close: {currentNote.Name}");
                }
                catch (Exception ex)
                {
                    ErrorHandlingPolicy.Handle(
                        ErrorCategory.IO,
                        ex,
                        $"Auto-saving note '{currentNote.Name}' on pane close",
                        logger);
                }
            }

            // Clean up timers
            searchDebounceTimer?.Stop();
            autoSaveDebounceTimer?.Stop();

            // Clean up file watcher
            if (fileWatcher != null)
            {
                fileWatcher.EnableRaisingEvents = false;
                fileWatcher.Dispose();
            }

            // Dispose cancellation token sources
            disposalCancellation?.Dispose();
            stateRestoreCancellation?.Dispose();

            base.OnDispose();
        }

        /// <summary>
        /// Handle RefreshRequestedEvent - reload all notes from disk
        /// </summary>
        private void OnRefreshRequested(Core.Events.RefreshRequestedEvent evt)
        {
            // CRITICAL: Use this.Dispatcher, not Application.Current.Dispatcher (EventBus may call from background thread)
            this.Dispatcher.InvokeAsync(() =>
            {
                LoadAllNotes();
                Log("NotesPane refreshed (RefreshRequestedEvent)");
            });
        }

        #endregion

        #region Note Metadata

        private class NoteMetadata
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public DateTime LastModified { get; set; }
            public string Extension { get; set; }
        }

        #endregion
    }
}
