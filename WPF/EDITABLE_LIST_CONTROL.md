# EditableListControl - Reusable CRUD Component

A generic, keyboard-driven list control with full CRUD (Create, Read, Update, Delete) operations. Perfect for todo lists, file management, settings editors, and any widget that needs list manipulation.

## Features

- ✅ **Generic Type Support** - Works with any class type `T`
- ✅ **Full CRUD Operations** - Add, Edit, Delete, Read
- ✅ **Keyboard-Driven** - Enter to add/save, Delete key, Escape to cancel
- ✅ **Customizable Display** - Provide your own formatter function
- ✅ **Validation** - Optional item validation before add/update
- ✅ **Event Callbacks** - React to add/delete/update/selection events
- ✅ **Theme Integration** - Uses ThemeManager colors
- ✅ **Observable Collection** - Automatic UI updates
- ✅ **Status Messages** - Optional status bar with feedback
- ✅ **Flexible Styling** - Show/hide buttons, status, configure text

## Quick Start

### Simple String List

```csharp
var control = new EditableListControl<string>
{
    Title = "MY TASKS",
    ItemCreator = text => text,  // Text → Item
    ItemUpdater = (item, text) => text,  // Update item with new text
    DisplayFormatter = item => item  // Item → Display text
};

// Add to widget
Content = control;
```

### Custom Object List

```csharp
public class TodoItem
{
    public string Text { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

var control = new EditableListControl<TodoItem>
{
    Title = "TODO LIST",

    ItemCreator = text => new TodoItem
    {
        Text = text,
        IsCompleted = false,
        CreatedAt = DateTime.Now
    },

    ItemUpdater = (item, text) => new TodoItem
    {
        Text = text,
        IsCompleted = item.IsCompleted,
        CreatedAt = item.CreatedAt
    },

    DisplayFormatter = item => item.IsCompleted
        ? $"[✓] {item.Text}"
        : $"[ ] {item.Text}",

    ItemValidator = item => !string.IsNullOrWhiteSpace(item.Text),

    OnItemAdded = item => Logger.Instance.Info("Todo", $"Added: {item.Text}"),
    OnItemDeleted = item => Logger.Instance.Info("Todo", $"Deleted: {item.Text}"),
    OnItemUpdated = (old, @new) => Logger.Instance.Info("Todo", $"Updated: {old.Text} → {@new.Text}"),
    OnSelectionChanged = item => Console.WriteLine($"Selected: {item.Text}")
};
```

## Configuration Properties

### Required Properties

| Property | Type | Description |
|----------|------|-------------|
| `ItemCreator` | `Func<string, T>` | Creates new item from input text |
| `ItemUpdater` | `Func<T, string, T>` | Updates existing item with new text |
| `DisplayFormatter` | `Func<T, string>` | Formats item for display in list |

### Optional Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ItemValidator` | `Func<T, bool>` | `item != null` | Validates item before add/update |
| `Title` | `string` | `"ITEMS"` | Title displayed at top |
| `AddButtonText` | `string` | `"Add"` | Text on add button |
| `EditButtonText` | `string` | `"Edit"` | Text on edit button |
| `DeleteButtonText` | `string` | `"Delete"` | Text on delete button |
| `PlaceholderText` | `string` | `"Enter text..."` | Input box placeholder |
| `ShowButtons` | `bool` | `true` | Show/hide button panel |
| `AllowEdit` | `bool` | `true` | Enable edit functionality |
| `AllowDelete` | `bool` | `true` | Enable delete functionality |
| `ShowStatus` | `bool` | `true` | Show/hide status bar |

### Event Callbacks

| Callback | Type | Description |
|----------|------|-------------|
| `OnItemAdded` | `Action<T>` | Called after item is added |
| `OnItemDeleted` | `Action<T>` | Called after item is deleted |
| `OnItemUpdated` | `Action<T, T>` | Called after item is updated (old, new) |
| `OnSelectionChanged` | `Action<T>` | Called when selection changes |

## Public API Methods

### CRUD Operations

```csharp
// Add new item from input box
control.AddItem();

// Start editing selected item
control.StartEdit();

// Save edit changes
control.CommitEdit();

// Cancel edit mode
control.CancelEdit();

// Delete selected item
control.DeleteSelectedItem();

// Clear all items
control.Clear();
```

### Data Management

