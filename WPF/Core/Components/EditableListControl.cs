using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Components
{
    /// <summary>
    /// Reusable control for displaying and editing a list of items with CRUD operations.
    /// Supports Add, Edit, Delete, and navigation. Fully keyboard-driven.
    /// </summary>
    /// <typeparam name="T">The type of items in the list</typeparam>
    public class EditableListControl<T> : UserControl where T : class
    {
        private ObservableCollection<T> items;
        private ListBox listBox;
        private TextBox inputBox;
        private TextBlock statusLabel;
        private Button addButton;
        private Button deleteButton;
        private Button editButton;

        private T selectedItem;
        private bool isEditMode = false;

        private Theme theme;

        // Configuration
        public Func<T, string> DisplayFormatter { get; set; }
        public Func<string, T> ItemCreator { get; set; }
        public Func<T, string, T> ItemUpdater { get; set; }
        public Func<T, bool> ItemValidator { get; set; }
        public Action<T> OnItemAdded { get; set; }
        public Action<T> OnItemDeleted { get; set; }
        public Action<T, T> OnItemUpdated { get; set; } // (oldItem, newItem)
        public Action<T> OnSelectionChanged { get; set; }

        // Properties
        public IEnumerable<T> Items => items;
        public T SelectedItem => selectedItem;
        public int ItemCount => items.Count;
        public bool IsEmpty => items.Count == 0;

        // Styling properties
        public string Title { get; set; } = "ITEMS";
        public string AddButtonText { get; set; } = "Add";
        public string EditButtonText { get; set; } = "Edit";
        public string DeleteButtonText { get; set; } = "Delete";
        public string PlaceholderText { get; set; } = "Enter text...";
        public bool ShowButtons { get; set; } = true;
        public bool AllowEdit { get; set; } = true;
        public bool AllowDelete { get; set; } = true;
        public bool ShowStatus { get; set; } = true;

        public EditableListControl()
        {
            items = new ObservableCollection<T>();
            theme = ThemeManager.Instance.CurrentTheme;

            // Default implementations
            DisplayFormatter = item => item?.ToString() ?? "(null)";
            ItemCreator = text => throw new NotImplementedException("ItemCreator must be set");
            ItemUpdater = (item, text) => throw new NotImplementedException("ItemUpdater must be set");
            ItemValidator = item => item != null;

            BuildUI();
            SetupEventHandlers();
        }

        private void BuildUI()
        {
            var mainPanel = new DockPanel
            {
                LastChildFill = true,
                Background = new SolidColorBrush(theme.Background)
            };

            // Title
            var titleBlock = new TextBlock
            {
                Text = Title,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(titleBlock, Dock.Top);
            mainPanel.Children.Add(titleBlock);

            // Input panel at top
            var inputPanel = new DockPanel
            {
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(inputPanel, Dock.Top);

            inputBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                Height = 28
            };
            inputPanel.Children.Add(inputBox);
            mainPanel.Children.Add(inputPanel);

            // Buttons panel
            if (ShowButtons)
            {
                var buttonsPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                DockPanel.SetDock(buttonsPanel, Dock.Top);

                addButton = CreateButton(AddButtonText, theme.Success);
                buttonsPanel.Children.Add(addButton);

                if (AllowEdit)
                {
                    editButton = CreateButton(EditButtonText, theme.Info);
                    editButton.IsEnabled = false;
                    editButton.Margin = new Thickness(5, 0, 0, 0);
                    buttonsPanel.Children.Add(editButton);
                }

                if (AllowDelete)
                {
                    deleteButton = CreateButton(DeleteButtonText, theme.Error);
                    deleteButton.IsEnabled = false;
                    deleteButton.Margin = new Thickness(5, 0, 0, 0);
                    buttonsPanel.Children.Add(deleteButton);
                }

                mainPanel.Children.Add(buttonsPanel);
            }

            // List box
            listBox = new ListBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                ItemsSource = items,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            // Custom item template
            var itemTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("."));
            factory.SetValue(TextBlock.MarginProperty, new Thickness(5));
            itemTemplate.VisualTree = factory;
            listBox.ItemTemplate = itemTemplate;

            mainPanel.Children.Add(listBox);

            // Status bar
            if (ShowStatus)
            {
                statusLabel = new TextBlock
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                    Margin = new Thickness(0, 5, 0, 0),
                    Text = "0 items"
                };
                DockPanel.SetDock(statusLabel, Dock.Bottom);
                mainPanel.Children.Add(statusLabel);
            }

            Content = mainPanel;
        }

        private Button CreateButton(string text, Color color)
        {
            return new Button
            {
                Content = text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Background = new SolidColorBrush(color),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5, 10, 5),
                Cursor = Cursors.Hand
            };
        }

        private void SetupEventHandlers()
        {
            // Input box key handling
            inputBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    if (isEditMode)
                        CommitEdit();
                    else
                        AddItem();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelEdit();
                    e.Handled = true;
                }
            };

            // List box selection
            listBox.SelectionChanged += (s, e) =>
            {
                if (listBox.SelectedItem is T item)
                {
                    selectedItem = item;
                    if (editButton != null) editButton.IsEnabled = true;
                    if (deleteButton != null) deleteButton.IsEnabled = true;
                    OnSelectionChanged?.Invoke(item);
                }
                else
                {
                    selectedItem = null;
                    if (editButton != null) editButton.IsEnabled = false;
                    if (deleteButton != null) deleteButton.IsEnabled = false;
                }
            };

            // List box keyboard shortcuts
            listBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Delete && AllowDelete && selectedItem != null)
                {
                    DeleteSelectedItem();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && AllowEdit && selectedItem != null)
                {
                    StartEdit();
                    e.Handled = true;
                }
            };

            // Button clicks
            if (ShowButtons)
            {
                addButton.Click += (s, e) => AddItem();
                if (editButton != null) editButton.Click += (s, e) => StartEdit();
                if (deleteButton != null) deleteButton.Click += (s, e) => DeleteSelectedItem();
            }
        }

        // Public API methods

        public void AddItem()
        {
            try
            {
                var text = inputBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return;

                var newItem = ItemCreator(text);
                if (newItem == null || !ItemValidator(newItem))
                {
                    UpdateStatus("Invalid item", theme.Error);
                    return;
                }

                items.Add(newItem);
                inputBox.Clear();
                UpdateStatus($"Added: {DisplayFormatter(newItem)}", theme.Success);
                OnItemAdded?.Invoke(newItem);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("EditableList", $"Failed to add item: {ex.Message}");
                UpdateStatus("Error adding item", theme.Error);
            }
        }

        public void StartEdit()
        {
            if (selectedItem == null) return;

            isEditMode = true;
            inputBox.Text = DisplayFormatter(selectedItem);
            inputBox.SelectAll();
            inputBox.Focus();
            UpdateStatus("Editing... (Enter to save, Esc to cancel)", theme.Info);
        }

        public void CommitEdit()
        {
            if (!isEditMode || selectedItem == null) return;

            try
            {
                var text = inputBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    CancelEdit();
                    return;
                }

                var oldItem = selectedItem;
                var updatedItem = ItemUpdater(oldItem, text);

                if (updatedItem == null || !ItemValidator(updatedItem))
                {
                    UpdateStatus("Invalid item", theme.Error);
                    return;
                }

                var index = items.IndexOf(oldItem);
                if (index >= 0)
                {
                    items[index] = updatedItem;
                    listBox.SelectedIndex = index;
                }

                inputBox.Clear();
                isEditMode = false;
                UpdateStatus($"Updated: {DisplayFormatter(updatedItem)}", theme.Success);
                OnItemUpdated?.Invoke(oldItem, updatedItem);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("EditableList", $"Failed to update item: {ex.Message}");
                UpdateStatus("Error updating item", theme.Error);
            }
        }

        public void CancelEdit()
        {
            isEditMode = false;
            inputBox.Clear();
            UpdateStatus($"{items.Count} items", theme.ForegroundSecondary);
        }

        public void DeleteSelectedItem()
        {
            if (selectedItem == null) return;

            var itemToDelete = selectedItem;
            items.Remove(itemToDelete);
            UpdateStatus($"Deleted: {DisplayFormatter(itemToDelete)}", theme.Warning);
            OnItemDeleted?.Invoke(itemToDelete);
        }

        public void Clear()
        {
            items.Clear();
            UpdateStatus("List cleared", theme.Info);
        }

        public void LoadItems(IEnumerable<T> itemsToLoad)
        {
            items.Clear();
            foreach (var item in itemsToLoad)
            {
                items.Add(item);
            }
            UpdateStatus($"Loaded {items.Count} items", theme.Success);
        }

        public void SelectItem(T item)
        {
            var index = items.IndexOf(item);
            if (index >= 0)
            {
                listBox.SelectedIndex = index;
                listBox.ScrollIntoView(item);
            }
        }

        public void SelectIndex(int index)
        {
            if (index >= 0 && index < items.Count)
            {
                listBox.SelectedIndex = index;
                listBox.ScrollIntoView(items[index]);
            }
        }

        private void UpdateStatus(string message, Color? color = null)
        {
            if (!ShowStatus || statusLabel == null) return;

            statusLabel.Text = message;
            statusLabel.Foreground = new SolidColorBrush(color ?? theme.ForegroundSecondary);
        }

        // Helper methods

        public T GetItemAt(int index)
        {
            return (index >= 0 && index < items.Count) ? items[index] : null;
        }

        public int IndexOf(T item)
        {
            return items.IndexOf(item);
        }

        public bool Contains(T item)
        {
            return items.Contains(item);
        }

        public List<T> GetAllItems()
        {
            return items.ToList();
        }

        public void RefreshDisplay()
        {
            listBox.Items.Refresh();
            UpdateStatus($"{items.Count} items", theme.ForegroundSecondary);
        }
    }
}
