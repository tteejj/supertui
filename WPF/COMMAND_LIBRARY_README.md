# Command Library - Implementation Summary

## Overview

A clipboard manager for command snippets. Store text strings, search/filter them with advanced syntax, and copy to clipboard for pasting elsewhere.

**Key Point:** SuperTUI does NOT execute these commands. They are just text strings to be copied to clipboard and pasted into other applications (terminal, IDE, browser, etc.).

---

## What Was Created

### 1. Core/Commands/Command.cs (118 lines)
**Data model** for storing command snippets with metadata.

**Properties:**
- `Id` - Unique identifier (GUID)
- `Title` - Short display name
- `Description` - Longer description of what the command does
- `Tags` - Array of tags for categorization (e.g., `["docker", "admin"]`)
- `CommandText` - The actual command string (REQUIRED field)
- `Created`, `LastUsed`, `UseCount` - Usage statistics

**Key Methods:**
- `RecordUsage()` - Updates LastUsed timestamp and increments UseCount
- `GetDisplayText()` - Format for list display (includes tags)
- `GetSearchableText()` - All fields concatenated for searching
- `GetDetailText()` - Full details for detail panel
- `IsValid` - Validates that CommandText is not empty

---

### 2. Core/Commands/CommandService.cs (372 lines)
**Service layer** for CRUD operations, JSON persistence, and advanced search.

**Core Features:**
- **JSON Storage:** `~/.supertui/commands.json`
- **CRUD Operations:** Add, Update, Delete, Get commands
- **Clipboard Integration:** `CopyToClipboard(id)` - copies and tracks usage
- **Advanced Search:** Substring matching with tag syntax
- **Logger Integration:** Uses existing SuperTUI Logger for diagnostics

**Search Syntax:**
| Syntax | Example | Description |
|--------|---------|-------------|
| Simple | `docker` | Substring match in any field |
| Tag | `t:docker` | Match commands with "docker" tag |
| Multiple Tags | `t:docker,admin` | Match docker OR admin |
| AND | `+docker +restart` | Must contain BOTH terms |
| OR | `docker|podman` | Contains EITHER term |

**Example:**
```csharp
var service = new CommandService(logger);

// Add command
var cmd = new Command {
    Title = "Docker restart all",
    Description = "Restart all running containers",
    Tags = new[] { "docker", "admin" },
    CommandText = "docker restart $(docker ps -q)"
};
service.AddCommand(cmd);

// Search
var results = service.SearchCommands("t:docker");  // All docker commands
var results = service.SearchCommands("+docker +restart");  // AND search

// Copy to clipboard
service.CopyToClipboard(cmd.Id);  // Copies and tracks usage
```

---

## Integration with Existing Infrastructure

✅ **Logger Integration**
```csharp
public CommandService(Logger logger = null)
{
    _logger = logger;
    // Uses logger for Info, Debug, Warning, Error
}
```

✅ **Path Handling**
```csharp
// Stores in ~/.supertui/commands.json
var appDataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".supertui"
);
```

✅ **Error Handling**
- All file operations wrapped in try/catch
- Logs errors via existing Logger
- Falls back to temp directory if ~/.supertui can't be created

---

## JSON Format

```json
[
  {
    "Id": "a1b2c3d4-...",
    "Title": "Docker restart all",
    "Description": "Restart all running containers",
    "Tags": ["docker", "admin"],
    "CommandText": "docker restart $(docker ps -q)",
    "Created": "2025-10-15T10:30:00Z",
    "LastUsed": "2025-10-20T14:30:00Z",
    "UseCount": 12
  },
  {
    "Id": "e5f6g7h8-...",
    "Title": "Git status",
    "Description": "Show working tree status",
    "Tags": ["git"],
    "CommandText": "git status",
    "Created": "2025-10-10T09:00:00Z",
    "LastUsed": "2025-10-20T15:45:00Z",
    "UseCount": 5
  }
]
```

---

## Next Steps: Building the Widget

When you're ready to build the UI widget, here's what it will need:

### CommandLibraryWidget.cs
**UI Components:**
1. **Search TextBox** - Live filtering with advanced syntax
2. **ListBox** - Shows filtered commands with tags
3. **Detail Panel** - Shows full command text, description, stats
4. **Footer** - Help text with keyboard shortcuts