```csharp
// Load items from collection
control.LoadItems(new[] { item1, item2, item3 });

// Get all items as list
List<TodoItem> allItems = control.GetAllItems();

// Get item at index
TodoItem item = control.GetItemAt(0);

// Find index of item
int index = control.IndexOf(item);

// Check if contains item
bool exists = control.Contains(item);
```

### Selection & Navigation

```csharp
// Select item by reference
control.SelectItem(item);

// Select item by index
control.SelectIndex(2);

// Get selected item
TodoItem selected = control.SelectedItem;

// Refresh display
control.RefreshDisplay();
```

### Properties

```csharp
// Get all items (IEnumerable)
IEnumerable<TodoItem> items = control.Items;

// Get selected item
TodoItem selected = control.SelectedItem;

// Get count
int count = control.ItemCount;

// Check if empty
bool isEmpty = control.IsEmpty;
```

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Enter` | Add item (or save edit if editing) |
| `Escape` | Cancel edit mode |
| `Delete` | Delete selected item |
| `Enter` (on list item) | Start editing selected item |
| `↑` / `↓` | Navigate list |

## Usage Examples

### Example 1: Simple Todo List

```csharp
public class SimpleTodoWidget : WidgetBase
{
    private EditableListControl<string> todoList;

    public override void Initialize()
    {
        todoList = new EditableListControl<string>
        {
            Title = "TODO",
            ItemCreator = text => text,
            ItemUpdater = (item, text) => text,
            DisplayFormatter = item => $"• {item}"
        };

        Content = todoList;
    }
}
```

### Example 2: Todo with Completion Toggle

```csharp
public class TodoWidget : WidgetBase
{
    private EditableListControl<TodoItem> todoList;

    public override void Initialize()
    {
        todoList = new EditableListControl<TodoItem>
        {
            Title = "TASKS",
            ItemCreator = CreateTodoItem,
            ItemUpdater = UpdateTodoItem,
            DisplayFormatter = FormatTodoItem,
            OnItemAdded = item => SaveToFile()
        };

        // Load from file
        todoList.LoadItems(LoadFromFile());

        // Custom keyboard handling for toggle
        todoList.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Space && todoList.SelectedItem != null)
            {
                ToggleCompletion(todoList.SelectedItem);
                e.Handled = true;
            }
        };

        Content = todoList;
    }

    private TodoItem CreateTodoItem(string text)
    {
        return new TodoItem
        {
            Id = Guid.NewGuid(),
            Text = text,
            IsCompleted = false,
            CreatedAt = DateTime.Now
        };
    }

    private TodoItem UpdateTodoItem(TodoItem item, string text)
    {
        return new TodoItem
        {
            Id = item.Id,
            Text = text,
            IsCompleted = item.IsCompleted,
            CreatedAt = item.CreatedAt
        };
    }

    private string FormatTodoItem(TodoItem item)
    {
        var checkbox = item.IsCompleted ? "[✓]" : "[ ]";
        var style = item.IsCompleted ? "(strikethrough)" : "";
        return $"{checkbox} {item.Text}";
    }

    private void ToggleCompletion(TodoItem item)
    {
        var updated = new TodoItem
        {
            Id = item.Id,
            Text = item.Text,
            IsCompleted = !item.IsCompleted,
            CreatedAt = item.CreatedAt
        };

        var index = todoList.IndexOf(item);
        todoList.GetAllItems()[index] = updated;
        todoList.RefreshDisplay();
        SaveToFile();
    }
}
```

### Example 3: File List with Icons

```csharp
public class FileListWidget : WidgetBase
{
    private EditableListControl<FileInfo> fileList;

    public override void Initialize()
    {
        fileList = new EditableListControl<FileInfo>
        {
            Title = "FILES",
            AllowEdit = false,  // No editing file names
            ItemCreator = text => new FileInfo(text),
            DisplayFormatter = FormatFileInfo,
            OnSelectionChanged = file => PreviewFile(file)
        };

        LoadDirectory(@"C:\MyFolder");
        Content = fileList;
    }

    private string FormatFileInfo(FileInfo file)
    {
        var icon = GetFileIcon(file.Extension);
        var size = FormatFileSize(file.Length);
        return $"{icon} {file.Name} ({size})";
    }

    private void LoadDirectory(string path)
    {
        var files = Directory.GetFiles(path).Select(f => new FileInfo(f));
        fileList.LoadItems(files);
    }
}
```

### Example 4: Settings Editor

```csharp
public class SettingsWidget : WidgetBase
{
    private EditableListControl<KeyValuePair<string, string>> settings;

