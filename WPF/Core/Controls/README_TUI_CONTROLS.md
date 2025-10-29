# TUI-Styled WPF Controls

## Overview

This is a collection of **WPF controls styled to look like terminal/TUI applications** WITHOUT the limitations of actual terminal UIs. You get the aesthetic of terminal applications (box-drawing characters, monospaced fonts, clean layouts) with full WPF functionality (mouse support, smooth scrolling, animations, rich interaction).

## Philosophy

**✅ TUI Aesthetics:**
- Box-drawing characters (┌─┐│└┘├┤┬┴┼)
- Monospaced fonts (Consolas, Courier New)
- Terminal color schemes
- Clean, minimal design
- Character-based visual language

**❌ NOT Terminal Limitations:**
- Not limited to 80x24 grid
- Full mouse support
- Real scrolling (not line-by-line)
- Full WPF features (gradients, opacity, animations)
- Rich data binding
- Flexible layouts

## Available Controls

### 1. TUIBox
Container with box-drawing character borders.

```csharp
var box = new TUIBox
{
    Title = "MY TASKS",
    BorderStyle = TUIBorderStyle.Single,  // Single, Double, Rounded, Bold
    ShowTitle = true,
    Content = myContent
};
```

**Border Styles:**
- `Single`: `┌─┐│└┘` - Clean single-line borders
- `Double`: `╔═╗║╚╝` - Bold double-line borders
- `Rounded`: `╭─╮│╰╯` - Rounded corners
- `Bold`: `┏━┓┃┗┛` - Thick/heavy borders

### 2. TUITextInput
Text input styled like terminal input.

```csharp
var input = new TUITextInput
{
    Text = "task title",
    Placeholder = "Enter task...",
    Prefix = "[ ",  // Customizable prefix
    Suffix = " ]"   // Customizable suffix
};
```

**Looks like:** `[ input text here          ]`

### 3. TUIComboBox
Dropdown/combo box styled for terminal aesthetic.

```csharp
var combo = new TUIComboBox
{
    Label = "Status",
    ItemsSource = new List<string> { "Todo", "In Progress", "Done" },
    SelectedIndex = 0
};
```

**Looks like:** `Status: [ Todo ▼ ]`

### 4. TUIListBox
List box with terminal-style item rendering.

```csharp
var list = new TUIListBox
{
    ItemsSource = tasks,
    SelectedIndex = 0,
    ShowCheckboxes = true
};
```

**Features:**
- Terminal-style selection highlighting
- Keyboard navigation (arrows, page up/down)
- Mouse support
- Full scrolling

### 5. TUIStatusBar
Status/command bar for showing keyboard hints.

```csharp
var statusBar = new TUIStatusBar
{
    Commands = new List<TUICommand>
    {
        new TUICommand("Enter", "Create"),
        new TUICommand("Esc", "Cancel"),
        new TUICommand("Tab", "Next Field")
    },
    StatusText = "Creating new task..."
};
```

**Looks like:** `[Enter]Create [Esc]Cancel [Tab]Next Field          Creating new task...`

## Example Usage

See `/home/teej/supertui/WPF/Widgets/TUIDemoWidget.cs` for a complete demo showing all controls in action.

### Basic Layout Example

```csharp
// Main container
var mainGrid = new Grid();
mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status bar

// Content area with TUI box
var taskBox = new TUIBox
{
    Title = "TASKS",
    BorderStyle = TUIBorderStyle.Single
};

var taskList = new TUIListBox
{
    ItemsSource = new List<string> { "Task 1", "Task 2", "Task 3" }
};
taskBox.Content = taskList;

Grid.SetRow(taskBox, 0);
mainGrid.Children.Add(taskBox);

// Status bar
var statusBar = new TUIStatusBar
{
    Commands = new List<TUICommand>
    {
        new TUICommand("Ctrl+N", "New"),
        new TUICommand("Enter", "Edit")
    }
};
Grid.SetRow(statusBar, 1);
mainGrid.Children.Add(statusBar);
```

## Theme Integration

All TUI controls automatically integrate with the existing `ThemeManager`:

```csharp
// Controls use these theme colors:
theme.Foreground      // Main text color
theme.ForegroundSecondary  // Dim text (prefixes, suffixes)
theme.Background      // Background color
theme.Surface         // Input/list backgrounds
theme.Border          // Border color
theme.Primary         // Accents (labels, keys)
theme.Selection       // Selection highlight
```

Controls automatically update when theme changes - no manual refresh needed.

## Creating New TUI-Styled Controls

Follow this pattern:

1. **Extend Control (not UserControl)** for better performance
2. **Use box-drawing characters** for borders/decorations
3. **Use monospaced font:** `new FontFamily("Consolas, Courier New, monospace")`
4. **Subscribe to theme changes:**
   ```csharp
   ThemeChangedWeakEventManager.AddHandler(ThemeManager.Instance, OnThemeChanged);
   ```
5. **Keep WPF functionality** - don't artificially limit yourself

## Key Differences from Real TUIs

| Feature | Real TUI | These Controls |
|---------|----------|----------------|
| Grid | Fixed (80x24, etc.) | Flexible, responsive |
| Scrolling | Line-by-line | Smooth pixel scrolling |
| Mouse | Limited/none | Full support |
| Colors | 16 ANSI colors | Full RGB |
| Animation | None | Full WPF animations |
| Layout | Character grid | WPF layout engine |
| Input | Character-based | Rich WPF input |

## Inspiration

These controls are inspired by:
- **btop** - System monitor TUI
- **TelegramTUI** - Telegram terminal client
- **lazygit** - Git TUI
- Classic terminal applications

But without their limitations!

## File Locations

- `/home/teej/supertui/WPF/Core/Controls/TUIBox.cs`
- `/home/teej/supertui/WPF/Core/Controls/TUITextInput.cs`
- `/home/teej/supertui/WPF/Core/Controls/TUIComboBox.cs`
- `/home/teej/supertui/WPF/Core/Controls/TUIListBox.cs`
- `/home/teej/supertui/WPF/Core/Controls/TUIStatusBar.cs`
- `/home/teej/supertui/WPF/Widgets/TUIDemoWidget.cs` - Demo widget

## Next Steps

1. Run the demo widget to see the controls in action
2. Replace standard WPF controls in existing widgets with TUI controls
3. Create more specialized TUI controls as needed (TUITable, TUIProgress, etc.)
4. Add animations and transitions while maintaining TUI aesthetic

The goal is **terminal aesthetics with modern UI power**!