**Keyboard Shortcuts** (consistent with other widgets):
- **Enter** - Copy selected command to clipboard (with toast notification)
- **a** - Add new command (show dialog)
- **e** - Edit selected command (show dialog)
- **d** - Delete selected command (no confirmation, automatic)
- **Esc** - Clear search box, focus search
- **Up/Down** - Navigate list

**Theme Integration:**
```csharp
protected override void OnInitialize()
{
    base.OnInitialize();

    // Get ThemeManager from service container
    var theme = ServiceContainer?.GetService<ThemeManager>();

    // Apply themed colors
    searchBox.Background = new SolidColorBrush(theme.GetBgColor("input.background"));
    searchBox.Foreground = new SolidColorBrush(theme.GetColor("color.primary"));
    // ... etc
}
```

**Empty State:**
When library is empty, show message: `"Press 'a' to add your first command"`

**Toast Notification:**
When copying to clipboard: `"Copied to clipboard"`

---

## Terminal Aesthetic Reference

From the old TUI implementation (`praxis-main/Components/CommandPalette.ps1`), the UI should look like:

```
┌─ Command Library ────────────────────────────────────────┐
│ Search: dock█                                             │
├──────────────────────────────────────────────────────────┤
│ Docker restart container          t:docker,admin         │
│ Docker list all containers        t:docker               │
│ Docker compose up -d              t:docker,compose       │
│                                                           │
├──────────────────────────────────────────────────────────┤
│ Command: docker restart $(docker ps -q)                  │
│ Description: Restarts all running containers             │
│ Tags: docker, admin                                      │
│ Used: 12 times, last: 2025-10-20 14:30                  │
├──────────────────────────────────────────────────────────┤
│ [Enter] Copy  [a] Add  [e] Edit  [d] Delete  [Esc] Clear │
└──────────────────────────────────────────────────────────┘
```

**In WPF:**
- Use `Grid` with `RowDefinitions` for layout
- `TextBox` for search (monospace font)
- `ListBox` with `ItemTemplate` for commands
- `TextBlock` for detail panel (multiline, wrap)
- All controls styled with ThemeManager colors
- Borders using `Border` control with themed `BorderBrush`

---

## Testing

Cannot test on Linux (WPF is Windows-only), but the code should:
1. Compile cleanly in the SuperTUI build
2. Integrate with existing Logger infrastructure
3. Create JSON file on first run
4. Support all search syntax patterns

---

## Files Created

```
WPF/
├── Core/
│   └── Commands/
│       ├── Command.cs              (118 lines) ✅
│       └── CommandService.cs       (372 lines) ✅
└── Test_CommandService.ps1         (Test script - Windows only)
```

**Total: 490 lines of C# code**

---

## Usage Example (Future Widget)

```powershell
# In SuperTUI.ps1 or demo script
$logger = [SuperTUI.Core.Infrastructure.Logger]::new()
$commandService = [SuperTUI.Core.Commands.CommandService]::new($logger)

# Create widget
$commandWidget = [SuperTUI.Widgets.CommandLibraryWidget]::new()
$commandWidget.Service = $commandService

# Add to workspace
$workspace.AddWidget($commandWidget)
```

---

## Design Decisions Made

1. ✅ **No Groups** - Only tags (simpler, more flexible)
2. ✅ **Substring Matching** - Not fuzzy (simpler, faster)
3. ✅ **Single Line Commands** - No multi-line support (simpler)
4. ✅ **Automatic Delete** - No confirmation dialog (consistent with other widgets)
5. ✅ **Toast Notifications** - Show "Copied to clipboard" feedback
6. ✅ **Empty State Message** - Guide users to press 'a' to add first command
7. ✅ **Theme Integration** - Use ThemeManager, no hardcoded colors
8. ✅ **Logger Integration** - Use existing infrastructure
9. ✅ **JSON Persistence** - Standard format, no export/import needed

---

## Notes

- This is **Option A** from your request: Data layer only (Command.cs + CommandService.cs)
- Widget UI will be implemented later when infrastructure is ready
- Code follows existing SuperTUI patterns (Logger integration, error handling, etc.)
- Search implementation ported from terminal TUI `praxis-main/Services/CommandService.ps1`
- Ready to be integrated into the SuperTUI build system