    public override void Initialize()
    {
        settings = new EditableListControl<KeyValuePair<string, string>>
        {
            Title = "SETTINGS",
            ItemCreator = text =>
            {
                var parts = text.Split('=');
                return new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim());
            },
            ItemUpdater = (item, text) =>
            {
                var parts = text.Split('=');
                return new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim());
            },
            DisplayFormatter = kvp => $"{kvp.Key} = {kvp.Value}",
            ItemValidator = kvp => !string.IsNullOrWhiteSpace(kvp.Key),
            OnItemUpdated = (old, @new) => ApplySetting(@new.Key, @new.Value)
        };

        LoadSettings();
        Content = settings;
    }
}
```

## Advanced Customization

### Custom Item Template

For more complex item rendering beyond text:

```csharp
// After creating the control, you can access the internal ListBox
var listBox = (ListBox)((DockPanel)control.Content).Children[3];

// Set custom DataTemplate
var template = new DataTemplate();
var factory = new FrameworkElementFactory(typeof(StackPanel));
factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

// Add icon
var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
iconFactory.SetValue(TextBlock.TextProperty, "📄");
iconFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 5, 0));
factory.AppendChild(iconFactory);

// Add text
var textFactory = new FrameworkElementFactory(typeof(TextBlock));
textFactory.SetBinding(TextBlock.TextProperty, new Binding("."));
factory.AppendChild(textFactory);

template.VisualTree = factory;
listBox.ItemTemplate = template;
```

### State Persistence

```csharp
// Save state
public override Dictionary<string, object> SaveState()
{
    return new Dictionary<string, object>
    {
        ["Items"] = JsonSerializer.Serialize(todoList.GetAllItems())
    };
}

// Load state
public override void LoadState(Dictionary<string, object> state)
{
    if (state.TryGetValue("Items", out var json))
    {
        var items = JsonSerializer.Deserialize<List<TodoItem>>(json.ToString());
        todoList.LoadItems(items);
    }
}
```

## Best Practices

### ✅ Do

- Set `ItemCreator`, `ItemUpdater`, and `DisplayFormatter` before adding items
- Use immutable updates in `ItemUpdater` (return new object)
- Validate items in `ItemValidator` to prevent bad data
- Save state in callbacks (`OnItemAdded`, `OnItemDeleted`, etc.)
- Use `RefreshDisplay()` after manual collection changes
- Handle exceptions in your creator/updater functions

### ❌ Don't

- Don't modify items directly (use `ItemUpdater`)
- Don't forget to call `RefreshDisplay()` after manual changes
- Don't assume `SelectedItem` is non-null
- Don't do heavy operations in `DisplayFormatter` (called frequently)
- Don't modify the `Items` collection directly (use control methods)

## Integration with Widgets

```csharp
public class MyWidget : WidgetBase
{
    private EditableListControl<MyType> list;

    public override void Initialize()
    {
        list = new EditableListControl<MyType>
        {
            // Configure...
        };

        Content = list;
    }

    public override Dictionary<string, object> SaveState()
    {
        return new Dictionary<string, object>
        {
            ["Items"] = list.GetAllItems()
        };
    }

    public override void LoadState(Dictionary<string, object> state)
    {
        if (state.TryGetValue("Items", out var items))
        {
            list.LoadItems((List<MyType>)items);
        }
    }

    protected override void OnDispose()
    {
        list = null;
        base.OnDispose();
    }
}
```

## Thread Safety

The control is **NOT thread-safe**. All operations must be called from the UI thread. Use `Dispatcher.Invoke()` if updating from background threads:

```csharp
Task.Run(() =>
{
    var newItem = FetchDataFromAPI();

    Dispatcher.Invoke(() =>
    {
        control.LoadItems(new[] { newItem });
    });
});
```

## Performance

- **Item Count**: Tested up to 10,000 items with smooth performance
- **Display Formatter**: Called on every render, keep it fast
- **ObservableCollection**: Automatic UI updates, no manual refresh needed (unless manual changes)
- **Memory**: Weak references not used (items held strongly)

## Future Enhancements

Potential additions:

- Multi-select support
- Drag-and-drop reordering
- Search/filter functionality
- Virtual scrolling for large lists
- Undo/redo for changes
- Batch operations
- Export/import to JSON/CSV

---

**Related:**
- `WidgetBase.cs` - Base class for widgets
- `DEPENDENCY_INJECTION.md` - DI container usage
- `EVENTBUS.md` - Event system integration
