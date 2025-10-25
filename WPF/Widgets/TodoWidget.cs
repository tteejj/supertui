using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Events;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Todo list widget with full CRUD operations.
    /// Supports add, edit, delete, and toggle completion.
    /// Space bar toggles completion status.
    /// </summary>
    public class TodoWidget : WidgetBase, IThemeable
    {
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private StandardWidgetFrame frame;
        private EditableListControl<TodoItem> todoList;
        private string dataFile;

        public TodoWidget(ILogger logger, IThemeManager themeManager) : this(logger, themeManager, null)
        {
        }

        public TodoWidget(ILogger logger, IThemeManager themeManager, string dataFilePath)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? ThemeManager.Instance;
            WidgetName = "Todo List";
            dataFile = dataFilePath ?? Path.Combine(
                SuperTUI.Extensions.PortableDataDirectory.GetSuperTUIDataDirectory(), "todos.json");
        }

        // Backward compatibility constructors
        public TodoWidget() : this(Logger.Instance, ThemeManager.Instance, null)
        {
        }

        public TodoWidget(ILogger logger) : this(logger, ThemeManager.Instance, null)
        {
        }

        public TodoWidget(string dataFilePath) : this(Logger.Instance, ThemeManager.Instance, dataFilePath)
        {
        }

        public override void Initialize()
        {
            // Ensure data directory exists
            var directory = Path.GetDirectoryName(dataFile);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // Create the editable list control
            todoList = new EditableListControl<TodoItem>
            {
                Title = "TODO LIST",
                AddButtonText = "Add Task",
                EditButtonText = "Edit",
                DeleteButtonText = "Delete",
                PlaceholderText = "Enter new task...",

                // Item creator: text → TodoItem
                ItemCreator = text => new TodoItem
                {
                    Id = Guid.NewGuid(),
                    Text = text,
                    IsCompleted = false,
                    CreatedAt = DateTime.Now
                },

                // Item updater: (old item, new text) → updated TodoItem
                ItemUpdater = (item, text) => new TodoItem
                {
                    Id = item.Id,
                    Text = text,
                    IsCompleted = item.IsCompleted,
                    CreatedAt = item.CreatedAt
                },

                // Display formatter: TodoItem → display string
                DisplayFormatter = FormatTodoItem,

                // Validator: ensure text is not empty
                ItemValidator = item => !string.IsNullOrWhiteSpace(item.Text),

                // Event callbacks
                OnItemAdded = item =>
                {
                    logger.Info("Todo", $"Added task: {item.Text}");
                    SaveTodos();
                    PublishTaskEvent();
                },

                OnItemDeleted = item =>
                {
                    logger.Info("Todo", $"Deleted task: {item.Text}");
                    SaveTodos();
                    PublishTaskEvent();
                },

                OnItemUpdated = (oldItem, newItem) =>
                {
                    logger.Info("Todo", $"Updated task: {oldItem.Text} → {newItem.Text}");
                    SaveTodos();
                    PublishTaskEvent();
                }
            };

            // Add custom keyboard handler for Space bar (toggle completion)
            todoList.KeyDown += TodoList_KeyDown;

            // Load existing todos
            LoadTodos();

            // Wrap in standard frame
            frame = new StandardWidgetFrame(themeManager)
            {
                Title = "TODO LIST"
            };
            frame.SetStandardShortcuts("Enter: Add/Edit", "Del: Delete", "Space: Toggle Complete", "?: Help");
            frame.Content = todoList;

            Content = frame;
        }

        private void TodoList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && todoList.SelectedItem != null)
            {
                ToggleCompletion(todoList.SelectedItem);
                e.Handled = true;
            }
        }

        private string FormatTodoItem(TodoItem item)
        {
            var checkbox = item.IsCompleted ? "[✓]" : "[ ]";
            var strikethrough = item.IsCompleted ? "" : ""; // Could add strikethrough styling
            return $"{checkbox} {item.Text}";
        }

        private void ToggleCompletion(TodoItem item)
        {
            // Create updated item with toggled completion
            var updatedItem = new TodoItem
            {
                Id = item.Id,
                Text = item.Text,
                IsCompleted = !item.IsCompleted,
                CreatedAt = item.CreatedAt
            };

            // Replace in list
            var items = todoList.GetAllItems();
            var index = items.FindIndex(i => i.Id == item.Id);
            if (index >= 0)
            {
                items[index] = updatedItem;
                todoList.LoadItems(items);
                todoList.SelectIndex(index);
                SaveTodos();
                PublishTaskEvent();

                logger.Info("Todo",
                    updatedItem.IsCompleted
                        ? $"Completed: {updatedItem.Text}"
                        : $"Uncompleted: {updatedItem.Text}");
            }
        }

        private void LoadTodos()
        {
            try
            {
                if (File.Exists(dataFile))
                {
                    var json = File.ReadAllText(dataFile);
                    var todos = JsonSerializer.Deserialize<List<TodoItem>>(json);
                    if (todos != null && todos.Any())
                    {
                        todoList.LoadItems(todos);
                        logger.Info("Todo", $"Loaded {todos.Count} tasks from {dataFile}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Todo", $"Failed to load todos: {ex.Message}");
            }
        }

        private void SaveTodos()
        {
            try
            {
                var todos = todoList.GetAllItems();
                var json = JsonSerializer.Serialize(todos, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(dataFile, json);
            }
            catch (Exception ex)
            {
                logger.Error("Todo", $"Failed to save todos: {ex.Message}");
            }
        }

        private void PublishTaskEvent()
        {
            var items = todoList.GetAllItems();
            var completedCount = items.Count(i => i.IsCompleted);
            var pendingCount = items.Count - completedCount;

            SuperTUI.Core.EventBus.Instance.Publish(new TaskStatusChangedEvent
            {
                TotalTasks = items.Count,
                CompletedTasks = completedCount,
                PendingTasks = pendingCount,
                Timestamp = DateTime.Now
            });
        }

        public override Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["DataFile"] = dataFile,
                ["ItemCount"] = todoList.ItemCount
            };
        }

        public override void RestoreState(Dictionary<string, object> state)
        {
            // State is persisted to file, just reload
            LoadTodos();
        }

        protected override void OnDispose()
        {
            SaveTodos();
            todoList.KeyDown -= TodoList_KeyDown;
            todoList = null;
            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            // Apply theme to frame
            if (frame != null)
            {
                frame.ApplyTheme();
            }

            // EditableListControl handles its own theme updates
            if (todoList != null)
            {
                todoList.ApplyTheme();
            }
        }
    }

    /// <summary>
    /// Represents a single todo item
    /// </summary>
    public class TodoItem
    {
        public Guid Id { get; set; }
        public string Text { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }

        public override string ToString() => Text;
    }
}
