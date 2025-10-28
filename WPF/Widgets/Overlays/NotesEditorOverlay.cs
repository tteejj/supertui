using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets.Overlays
{
    /// <summary>
    /// Notes editor overlay for quick note editing (center zone)
    /// Provides multi-line text editing for task notes
    /// </summary>
    public class NotesEditorOverlay : UserControl
    {
        private readonly ITaskService taskService;
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly TaskItem task;

        private TextBox notesBox;
        private TextBlock statusText;

        public event Action<TaskItem> NotesSaved;
        public event Action Cancelled;

        public NotesEditorOverlay(TaskItem task, ITaskService taskService)
        {
            this.task = task ?? throw new ArgumentNullException(nameof(task));
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.logger = Logger.Instance;
            this.themeManager = ThemeManager.Instance;

            BuildUI();
            LoadNotes();
        }

        private void BuildUI()
        {
            var theme = themeManager.CurrentTheme;

            var mainPanel = new Border
            {
                Background = new SolidColorBrush(theme.Surface),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(20),
                Margin = new Thickness(100, 50, 100, 50),
                MaxWidth = 700,
                MaxHeight = 600
            };

            var formPanel = new StackPanel();

            // Title
            var titleText = new TextBlock
            {
                Text = $"Edit Notes: {task.Title}",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 0, 0, 15)
            };
            formPanel.Children.Add(titleText);

            // Notes editor
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 400,
                Margin = new Thickness(0, 0, 0, 15)
            };

            notesBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                MinHeight = 300,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            scrollViewer.Content = notesBox;
            formPanel.Children.Add(scrollViewer);

            // Hint text
            var hintText = new TextBlock
            {
                Text = "[Ctrl+S] Save  [Esc] Cancel",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(
                    (byte)(theme.Foreground.R * 0.6),
                    (byte)(theme.Foreground.G * 0.6),
                    (byte)(theme.Foreground.B * 0.6)
                )),
                Margin = new Thickness(0, 0, 0, 15)
            };
            formPanel.Children.Add(hintText);

            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var saveButton = new Button
            {
                Content = "Save Notes",
                Width = 120,
                Height = 35,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            saveButton.Click += OnSave;

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 120,
                Height = 35,
                FontSize = 12,
                Foreground = new SolidColorBrush(theme.Foreground),
                Background = new SolidColorBrush(theme.Background),
                BorderBrush = new SolidColorBrush(theme.Primary),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            cancelButton.Click += (s, e) => Cancelled?.Invoke();

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            formPanel.Children.Add(buttonPanel);

            // Status text
            statusText = new TextBlock
            {
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Secondary),
                TextWrapping = TextWrapping.Wrap
            };
            formPanel.Children.Add(statusText);

            mainPanel.Child = formPanel;
            this.Content = mainPanel;
            this.Focusable = true;

            // Keyboard shortcuts
            this.KeyDown += OnKeyDown;

            // Focus notes box when loaded
            this.Loaded += (s, e) => notesBox.Focus();
        }

        private void LoadNotes()
        {
            // Notes is a List<TaskNote>, convert to text
            if (task.Notes != null && task.Notes.Any())
            {
                notesBox.Text = string.Join("\n---\n", task.Notes.Select(n => n.Content));
            }
            statusText.Text = $"Editing notes for task ID: {task.Id}";
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+S to save
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                OnSave(this, null);
                e.Handled = true;
            }
            // Escape to cancel
            else if (e.Key == Key.Escape)
            {
                Cancelled?.Invoke();
                e.Handled = true;
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            try
            {
                // Convert text to List<TaskNote>
                if (!string.IsNullOrWhiteSpace(notesBox.Text))
                {
                    if (task.Notes == null)
                        task.Notes = new List<TaskNote>();

                    // Simple approach: treat entire text as one note
                    if (task.Notes.Count == 0)
                    {
                        task.Notes.Add(new TaskNote { Content = notesBox.Text.Trim() });
                    }
                    else
                    {
                        // Update the first note
                        task.Notes[0].Content = notesBox.Text.Trim();
                    }
                }

                task.UpdatedAt = DateTime.Now;

                taskService.UpdateTask(task);
                logger?.Info("NotesEditorOverlay", $"Updated notes for task: {task.Title}");

                NotesSaved?.Invoke(task);
            }
            catch (Exception ex)
            {
                logger?.Error("NotesEditorOverlay", $"Failed to save notes: {ex.Message}");
                statusText.Text = $"Error: {ex.Message}";
                statusText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }
    }
}
