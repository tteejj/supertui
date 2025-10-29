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
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// PRODUCTION-QUALITY NOTES PANE
    /// Full CRUD: Create, Read, Update, Delete
    /// Features: Auto-save, fuzzy search, rename, markdown support, file watcher, command palette
    /// Keyboard-first navigation with terminal aesthetic
    /// </summary>
    public class NotesPane : PaneBase
    {
        #region Fields

        private readonly IConfigurationManager config;

        // UI Components - Three-panel layout
        private Grid mainLayout;
        private TextBox searchBox;
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

        // Command palette
        private Border commandPaletteBorder;
        private TextBox commandInput;
        private ListBox commandList;
        private bool isCommandPaletteVisible;

        // Debouncing
        private DispatcherTimer searchDebounceTimer;
        private DispatcherTimer autoSaveDebounceTimer;

        // Constants
        private const int SEARCH_DEBOUNCE_MS = 150;
        private const int AUTOSAVE_DEBOUNCE_MS = 1000;
        private const string BACKUP_EXTENSION = ".bak";

        #endregion

        #region Constructor

        public NotesPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager config)
            : base(logger, themeManager, projectContext)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            PaneName = "Notes";
            PaneIcon = "📝";

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
                await AutoSaveCurrentNoteAsync();
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

            // Three-column layout: Search/Filter | Note List | Editor
            mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) }); // Left panel
            mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Editor

            // Left panel contains search + note list
            var leftPanel = BuildLeftPanel();
            Grid.SetColumn(leftPanel, 0);
            mainLayout.Children.Add(leftPanel);

            // Editor panel
            var editorPanel = BuildEditorPanel();
            Grid.SetColumn(editorPanel, 1);
            mainLayout.Children.Add(editorPanel);

            // Command palette (overlay)
            commandPaletteBorder = BuildCommandPalette();
            commandPaletteBorder.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(commandPaletteBorder, 2);
            Panel.SetZIndex(commandPaletteBorder, 1000);
            mainLayout.Children.Add(commandPaletteBorder);

            // Set up keyboard shortcuts
            this.PreviewKeyDown += OnPreviewKeyDown;

            // Initialize notes
            UpdateNotesFolder();
            LoadAllNotes();
            SetupFileWatcher();

            return mainLayout;
        }

        private Grid BuildLeftPanel()
        {
            var theme = themeManager.CurrentTheme;
            var bgBrush = new SolidColorBrush(theme.Background);
            var fgBrush = new SolidColorBrush(theme.Foreground);
            var borderBrush = new SolidColorBrush(theme.Border);
            var transparentBrush = new SolidColorBrush(Colors.Transparent);

            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

            // Search box
            var searchContainer = new Border
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8),
                Height = 40
            };

            searchBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 10,
                Foreground = fgBrush,
                Background = transparentBrush,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            searchBox.TextChanged += OnSearchTextChanged;
            searchBox.GotFocus += (s, e) => searchBox.Text = searchBox.Text == "Search notes... (Ctrl+F)" ? "" : searchBox.Text;
            searchBox.LostFocus += (s, e) => searchBox.Text = string.IsNullOrEmpty(searchBox.Text) ? "Search notes... (Ctrl+F)" : searchBox.Text;
            searchBox.Text = "Search notes... (Ctrl+F)";

            searchContainer.Child = searchBox;
            Grid.SetRow(searchContainer, 0);
            panel.Children.Add(searchContainer);

            // Notes list
            var listBorder = new Border
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(0)
            };

            notesListBox = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 10,
                Foreground = fgBrush,
                Background = transparentBrush,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };
            notesListBox.SelectionChanged += OnNoteSelected;
            notesListBox.PreviewKeyDown += OnNotesListKeyDown;

            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, transparentBrush));
            itemStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, fgBrush));
            itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 6, 12, 6)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            notesListBox.ItemContainerStyle = itemStyle;

            listBorder.Child = notesListBox;
            Grid.SetRow(listBorder, 1);
            panel.Children.Add(listBorder);

            return panel;
        }

        private Grid BuildEditorPanel()
        {
            var theme = themeManager.CurrentTheme;
            var bgBrush = new SolidColorBrush(theme.Background);
            var fgBrush = new SolidColorBrush(theme.Foreground);
            var borderBrush = new SolidColorBrush(theme.Border);
            var transparentBrush = new SolidColorBrush(Colors.Transparent);

            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Editor
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status bar

            // Editor
            noteEditor = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 11,
                Foreground = fgBrush,
                Background = transparentBrush,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = "No note selected\n\nPress Ctrl+N to create a new note\nPress Ctrl+: for command palette"
            };
            noteEditor.TextChanged += OnEditorTextChanged;
            noteEditor.IsEnabled = false;

            Grid.SetRow(noteEditor, 0);
            panel.Children.Add(noteEditor);

            // Status bar
            var statusBorder = new Border
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Height = 24,
                Padding = new Thickness(12, 0, 12, 0)
            };

            statusBar = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 9,
                Foreground = fgBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Text = "Ready"
            };

            statusBorder.Child = statusBar;
            Grid.SetRow(statusBorder, 1);
            panel.Children.Add(statusBorder);

            return panel;
        }

        private Border BuildCommandPalette()
        {
            var theme = themeManager.CurrentTheme;
            var bgBrush = new SolidColorBrush(theme.Surface);
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
                FontSize = 12,
                Foreground = fgBrush,
                Background = transparentBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8),
                Height = 36
            };
            commandInput.TextChanged += OnCommandInputChanged;
            commandInput.PreviewKeyDown += OnCommandInputKeyDown;

            Grid.SetRow(commandInput, 0);
            grid.Children.Add(commandInput);

            // Command list
            commandList = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 10,
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
                    Log($"Failed to create notes folder '{currentNotesFolder}': {ex.Message}", LogLevel.Error);
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
                        Log($"Failed to create fallback notes folder: {fallbackEx.Message}", LogLevel.Error);
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
                Log($"Failed to load notes: {ex.Message}", LogLevel.Error);
                ShowStatus($"ERROR: Failed to load notes", isError: true);
            }

            FilterNotes();
        }

        private void FilterNotes()
        {
            var query = searchBox.Text;

            if (string.IsNullOrEmpty(query) || query == "Search notes... (Ctrl+F)")
            {
                filteredNotes = allNotes.ToList();
            }
            else
            {
                // Fuzzy search
                filteredNotes = allNotes
                    .Select(note => new { Note = note, Score = CalculateFuzzyScore(query.ToLower(), note.Name.ToLower()) })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Select(x => x.Note)
                    .ToList();
            }

            UpdateNotesList();
        }

        private void UpdateNotesList()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var selectedNote = currentNote;
                notesListBox.Items.Clear();

                if (!filteredNotes.Any())
                {
                    var placeholder = new TextBlock
                    {
                        Text = allNotes.Any() ? "No matching notes" : "No notes yet\nPress Ctrl+N to create one",
                        FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                        FontSize = 10,
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
                        Text = note.Name,
                        FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold
                    };
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
            if (hasUnsavedChanges && currentNote != null)
            {
                var result = MessageBox.Show(
                    $"Save changes to '{currentNote.Name}'?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                {
                    // Reselect current note
                    isLoadingNote = true;
                    var currentItem = notesListBox.Items.OfType<ListBoxItem>()
                        .FirstOrDefault(i => i.Tag is NoteMetadata n && n.FullPath == currentNote.FullPath);
                    if (currentItem != null)
                    {
                        notesListBox.SelectedItem = currentItem;
                    }
                    isLoadingNote = false;
                    return;
                }
                else if (result == MessageBoxResult.Yes)
                {
                    await SaveCurrentNoteAsync();
                }
            }

            isLoadingNote = true;

            try
            {
                if (!File.Exists(note.FullPath))
                {
                    ShowStatus($"ERROR: Note file not found", isError: true);
                    LoadAllNotes();
                    return;
                }

                var content = await Task.Run(() => File.ReadAllText(note.FullPath));

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    currentNote = note;
                    noteEditor.Text = content;
                    noteEditor.IsEnabled = true;
                    hasUnsavedChanges = false;
                    UpdateStatusBar();
                });

                Log($"Loaded note: {note.Name}");
            }
            catch (Exception ex)
            {
                Log($"Failed to load note: {ex.Message}", LogLevel.Error);
                ShowStatus($"ERROR: Failed to load note", isError: true);
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
            if (hasUnsavedChanges && currentNote != null)
            {
                var result = MessageBox.Show(
                    $"Save changes to '{currentNote.Name}'?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes)
                {
                    await SaveCurrentNoteAsync();
                }
            }

            // Prompt for note name
            var theme = themeManager.CurrentTheme;
            var dialog = new Window
            {
                Title = "New Note",
                Width = 400,
                Height = 120,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(theme.Surface)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var nameInput = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Margin = new Thickness(16),
                Padding = new Thickness(8),
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1)
            };

            Grid.SetRow(nameInput, 0);
            grid.Children.Add(nameInput);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 0, 16, 16)
            };

            var createBtn = new Button
            {
                Content = "Create",
                Width = 80,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(8, 4, 8, 4),
                FontFamily = new FontFamily("JetBrains Mono, Consolas")
            };
            createBtn.Click += async (s, e) =>
            {
                var noteName = nameInput.Text.Trim();
                if (string.IsNullOrEmpty(noteName))
                {
                    MessageBox.Show("Note name cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                dialog.DialogResult = true;
                dialog.Close();
                await CreateNoteFileAsync(noteName);
            };

            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 80,
                Padding = new Thickness(8, 4, 8, 4),
                FontFamily = new FontFamily("JetBrains Mono, Consolas")
            };
            cancelBtn.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(createBtn);
            buttonPanel.Children.Add(cancelBtn);
            Grid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            nameInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) createBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (e.Key == Key.Escape) dialog.Close();
            };

            nameInput.Focus();
            dialog.ShowDialog();
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

                await Task.Run(() => File.WriteAllText(fullPath, $"# {noteName}\n\n"));

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
            catch (Exception ex)
            {
                Log($"Failed to create note: {ex.Message}", LogLevel.Error);
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
                });

                currentNote.LastModified = DateTime.Now;
                hasUnsavedChanges = false;
                UpdateStatusBar();
                UpdateNotesList();

                Log($"Saved note: {currentNote.Name}");
                ShowStatus($"Saved: {currentNote.Name}");
            }
            catch (Exception ex)
            {
                Log($"Failed to save note: {ex.Message}", LogLevel.Error);
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

            var result = MessageBox.Show(
                $"Are you sure you want to delete '{currentNote.Name}'?\n\nThis action cannot be undone.",
                "Delete Note",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await Task.Run(() =>
                {
                    // Move to recycle bin would be better, but this works
                    File.Delete(currentNote.FullPath);

                    // Also delete backup if it exists
                    var backupPath = currentNote.FullPath + BACKUP_EXTENSION;
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                });

                Log($"Deleted note: {currentNote.Name}");
                ShowStatus($"Deleted: {currentNote.Name}");

                allNotes.Remove(currentNote);
                currentNote = null;
                hasUnsavedChanges = false;

                noteEditor.Text = "Note deleted\n\nPress Ctrl+N to create a new note";
                noteEditor.IsEnabled = false;

                FilterNotes();
            }
            catch (Exception ex)
            {
                Log($"Failed to delete note: {ex.Message}", LogLevel.Error);
                ShowStatus($"ERROR: Failed to delete note", isError: true);
                MessageBox.Show(
                    $"Failed to delete note: {ex.Message}",
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
                Background = new SolidColorBrush(theme.Surface)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var nameInput = new TextBox
            {
                Text = currentNote.Name,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 12,
                Margin = new Thickness(16),
                Padding = new Thickness(8),
                Background = new SolidColorBrush(theme.BackgroundSecondary),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1)
            };

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
            nameInput.Focus();
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

                await Task.Run(() => File.Move(oldPath, newPath));

                currentNote.Name = newName;
                currentNote.FullPath = newPath;
                currentNote.LastModified = DateTime.Now;

                UpdateStatusBar();
                FilterNotes();

                Log($"Renamed note to: {newName}");
                ShowStatus($"Renamed to: {newName}");
            }
            catch (Exception ex)
            {
                Log($"Failed to rename note: {ex.Message}", LogLevel.Error);
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
            if (e.Key == Key.Enter && notesListBox.SelectedItem != null)
            {
                noteEditor.Focus();
                e.Handled = true;
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Keyboard shortcuts
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.N:
                        CreateNewNote();
                        e.Handled = true;
                        break;

                    case Key.S:
                        _ = SaveCurrentNoteAsync();
                        e.Handled = true;
                        break;

                    case Key.D:
                        DeleteCurrentNote();
                        e.Handled = true;
                        break;

                    case Key.F:
                        searchBox.Focus();
                        searchBox.SelectAll();
                        e.Handled = true;
                        break;

                    case Key.OemSemicolon:
                        // Ctrl+: for command palette (doesn't conflict with MainWindow global palette)
                        if (!isCommandPaletteVisible)
                        {
                            ShowCommandPalette();
                            e.Handled = true;
                        }
                        break;
                }
            }
            else if (e.Key == Key.F2)
            {
                RenameCurrentNote();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && notesListBox.IsFocused)
            {
                DeleteCurrentNote();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (isCommandPaletteVisible)
                {
                    HideCommandPalette();
                    e.Handled = true;
                }
                else if (searchBox.IsFocused)
                {
                    searchBox.Text = "";
                    noteEditor.Focus();
                    e.Handled = true;
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
            commandInput.Focus();
            PopulateCommands("");
        }

        private void HideCommandPalette()
        {
            isCommandPaletteVisible = false;
            commandPaletteBorder.Visibility = Visibility.Collapsed;
            noteEditor.Focus();
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
                    FontSize = 11,
                    FontWeight = FontWeights.Bold
                };
                stackPanel.Children.Add(nameBlock);

                var descBlock = new TextBlock
                {
                    Text = cmd.Description,
                    FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                    FontSize = 9,
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
                commandList.Focus();
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
                commandInput.Focus();
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

                case "search":
                    searchBox.Focus();
                    searchBox.SelectAll();
                    break;

                case "export":
                    MessageBox.Show("Export feature coming soon!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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
                Log($"Failed to setup file watcher: {ex.Message}", LogLevel.Error);
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce file system events
            Application.Current?.Dispatcher.InvokeAsync(() =>
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
            if (currentNote == null)
            {
                statusBar.Text = "Ready";
                return;
            }

            var wordCount = noteEditor.Text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var charCount = noteEditor.Text.Length;
            var modifiedIndicator = hasUnsavedChanges ? " •" : "";

            statusBar.Text = $"{currentNote.Name}{modifiedIndicator} | {wordCount} words | {charCount} chars";
        }

        private void ShowStatus(string message, bool isError = false)
        {
            Application.Current?.Dispatcher.Invoke(() =>
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

        #endregion

        #region Overrides

        public override void Initialize()
        {
            base.Initialize();

            // Set initial focus to notes list for keyboard-first navigation
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                notesListBox?.Focus();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        protected override void OnProjectContextChanged(object sender, ProjectContextChangedEventArgs e)
        {
            Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                if (hasUnsavedChanges && currentNote != null)
                {
                    await SaveCurrentNoteAsync();
                }

                UpdateNotesFolder();
                LoadAllNotes();
                SetupFileWatcher();

                currentNote = null;
                hasUnsavedChanges = false;
                noteEditor.Text = "Project context changed\n\nSelect a note or create a new one";
                noteEditor.IsEnabled = false;
                UpdateStatusBar();
            });
        }

        protected override void OnDispose()
        {
            // Save on close if needed
            if (hasUnsavedChanges && currentNote != null)
            {
                try
                {
                    File.WriteAllText(currentNote.FullPath, noteEditor.Text);
                    Log($"Auto-saved note on close: {currentNote.Name}");
                }
                catch (Exception ex)
                {
                    Log($"Failed to auto-save on close: {ex.Message}", LogLevel.Error);
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

            base.OnDispose();
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
