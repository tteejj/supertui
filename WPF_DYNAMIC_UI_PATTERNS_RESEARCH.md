# WPF Dynamic UI Patterns for SuperTUI
**Research Document - Context-Aware UI Capabilities**
**Date:** 2025-10-27
**Architecture:** WPF Desktop Application with Terminal Aesthetics

---

## Executive Summary

This document explores advanced WPF techniques for implementing dynamic, context-aware UI patterns in SuperTUI. The focus is on patterns that work within SuperTUI's existing architecture: dependency injection, EventBus pub/sub, ObservableCollection data binding, and code-behind UI construction (no XAML).

**Current SuperTUI Architecture:**
- Widgets built programmatically (code-behind, no XAML files)
- Data binding with ObservableCollection<T> (e.g., KanbanBoardWidget, AgendaWidget)
- EventBus for inter-widget communication (pub/sub pattern)
- Master-detail already implemented (TaskManagementWidget: filter list → task list → detail panel)
- Expanders for progressive disclosure (AgendaWidget: collapsible time groups)

---

## 1. MASTER-DETAIL PATTERNS

### 1.1 Current Implementation in SuperTUI

**TaskManagementWidget** (3-pane layout):
```
┌──────────────┬─────────────────┬──────────────┐
│   FILTERS    │   TASK LIST     │   DETAILS    │
│              │                 │              │
│ > All (15)   │ ☐ Task 1        │ Title:       │
│   Today (3)  │ ☐ Task 2        │ Description: │
│   Overdue    │ ☑ Task 3        │ Status:      │
│              │                 │ Tags:        │
└──────────────┴─────────────────┴──────────────┘
```

**EventBus Coordination:**
```csharp
// Selection in one widget updates another widget
EventBus.Subscribe<Core.Events.TaskSelectedEvent>(OnTaskSelectedFromOtherWidget);

private void OnTaskSelectedFromOtherWidget(Core.Events.TaskSelectedEvent evt)
{
    if (evt.SourceWidget == WidgetType) return; // Ignore own events
    SelectTaskById(evt.Task.Id);  // Update own selection
}
```

### 1.2 WPF Techniques for Master-Detail

#### A. CollectionViewSource (Shared CurrentItem)

**Concept:** Multiple controls synchronize selection through a shared ICollectionView.

```csharp
// Create shared view
var tasksView = CollectionViewSource.GetDefaultView(tasksCollection);

// Master ListBox
masterListBox.ItemsSource = tasksView;

// Detail panel binds to CurrentItem
detailPanel.DataContext = tasksView;

// CurrentItem automatically syncs between master and detail
tasksView.CurrentChanged += (s, e) => {
    var current = tasksView.CurrentItem as TaskItem;
    // Update detail panel
};
```

**SuperTUI Application:**
- Use in multi-region layouts where multiple widgets show same data
- Example: Split-pane file explorer (directory tree + file list + preview)
- Example: Project/task view (project list + task list + task details)

**Trade-offs:**
- ✅ Automatic synchronization (no manual event handling)
- ✅ Built-in filtering/sorting/grouping
- ❌ Less explicit than EventBus (harder to debug)
- ❌ Tight coupling (all controls must use same CollectionView)

**Recommendation:** Use EventBus for cross-widget coordination, CollectionViewSource for intra-widget regions.

#### B. Multi-Region Coordination via EventBus (Current Pattern)

**Already implemented in SuperTUI!** This is the correct pattern.

```csharp
// Publish from KanbanWidget
EventBus.Publish(new TaskSelectedEvent { Task = task, SourceWidget = "KanbanBoard" });

// Subscribe in TaskManagementWidget
EventBus.Subscribe<TaskSelectedEvent>(evt => {
    if (evt.SourceWidget != WidgetType) {
        SelectTaskById(evt.Task.Id);
    }
});
```

**Benefits:**
- ✅ Loose coupling (widgets don't reference each other)
- ✅ Flexible (any widget can participate)
- ✅ Testable (mock EventBus in tests)
- ✅ Debuggable (log all events)

**Enhancement Opportunity:** Add event filtering by priority
```csharp
EventBus.Subscribe<TaskSelectedEvent>(OnTaskSelected, priority: SubscriptionPriority.High);
```

---

## 2. CONTEXTUAL PANELS (Show/Hide Based on Selection)

### 2.1 Visibility Bindings with Converters

**Pattern:** Show/hide panels based on selection state.

```csharp
// Converter: null → Collapsed, non-null → Visible
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Usage in code-behind
var converter = new NullToVisibilityConverter();
var binding = new Binding("SelectedTask") { Converter = converter };
detailPanel.SetBinding(UIElement.VisibilityProperty, binding);
```

**SuperTUI Application:**
```csharp
// TaskManagementWidget: Show detail panel only when task selected
private void RefreshDetailPanel()
{
    if (selectedTask == null)
    {
        detailsPanel.Visibility = Visibility.Collapsed;
        emptyStatePanel.Visibility = Visibility.Visible;
    }
    else
    {
        detailsPanel.Visibility = Visibility.Visible;
        emptyStatePanel.Visibility = Visibility.Collapsed;
        // Populate details
    }
}
```

### 2.2 Slide-In/Slide-Out Animations

**WPF DoubleAnimation for smooth panel transitions:**

```csharp
// Slide-in animation (right to left)
private void ShowDetailPanel()
{
    detailPanel.Visibility = Visibility.Visible;
    
    var slideIn = new DoubleAnimation
    {
        From = detailPanel.ActualWidth,  // Start off-screen (right)
        To = 0,                          // End at normal position
        Duration = TimeSpan.FromMilliseconds(250),
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
    };
    
    var transform = new TranslateTransform();
    detailPanel.RenderTransform = transform;
    transform.BeginAnimation(TranslateTransform.XProperty, slideIn);
}

// Slide-out animation (left to right)
private void HideDetailPanel()
{
    var slideOut = new DoubleAnimation
    {
        From = 0,
        To = detailPanel.ActualWidth,
        Duration = TimeSpan.FromMilliseconds(200),
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
    };
    
    var transform = detailPanel.RenderTransform as TranslateTransform ?? new TranslateTransform();
    detailPanel.RenderTransform = transform;
    
    slideOut.Completed += (s, e) => detailPanel.Visibility = Visibility.Collapsed;
    transform.BeginAnimation(TranslateTransform.XProperty, slideOut);
}
```

**Fade animations:**
```csharp
private void FadeIn(UIElement element, double duration = 200)
{
    element.Visibility = Visibility.Visible;
    var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(duration));
    element.BeginAnimation(UIElement.OpacityProperty, fade);
}

private void FadeOut(UIElement element, double duration = 200)
{
    var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(duration));
    fade.Completed += (s, e) => element.Visibility = Visibility.Collapsed;
    element.BeginAnimation(UIElement.OpacityProperty, fade);
}
```

**SuperTUI Enhancement Ideas:**
- Slide-in detail panel when task selected (terminal-like split)
- Fade in/out notification panels
- Slide-down command palette overlay
- Smooth expand/collapse for Expander controls

**Terminal Aesthetic Considerations:**
- Keep animations fast (100-250ms)
- Use linear or ease-out (no bouncy effects)
- Monochrome focus (no flashy colors)
- Optional: disable animations via config (accessibility)

### 2.3 Popup/Flyout Patterns

**WPF Popup control for context menus, tooltips, notifications:**

```csharp
// Create popup
var popup = new Popup
{
    PlacementTarget = taskListBox,
    Placement = PlacementMode.Bottom,
    StaysOpen = false,  // Close on click outside
    AllowsTransparency = true
};

// Popup content
var border = new Border
{
    Background = new SolidColorBrush(theme.Surface),
    BorderBrush = new SolidColorBrush(theme.Border),
    BorderThickness = new Thickness(1),
    Padding = new Thickness(10),
    Child = new StackPanel
    {
        Children = {
            new TextBlock { Text = "Quick Actions:" },
            CreatePopupButton("Mark Complete", () => { /* ... */ }),
            CreatePopupButton("Edit Task", () => { /* ... */ }),
            CreatePopupButton("Delete", () => { /* ... */ })
        }
    }
};
popup.Child = border;

// Show popup
popup.IsOpen = true;

// Close on ESC key
popup.KeyDown += (s, e) => {
    if (e.Key == Key.Escape) popup.IsOpen = false;
};
```

**SuperTUI Use Cases:**
- Context menus for tasks (right-click or keyboard shortcut)
- Quick filters (popup with checkboxes)
- Tag selector flyout
- Notification toasts (bottom-right corner)
- Color theme picker (popup palette)

**Integration with Existing Overlays:**
SuperTUI already has overlay patterns:
- `ShortcutOverlay` - keyboard shortcuts
- `QuickJumpOverlay` - workspace switcher
- `CommandPaletteWidget` - command search

**Enhancement:** Standardize popup/overlay infrastructure:
```csharp
public class OverlayManager
{
    public void ShowOverlay(UIElement content, OverlayPosition position);
    public void ShowPopup(UIElement content, UIElement target, PlacementMode placement);
    public void ShowNotification(string message, NotificationType type, int durationMs);
    public void DismissAll();
}
```

### 2.4 Expandable Detail Regions (Already Implemented!)

**AgendaWidget** already uses WPF Expander for progressive disclosure:

```csharp
private Expander BuildTimeGroup(string title, ObservableCollection<TaskItem> dataSource, 
                                 out ListBox listBox, System.Windows.Media.Color headerColor)
{
    var expander = new Expander
    {
        Header = new TextBlock { Text = $"{title} (0)", FontWeight = FontWeights.Bold },
        IsExpanded = true,
        Margin = new Thickness(0, 5, 0, 5)
    };
    
    listBox = new ListBox { ItemsSource = dataSource };
    expander.Content = listBox;
    
    return expander;
}
```

**Enhancement Ideas:**
- Animate expand/collapse (DoubleAnimation on Height)
- Remember expand state per user (save in widget state)
- Keyboard shortcuts (Space to toggle, Enter to expand all)
- Visual cues (▶/▼ icons, indentation)

---

## 3. ADAPTIVE LAYOUTS (Content-Driven Layout Changes)

### 3.1 Responsive Sizing (Like CSS Media Queries)

**WPF doesn't have built-in media queries, but you can implement responsive behavior:**

```csharp
// Listen to window size changes
this.SizeChanged += (s, e) => {
    var width = e.NewSize.Width;
    
    if (width < 800)
    {
        // Mobile layout: Stack vertically
        mainGrid.ColumnDefinitions.Clear();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(filterPanel, 0);
        Grid.SetRow(filterPanel, 0);
        Grid.SetColumn(taskPanel, 0);
        Grid.SetRow(taskPanel, 1);
        Grid.SetColumn(detailPanel, 0);
        Grid.SetRow(detailPanel, 2);
    }
    else if (width < 1200)
    {
        // Tablet layout: 2 columns
        mainGrid.ColumnDefinitions.Clear();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(filterPanel, 0);
        Grid.SetColumn(taskPanel, 0);
        Grid.SetRow(taskPanel, 1);
        Grid.SetColumn(detailPanel, 1);
    }
    else
    {
        // Desktop layout: 3 columns (default)
        RebuildDesktopLayout();
    }
};
```

**SuperTUI Considerations:**
- Desktop-only app (WPF limitation), no mobile/tablet support
- But: users resize windows! Handle narrow widths gracefully
- Minimum widget sizes (GridLayoutEngine enforces MinWidth/MinHeight)
- Grid splitters allow manual resizing

**Recommendation:** Add responsive breakpoints for narrow windows:
- < 600px: Hide filter panel, show hamburger menu
- < 900px: Stack panels vertically
- < 1200px: 2-column layout
- \>= 1200px: 3-column layout (default)

### 3.2 Content-Driven Layout Switching

**Example: Switch between list view and card view based on data:**

```csharp
// Data-driven layout
private void UpdateLayout()
{
    if (tasks.Count > 50)
    {
        // Large dataset: Use virtual scrolling list
        taskListBox.VirtualizingPanel.IsVirtualizing = true;
        taskListBox.VirtualizingPanel.VirtualizationMode = VirtualizationMode.Recycling;
        taskListBox.ItemTemplate = simpleTemplate;  // Simpler template for performance
    }
    else
    {
        // Small dataset: Use rich card view
        taskListBox.VirtualizingPanel.IsVirtualizing = false;
        taskListBox.ItemTemplate = detailedTemplate;  // Rich template with images, etc.
    }
}
```

**SuperTUI Application:**
- KanbanBoard: Switch to compact view when >20 tasks per column
- AgendaWidget: Collapse groups automatically when >10 tasks
- SystemMonitor: Adaptive detail level based on metric count

### 3.3 State-Based Visual Trees (DataTriggers)

**WPF DataTrigger changes UI based on data state:**

```csharp
// Create style with data triggers (in code-behind)
var style = new Style(typeof(ListBoxItem));

// Trigger: Overdue tasks get red background
var overdueTrigger = new DataTrigger
{
    Binding = new Binding("IsOverdue"),
    Value = true
};
overdueTrigger.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(theme.Error)));
overdueTrigger.Setters.Add(new Setter(FontWeightProperty, FontWeights.Bold));
style.Triggers.Add(overdueTrigger);

// Trigger: Completed tasks get gray text
var completedTrigger = new DataTrigger
{
    Binding = new Binding("Status"),
    Value = TaskStatus.Completed
};
completedTrigger.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(theme.ForegroundDisabled)));
completedTrigger.Setters.Add(new Setter(OpacityProperty, 0.6));
style.Triggers.Add(completedTrigger);

// Apply style
listBox.ItemContainerStyle = style;
```

**SuperTUI Use Cases:**
- Task priority colors (High → red, Medium → yellow, Low → gray)
- Status badges (Pending, InProgress, Completed, Cancelled)
- Due date warnings (overdue, today, soon)
- Tag-based styling (different colors per tag)

**Current Implementation:**
TaskManagementWidget already does manual color coding:
```csharp
detailStatus.Foreground = new SolidColorBrush(
    selectedTask.Status == TaskStatus.Completed ? theme.Success :
    selectedTask.Status == TaskStatus.InProgress ? theme.Info :
    selectedTask.Status == TaskStatus.Cancelled ? theme.ForegroundDisabled :
    theme.Foreground);
```

**Enhancement:** Use DataTriggers for automatic styling:
```csharp
// Apply once, styles update automatically when data changes
private void ApplyConditionalStyles(ListBox listBox)
{
    var style = new Style(typeof(ListBoxItem));
    
    // Overdue tasks
    style.Triggers.Add(CreateDataTrigger("IsOverdue", true, 
        (BackgroundProperty, new SolidColorBrush(theme.Error))));
    
    // Due today
    style.Triggers.Add(CreateDataTrigger("IsDueToday", true, 
        (BackgroundProperty, new SolidColorBrush(theme.Warning))));
    
    // Completed
    style.Triggers.Add(CreateDataTrigger("Status", TaskStatus.Completed,
        (ForegroundProperty, new SolidColorBrush(theme.ForegroundDisabled)),
        (OpacityProperty, 0.6)));
    
    listBox.ItemContainerStyle = style;
}

private DataTrigger CreateDataTrigger(string property, object value, params (DependencyProperty, object)[] setters)
{
    var trigger = new DataTrigger { Binding = new Binding(property), Value = value };
    foreach (var (prop, val) in setters)
        trigger.Setters.Add(new Setter(prop, val));
    return trigger;
}
```

---

## 4. LIVE FILTERING & GROUPING

### 4.1 CollectionViewSource Live Filtering (Real-time Search)

**Pattern:** Filter ObservableCollection without rebuilding list.

```csharp
// Setup filterable view
private ICollectionView tasksView;
private string searchText = "";

private void SetupFiltering()
{
    tasksView = CollectionViewSource.GetDefaultView(tasks);
    tasksView.Filter = FilterTask;
    listBox.ItemsSource = tasksView;
}

// Filter predicate
private bool FilterTask(object item)
{
    var task = item as TaskItem;
    if (task == null) return false;
    
    // Multi-criteria filter
    if (!string.IsNullOrEmpty(searchText) && 
        !task.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        return false;
    
    if (showOnlyPending && task.Status != TaskStatus.Pending)
        return false;
    
    if (filterByTag != null && !task.Tags.Contains(filterByTag))
        return false;
    
    return true;
}

// Update filter (called when search text changes)
private void OnSearchTextChanged(string newText)
{
    searchText = newText;
    tasksView.Refresh();  // Re-runs filter, updates UI instantly
}
```

**SuperTUI Application:**
- Live search in TaskManagementWidget (filter as you type)
- Tag filtering (checkbox list, instant update)
- Status filtering (show only pending/completed)
- Date range filtering (slider or date picker)

**Performance:**
- ✅ No collection rebuild (just filter predicate)
- ✅ Virtual scrolling compatible
- ✅ Smooth updates (no flickering)

**Example: Live Search Box**
```csharp
private TextBox CreateSearchBox()
{
    var searchBox = new TextBox
    {
        FontFamily = new FontFamily("Consolas"),
        Background = new SolidColorBrush(theme.Surface),
        Foreground = new SolidColorBrush(theme.Foreground),
        Padding = new Thickness(5)
    };
    
    // Debounced search (wait 300ms after typing stops)
    DispatcherTimer searchTimer = null;
    searchBox.TextChanged += (s, e) =>
    {
        searchTimer?.Stop();
        searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        searchTimer.Tick += (_, __) =>
        {
            searchTimer.Stop();
            OnSearchTextChanged(searchBox.Text);
        };
        searchTimer.Start();
    };
    
    return searchBox;
}
```

### 4.2 Dynamic Grouping

**WPF CollectionViewSource supports grouping:**

```csharp
// Group tasks by status
private void SetupGrouping()
{
    var view = CollectionViewSource.GetDefaultView(tasks);
    view.GroupDescriptions.Add(new PropertyGroupDescription("Status"));
    
    // Optionally sort groups
    view.SortDescriptions.Add(new SortDescription("Status", ListSortDirection.Ascending));
    
    listBox.ItemsSource = view;
}

// Custom group template
private DataTemplate CreateGroupTemplate()
{
    var template = new DataTemplate();
    var factory = new FrameworkElementFactory(typeof(TextBlock));
    factory.SetBinding(TextBlock.TextProperty, new Binding("Name"));  // Group name
    factory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
    factory.SetValue(TextBlock.FontSizeProperty, 14.0);
    factory.SetValue(TextBlock.MarginProperty, new Thickness(0, 10, 0, 5));
    template.VisualTree = factory;
    return template;
}

// Apply to ListBox
var groupStyle = new GroupStyle { HeaderTemplate = CreateGroupTemplate() };
listBox.GroupStyle.Add(groupStyle);
```

**SuperTUI Use Cases:**
- Group tasks by: Status, Priority, Project, Tag, Due Date
- Group files by: Type, Size, Modified Date
- Group widgets by: Category, Workspace

**Dynamic Group Switching:**
```csharp
private void SetGrouping(string propertyName)
{
    var view = CollectionViewSource.GetDefaultView(tasks);
    view.GroupDescriptions.Clear();
    
    if (!string.IsNullOrEmpty(propertyName))
    {
        view.GroupDescriptions.Add(new PropertyGroupDescription(propertyName));
    }
    
    view.Refresh();
}

// UI: Dropdown to select grouping
var groupByCombo = new ComboBox
{
    ItemsSource = new[] { "None", "Status", "Priority", "Project", "DueDate" }
};
groupByCombo.SelectionChanged += (s, e) => SetGrouping(groupByCombo.SelectedItem?.ToString());
```

### 4.3 Live Sorting

**CollectionViewSource supports multi-level sorting:**

```csharp
private void SetupSorting()
{
    var view = CollectionViewSource.GetDefaultView(tasks);
    
    // Primary sort: Priority (High → Low)
    view.SortDescriptions.Add(new SortDescription("Priority", ListSortDirection.Descending));
    
    // Secondary sort: DueDate (earliest first)
    view.SortDescriptions.Add(new SortDescription("DueDate", ListSortDirection.Ascending));
    
    // Tertiary sort: Title (alphabetical)
    view.SortDescriptions.Add(new SortDescription("Title", ListSortDirection.Ascending));
    
    view.Refresh();
}

// Dynamic sort (user clicks column header)
private void ToggleSort(string propertyName)
{
    var view = CollectionViewSource.GetDefaultView(tasks);
    var existing = view.SortDescriptions.FirstOrDefault(s => s.PropertyName == propertyName);
    
    view.SortDescriptions.Clear();
    
    if (existing.PropertyName == propertyName)
    {
        // Toggle direction
        var newDirection = existing.Direction == ListSortDirection.Ascending 
            ? ListSortDirection.Descending 
            : ListSortDirection.Ascending;
        view.SortDescriptions.Add(new SortDescription(propertyName, newDirection));
    }
    else
    {
        // New sort
        view.SortDescriptions.Add(new SortDescription(propertyName, ListSortDirection.Ascending));
    }
    
    view.Refresh();
}
```

**SuperTUI Enhancement:**
- Sortable columns in task lists (click header to sort)
- Multi-level sorting (Ctrl+Click for secondary sort)
- Save sort preferences per widget

### 4.4 Virtual Scrolling for Performance

**WPF VirtualizingStackPanel (already used in SuperTUI):**

```csharp
// Enable virtualization for large lists
var listBox = new ListBox
{
    VirtualizingPanel.IsVirtualizing = true,
    VirtualizingPanel.VirtualizationMode = VirtualizationMode.Recycling,
    VirtualizingPanel.CacheLength = new VirtualizationCacheLength(5, 5),  // Cache 5 items before/after viewport
    VirtualizingPanel.CacheLengthUnit = VirtualizationCacheLengthUnit.Page
};
```

**Benefits:**
- Only renders visible items (+ small cache)
- Handles 10,000+ items smoothly
- Automatic recycling (reuses item containers)

**SuperTUI Status:** Already implemented in KanbanBoardWidget, AgendaWidget, TaskManagementWidget.

---

## 5. PROGRESSIVE DISCLOSURE

### 5.1 Expand/Collapse Sections (Already Implemented!)

**AgendaWidget uses WPF Expander:**

```csharp
var expander = new Expander
{
    Header = new TextBlock { Text = "OVERDUE (5)", FontWeight = FontWeights.Bold },
    IsExpanded = true,
    Content = taskListBox
};
```

**Enhancements:**
- Animate expand/collapse (smooth height transition)
- Collapse all / Expand all buttons
- Remember expand state per user
- Keyboard navigation (Tab to next expander, Space to toggle)

**Animated Expander:**
```csharp
private Expander CreateAnimatedExpander(string header, UIElement content)
{
    var expander = new Expander
    {
        Header = new TextBlock { Text = header, FontWeight = FontWeights.Bold },
        Content = content
    };
    
    // Animate expand/collapse
    expander.Expanded += (s, e) => AnimateExpand(content);
    expander.Collapsed += (s, e) => AnimateCollapse(content);
    
    return expander;
}

private void AnimateExpand(UIElement element)
{
    var animation = new DoubleAnimation
    {
        From = 0,
        To = double.NaN,  // NaN = auto-size
        Duration = TimeSpan.FromMilliseconds(200),
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
    };
    
    element.BeginAnimation(FrameworkElement.HeightProperty, animation);
}

private void AnimateCollapse(UIElement element)
{
    var animation = new DoubleAnimation
    {
        From = element.RenderSize.Height,
        To = 0,
        Duration = TimeSpan.FromMilliseconds(150),
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
    };
    
    element.BeginAnimation(FrameworkElement.HeightProperty, animation);
}
```

### 5.2 Drill-Down Hierarchies (TreeView)

**WPF TreeView for nested data:**

```csharp
// TreeView with data binding
var treeView = new TreeView
{
    ItemsSource = projects,
    FontFamily = new FontFamily("Consolas")
};

// Template for each level
var hierarchicalTemplate = new HierarchicalDataTemplate
{
    DataType = typeof(Project),
    ItemsSource = new Binding("Tasks")  // Child property
};

var factory = new FrameworkElementFactory(typeof(StackPanel));
factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
iconFactory.SetBinding(TextBlock.TextProperty, new Binding("Icon"));
factory.AppendChild(iconFactory);

var textFactory = new FrameworkElementFactory(typeof(TextBlock));
textFactory.SetBinding(TextBlock.TextProperty, new Binding("Name"));
textFactory.SetValue(TextBlock.MarginProperty, new Thickness(5, 0, 0, 0));
factory.AppendChild(textFactory);

hierarchicalTemplate.VisualTree = factory;
treeView.ItemTemplate = hierarchicalTemplate;

// Expand/collapse events
treeView.SelectedItemChanged += (s, e) => {
    var selected = treeView.SelectedItem;
    // Load detail panel
};
```

**SuperTUI Use Cases:**
- Project → Task → Subtask hierarchy
- File explorer (folders → files)
- Widget categories → widgets
- Command palette categories → commands

**Already Partially Implemented:**
- TreeTaskListControl supports parent/subtask hierarchy
- FileExplorerWidget has directory tree

### 5.3 Breadcrumb Navigation with Back Stack

**Implement breadcrumb trail:**

```csharp
public class NavigationManager
{
    private Stack<NavigationState> backStack = new Stack<NavigationState>();
    private Stack<NavigationState> forwardStack = new Stack<NavigationState>();
    
    public void NavigateTo(string location, object context)
    {
        // Save current state
        if (CurrentState != null)
            backStack.Push(CurrentState);
        
        // Clear forward stack (new branch)
        forwardStack.Clear();
        
        // Navigate
        CurrentState = new NavigationState { Location = location, Context = context };
        OnNavigated?.Invoke(this, CurrentState);
    }
    
    public void GoBack()
    {
        if (backStack.Count == 0) return;
        
        forwardStack.Push(CurrentState);
        CurrentState = backStack.Pop();
        OnNavigated?.Invoke(this, CurrentState);
    }
    
    public void GoForward()
    {
        if (forwardStack.Count == 0) return;
        
        backStack.Push(CurrentState);
        CurrentState = forwardStack.Pop();
        OnNavigated?.Invoke(this, CurrentState);
    }
    
    public List<string> GetBreadcrumbs()
    {
        return backStack.Reverse().Select(s => s.Location).ToList();
    }
}

// Breadcrumb UI
private StackPanel CreateBreadcrumbs(List<string> path)
{
    var panel = new StackPanel { Orientation = Orientation.Horizontal };
    
    for (int i = 0; i < path.Count; i++)
    {
        var button = new Button
        {
            Content = path[i],
            Style = new Style(),  // Flat style
            Cursor = Cursors.Hand
        };
        
        int index = i;
        button.Click += (s, e) => NavigateToLevel(index);
        
        panel.Children.Add(button);
        
        if (i < path.Count - 1)
        {
            panel.Children.Add(new TextBlock { Text = " > ", VerticalAlignment = VerticalAlignment.Center });
        }
    }
    
    return panel;
}
```

**SuperTUI Application:**
- File explorer (Home > Projects > MyProject > src)
- Widget navigation (Workspaces > Workspace 1 > TaskManagement)
- Task hierarchy (Project > Epic > Task > Subtask)

### 5.4 Detail-on-Demand (Hover Tooltips, Click for Full Detail)

**Rich tooltips:**

```csharp
// Create rich tooltip with layout
private ToolTip CreateTaskTooltip(TaskItem task)
{
    var tooltip = new ToolTip
    {
        Content = new Border
        {
            Background = new SolidColorBrush(theme.Surface),
            BorderBrush = new SolidColorBrush(theme.Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = new StackPanel
            {
                Children = {
                    new TextBlock { Text = task.Title, FontWeight = FontWeights.Bold },
                    new TextBlock { Text = task.Description, TextWrapping = TextWrapping.Wrap, MaxWidth = 300 },
                    new TextBlock { Text = $"Due: {task.DueDate:yyyy-MM-dd}", Margin = new Thickness(0, 5, 0, 0) },
                    new TextBlock { Text = $"Priority: {task.Priority}", Foreground = GetPriorityBrush(task.Priority) }
                }
            }
        },
        StaysOpen = false,
        Placement = PlacementMode.Mouse
    };
    
    return tooltip;
}

// Apply to list item
listBoxItem.ToolTip = CreateTaskTooltip(task);
```

**Click-for-detail pattern:**
```csharp
// Summary view (collapsed)
var summaryPanel = new StackPanel { /* ... */ };

// Detail view (expanded)
var detailPanel = new StackPanel { Visibility = Visibility.Collapsed, /* ... */ };

// Toggle on click
summaryPanel.MouseDown += (s, e) =>
{
    if (detailPanel.Visibility == Visibility.Collapsed)
    {
        detailPanel.Visibility = Visibility.Visible;
        summaryPanel.Background = new SolidColorBrush(theme.Hover);
    }
    else
    {
        detailPanel.Visibility = Visibility.Collapsed;
        summaryPanel.Background = Brushes.Transparent;
    }
};
```

---

## 6. MULTI-MODE VIEWS

### 6.1 View Mode Switching (List/Grid/Kanban/Timeline)

**Pattern:** Same data, different visualizations.

```csharp
public enum ViewMode
{
    List,      // Linear list (default)
    Grid,      // Card grid (thumbnails)
    Kanban,    // Columns by status
    Timeline,  // Horizontal timeline
    Calendar   // Month/week view
}

public class TaskViewer
{
    private ViewMode currentMode = ViewMode.List;
    private ObservableCollection<TaskItem> tasks;
    
    private ListBox listView;
    private ItemsControl gridView;
    private Grid kanbanView;
    private Canvas timelineView;
    private Calendar calendarView;
    
    public void SetViewMode(ViewMode mode)
    {
        // Hide all views
        listView.Visibility = Visibility.Collapsed;
        gridView.Visibility = Visibility.Collapsed;
        kanbanView.Visibility = Visibility.Collapsed;
        timelineView.Visibility = Visibility.Collapsed;
        calendarView.Visibility = Visibility.Collapsed;
        
        // Show selected view
        currentMode = mode;
        switch (mode)
        {
            case ViewMode.List:
                listView.Visibility = Visibility.Visible;
                break;
            case ViewMode.Grid:
                gridView.Visibility = Visibility.Visible;
                RebuildGridView();
                break;
            case ViewMode.Kanban:
                kanbanView.Visibility = Visibility.Visible;
                RebuildKanbanView();
                break;
            case ViewMode.Timeline:
                timelineView.Visibility = Visibility.Visible;
                RebuildTimelineView();
                break;
            case ViewMode.Calendar:
                calendarView.Visibility = Visibility.Visible;
                RebuildCalendarView();
                break;
        }
        
        SaveViewPreference(mode);
    }
}
```

**SuperTUI Application:**

**Already Implemented:**
- TaskManagementWidget (list view)
- KanbanBoardWidget (kanban view)
- AgendaWidget (timeline-ish view)

**Enhancement: Unified Task Viewer with Mode Switcher**
```csharp
// Toolbar with view mode buttons
var toolbar = new StackPanel { Orientation = Orientation.Horizontal };

toolbar.Children.Add(CreateViewModeButton("List", ViewMode.List, "☰"));
toolbar.Children.Add(CreateViewModeButton("Grid", ViewMode.Grid, "▦"));
toolbar.Children.Add(CreateViewModeButton("Kanban", ViewMode.Kanban, "▥"));
toolbar.Children.Add(CreateViewModeButton("Timeline", ViewMode.Timeline, "━"));
toolbar.Children.Add(CreateViewModeButton("Calendar", ViewMode.Calendar, "📅"));

private Button CreateViewModeButton(string label, ViewMode mode, string icon)
{
    var button = new Button
    {
        Content = $"{icon} {label}",
        FontFamily = new FontFamily("Consolas"),
        Background = new SolidColorBrush(theme.Surface),
        Foreground = new SolidColorBrush(theme.Foreground),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(10, 5, 10, 5),
        Margin = new Thickness(5, 0, 0, 0),
        Cursor = Cursors.Hand
    };
    
    button.Click += (s, e) => SetViewMode(mode);
    
    return button;
}
```

### 6.2 Layout Templates Per Mode

**Define reusable templates:**

```csharp
// List view template (compact)
private DataTemplate CreateListTemplate()
{
    var template = new DataTemplate();
    var factory = new FrameworkElementFactory(typeof(StackPanel));
    factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
    
    var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
    iconFactory.SetBinding(TextBlock.TextProperty, new Binding("StatusIcon"));
    factory.AppendChild(iconFactory);
    
    var titleFactory = new FrameworkElementFactory(typeof(TextBlock));
    titleFactory.SetBinding(TextBlock.TextProperty, new Binding("Title"));
    titleFactory.SetValue(TextBlock.MarginProperty, new Thickness(5, 0, 0, 0));
    factory.AppendChild(titleFactory);
    
    template.VisualTree = factory;
    return template;
}

// Grid view template (card)
private DataTemplate CreateGridTemplate()
{
    var template = new DataTemplate();
    var borderFactory = new FrameworkElementFactory(typeof(Border));
    borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(theme.Surface));
    borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(theme.Border));
    borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
    borderFactory.SetValue(Border.PaddingProperty, new Thickness(10));
    borderFactory.SetValue(Border.MarginProperty, new Thickness(5));
    borderFactory.SetValue(Border.WidthProperty, 200.0);
    borderFactory.SetValue(Border.HeightProperty, 150.0);
    
    var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
    borderFactory.AppendChild(stackFactory);
    
    var titleFactory = new FrameworkElementFactory(typeof(TextBlock));
    titleFactory.SetBinding(TextBlock.TextProperty, new Binding("Title"));
    titleFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
    titleFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
    stackFactory.AppendChild(titleFactory);
    
    var descFactory = new FrameworkElementFactory(typeof(TextBlock));
    descFactory.SetBinding(TextBlock.TextProperty, new Binding("Description"));
    descFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
    descFactory.SetValue(TextBlock.OpacityProperty, 0.7);
    descFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 5, 0, 0));
    stackFactory.AppendChild(descFactory);
    
    template.VisualTree = borderFactory;
    return template;
}

// Apply template based on mode
private void ApplyTemplate(ViewMode mode)
{
    switch (mode)
    {
        case ViewMode.List:
            listView.ItemTemplate = CreateListTemplate();
            break;
        case ViewMode.Grid:
            gridView.ItemTemplate = CreateGridTemplate();
            break;
    }
}
```

### 6.3 Preserving State Across Mode Switches

**Challenge:** Switching views shouldn't lose selection, scroll position, filters.

```csharp
public class ViewState
{
    public Guid? SelectedItemId { get; set; }
    public double ScrollOffset { get; set; }
    public string SearchText { get; set; }
    public List<string> ActiveFilters { get; set; }
    public string SortProperty { get; set; }
    public ListSortDirection SortDirection { get; set; }
}

private Dictionary<ViewMode, ViewState> viewStates = new Dictionary<ViewMode, ViewState>();

private void SaveViewState(ViewMode mode)
{
    var state = new ViewState();
    
    switch (mode)
    {
        case ViewMode.List:
            state.SelectedItemId = (listView.SelectedItem as TaskItem)?.Id;
            state.ScrollOffset = GetScrollOffset(listView);
            break;
        case ViewMode.Grid:
            state.SelectedItemId = (gridView.SelectedItem as TaskItem)?.Id;
            state.ScrollOffset = GetScrollOffset(gridView);
            break;
        // ... other modes
    }
    
    state.SearchText = searchBox.Text;
    state.ActiveFilters = GetActiveFilters();
    state.SortProperty = GetCurrentSortProperty();
    state.SortDirection = GetCurrentSortDirection();
    
    viewStates[mode] = state;
}

private void RestoreViewState(ViewMode mode)
{
    if (!viewStates.ContainsKey(mode)) return;
    
    var state = viewStates[mode];
    
    // Restore selection
    if (state.SelectedItemId.HasValue)
    {
        var item = tasks.FirstOrDefault(t => t.Id == state.SelectedItemId.Value);
        if (item != null)
        {
            switch (mode)
            {
                case ViewMode.List:
                    listView.SelectedItem = item;
                    listView.ScrollIntoView(item);
                    break;
                case ViewMode.Grid:
                    gridView.SelectedItem = item;
                    ScrollToItem(gridView, item);
                    break;
            }
        }
    }
    
    // Restore filters/search
    searchBox.Text = state.SearchText;
    ApplyFilters(state.ActiveFilters);
    ApplySorting(state.SortProperty, state.SortDirection);
}

// Switch mode with state preservation
public void SetViewModeWithState(ViewMode newMode)
{
    SaveViewState(currentMode);
    SetViewMode(newMode);
    RestoreViewState(newMode);
}
```

---

## 7. RECOMMENDED IMPLEMENTATION PRIORITIES FOR SUPERTUI

### Phase 1: Quick Wins (Low Effort, High Impact)

1. **Live Search/Filtering** (CollectionViewSource)
   - Add search box to TaskManagementWidget
   - Filter as you type (300ms debounce)
   - Clear button (X icon)

2. **DataTrigger Styling** (Automatic Color Coding)
   - Replace manual color updates with DataTriggers
   - Reduces code, improves maintainability
   - Automatic updates when data changes

3. **Rich Tooltips** (Detail-on-Demand)
   - Add tooltips to task list items
   - Show description, tags, due date on hover
   - Low-friction way to see details

4. **Animated Transitions** (Polish)
   - Fade in/out for detail panels
   - Slide animations for overlays
   - 150-250ms duration (fast, not distracting)

### Phase 2: Architectural Improvements (Medium Effort, High Value)

5. **Unified Overlay Manager**
   - Standardize popups, flyouts, notifications
   - Consistent look and behavior
   - Keyboard-accessible (ESC to dismiss)

6. **Responsive Breakpoints**
   - Handle narrow window widths gracefully
   - Stack panels vertically on small screens
   - Hide/show panels based on available space

7. **View Mode Switcher** (List/Grid/Kanban Toggle)
   - Add toolbar with mode buttons
   - Preserve state across mode switches
   - Save user preference per widget

8. **Breadcrumb Navigation**
   - File explorer breadcrumbs
   - Task hierarchy breadcrumbs
   - Back/Forward buttons (Alt+Left/Right)

### Phase 3: Advanced Features (High Effort, High Impact)

9. **Dynamic Grouping/Sorting**
   - Dropdown to select grouping (Status, Priority, Project, etc.)
   - Sortable column headers
   - Multi-level sorting (Ctrl+Click)

10. **Timeline/Calendar Views**
    - Timeline view for tasks (horizontal Gantt-style)
    - Calendar view for due dates
    - Drag tasks to change dates

11. **Drill-Down TreeView**
    - Project → Epic → Task → Subtask hierarchy
    - Expand/collapse branches
    - Lazy loading for performance

12. **Advanced Animations** (Optional Polish)
    - Staggered list animations (items fade in sequentially)
    - Flip animations for card views
    - Parallax scrolling effects

### Phase 4: Performance Optimizations

13. **Virtual Scrolling Tuning**
    - Already implemented, but tune cache sizes
    - Profile with large datasets (1000+ tasks)
    - Benchmark before/after

14. **Deferred Loading**
    - Load detail panels on demand (not upfront)
    - Lazy-load images, charts
    - Progressive enhancement

15. **Memory Profiling**
    - Check for leaks (event handlers, bindings)
    - Dispose properly (OnDispose already implemented)
    - Weak references where appropriate

---

## 8. CODE EXAMPLES FOR SUPERTUI

### 8.1 Enhanced TaskManagementWidget with Live Search

```csharp
public class TaskManagementWidget : WidgetBase, IThemeable
{
    private ICollectionView filteredTasksView;
    private TextBox searchBox;
    private string searchText = "";
    
    private void BuildTaskPanel()
    {
        var panel = new StackPanel();
        
        // Search box at top
        searchBox = CreateSearchBox();
        panel.Children.Add(searchBox);
        
        // Task list with filtered view
        filteredTasksView = CollectionViewSource.GetDefaultView(tasks);
        filteredTasksView.Filter = FilterTask;
        
        treeTaskListControl = new TreeTaskListControl(logger, themeManager);
        treeTaskListControl.LoadTasks(filteredTasksView);
        panel.Children.Add(treeTaskListControl);
        
        return panel;
    }
    
    private TextBox CreateSearchBox()
    {
        var searchBox = new TextBox
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Background = new SolidColorBrush(theme.Surface),
            Foreground = new SolidColorBrush(theme.Foreground),
            BorderBrush = new SolidColorBrush(theme.Border),
            Padding = new Thickness(5),
            Margin = new Thickness(0, 0, 0, 10)
        };
        
        // Placeholder text
        searchBox.GotFocus += (s, e) => {
            if (searchBox.Text == "Search tasks...")
                searchBox.Text = "";
        };
        searchBox.LostFocus += (s, e) => {
            if (string.IsNullOrEmpty(searchBox.Text))
                searchBox.Text = "Search tasks...";
        };
        searchBox.Text = "Search tasks...";
        
        // Debounced search
        DispatcherTimer searchTimer = null;
        searchBox.TextChanged += (s, e) =>
        {
            if (searchBox.Text == "Search tasks...") return;
            
            searchTimer?.Stop();
            searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            searchTimer.Tick += (_, __) =>
            {
                searchTimer.Stop();
                searchText = searchBox.Text;
                filteredTasksView.Refresh();
            };
            searchTimer.Start();
        };
        
        return searchBox;
    }
    
    private bool FilterTask(object item)
    {
        var task = item as TaskItem;
        if (task == null) return false;
        
        // Apply search filter
        if (!string.IsNullOrEmpty(searchText))
        {
            if (!task.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                !(task.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return false;
            }
        }
        
        // Apply current filter (All, Today, Overdue, etc.)
        return currentFilter.Predicate(task);
    }
}
```

### 8.2 Animated Detail Panel

```csharp
private Border detailPanel;
private bool isDetailPanelVisible = false;

private void ToggleDetailPanel(TaskItem task)
{
    if (task == null)
    {
        HideDetailPanel();
        return;
    }
    
    if (!isDetailPanelVisible)
    {
        ShowDetailPanel(task);
    }
    else
    {
        HideDetailPanel();
    }
}

private void ShowDetailPanel(TaskItem task)
{
    // Populate detail panel
    RefreshDetailPanel(task);
    
    // Slide in from right
    detailPanel.Visibility = Visibility.Visible;
    
    var slideIn = new DoubleAnimation
    {
        From = detailPanel.ActualWidth,
        To = 0,
        Duration = TimeSpan.FromMilliseconds(200),
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
    };
    
    var transform = new TranslateTransform();
    detailPanel.RenderTransform = transform;
    transform.BeginAnimation(TranslateTransform.XProperty, slideIn);
    
    isDetailPanelVisible = true;
}

private void HideDetailPanel()
{
    var slideOut = new DoubleAnimation
    {
        From = 0,
        To = detailPanel.ActualWidth,
        Duration = TimeSpan.FromMilliseconds(150),
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
    };
    
    var transform = detailPanel.RenderTransform as TranslateTransform ?? new TranslateTransform();
    detailPanel.RenderTransform = transform;
    
    slideOut.Completed += (s, e) => detailPanel.Visibility = Visibility.Collapsed;
    transform.BeginAnimation(TranslateTransform.XProperty, slideOut);
    
    isDetailPanelVisible = false;
}
```

### 8.3 DataTrigger-Based Styling

```csharp
private void ApplyConditionalStyling(ListBox listBox)
{
    var style = new Style(typeof(ListBoxItem));
    
    // Overdue tasks: red background, bold text
    var overdueTrigger = new DataTrigger
    {
        Binding = new Binding("IsOverdue"),
        Value = true
    };
    overdueTrigger.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(theme.Error)));
    overdueTrigger.Setters.Add(new Setter(FontWeightProperty, FontWeights.Bold));
    style.Triggers.Add(overdueTrigger);
    
    // Due today: yellow background
    var todayTrigger = new DataTrigger
    {
        Binding = new Binding("IsDueToday"),
        Value = true
    };
    todayTrigger.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(theme.Warning)));
    style.Triggers.Add(todayTrigger);
    
    // Completed tasks: gray text, reduced opacity
    var completedTrigger = new DataTrigger
    {
        Binding = new Binding("Status"),
        Value = TaskStatus.Completed
    };
    completedTrigger.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(theme.ForegroundDisabled)));
    completedTrigger.Setters.Add(new Setter(OpacityProperty, 0.6));
    style.Triggers.Add(completedTrigger);
    
    // High priority: red accent border
    var highPriorityTrigger = new DataTrigger
    {
        Binding = new Binding("Priority"),
        Value = TaskPriority.High
    };
    highPriorityTrigger.Setters.Add(new Setter(BorderBrushProperty, new SolidColorBrush(theme.Error)));
    highPriorityTrigger.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(2, 0, 0, 0)));
    style.Triggers.Add(highPriorityTrigger);
    
    listBox.ItemContainerStyle = style;
}
```

### 8.4 View Mode Switcher

```csharp
public enum TaskViewMode
{
    List,
    Kanban,
    Timeline,
    Calendar
}

private TaskViewMode currentViewMode = TaskViewMode.List;
private Dictionary<TaskViewMode, ViewState> viewStates = new Dictionary<TaskViewMode, ViewState>();

private UIElement CreateViewModeSwitcher()
{
    var toolbar = new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Background = new SolidColorBrush(theme.Surface),
        Padding = new Thickness(5)
    };
    
    toolbar.Children.Add(new TextBlock
    {
        Text = "View:",
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 10, 0),
        Foreground = new SolidColorBrush(theme.ForegroundSecondary)
    });
    
    toolbar.Children.Add(CreateViewButton("List", TaskViewMode.List, "☰"));
    toolbar.Children.Add(CreateViewButton("Kanban", TaskViewMode.Kanban, "▥"));
    toolbar.Children.Add(CreateViewButton("Timeline", TaskViewMode.Timeline, "━"));
    toolbar.Children.Add(CreateViewButton("Calendar", TaskViewMode.Calendar, "📅"));
    
    return toolbar;
}

private Button CreateViewButton(string label, TaskViewMode mode, string icon)
{
    var button = new Button
    {
        Content = $"{icon} {label}",
        FontFamily = new FontFamily("Consolas"),
        FontSize = 10,
        Background = new SolidColorBrush(mode == currentViewMode ? theme.Focus : theme.Surface),
        Foreground = new SolidColorBrush(theme.Foreground),
        BorderBrush = new SolidColorBrush(theme.Border),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(8, 4, 8, 4),
        Margin = new Thickness(2, 0, 2, 0),
        Cursor = Cursors.Hand
    };
    
    button.Click += (s, e) => SwitchViewMode(mode);
    
    return button;
}

private void SwitchViewMode(TaskViewMode newMode)
{
    if (newMode == currentViewMode) return;
    
    // Save current view state
    SaveViewState(currentViewMode);
    
    // Hide all views
    listView.Visibility = Visibility.Collapsed;
    kanbanView.Visibility = Visibility.Collapsed;
    timelineView.Visibility = Visibility.Collapsed;
    calendarView.Visibility = Visibility.Collapsed;
    
    // Show new view
    currentViewMode = newMode;
    switch (newMode)
    {
        case TaskViewMode.List:
            listView.Visibility = Visibility.Visible;
            break;
        case TaskViewMode.Kanban:
            kanbanView.Visibility = Visibility.Visible;
            break;
        case TaskViewMode.Timeline:
            timelineView.Visibility = Visibility.Visible;
            break;
        case TaskViewMode.Calendar:
            calendarView.Visibility = Visibility.Visible;
            break;
    }
    
    // Restore view state
    RestoreViewState(newMode);
    
    // Update toolbar button styles
    UpdateViewButtonStyles();
    
    logger?.Info("TaskWidget", $"Switched to {newMode} view");
}
```

---

## 9. CONCLUSION

### Key Takeaways

1. **SuperTUI Already Uses Many Best Practices:**
   - EventBus for inter-widget communication ✅
   - ObservableCollection for live data binding ✅
   - Master-detail patterns (3-pane layouts) ✅
   - Progressive disclosure (Expander controls) ✅
   - Virtual scrolling for performance ✅

2. **High-Value Enhancements:**
   - Live search/filtering (CollectionViewSource)
   - DataTrigger-based styling (automatic updates)
   - Animated transitions (polish, 150-250ms)
   - View mode switching (List/Kanban/Timeline)
   - Rich tooltips (detail-on-demand)

3. **WPF Strengths for SuperTUI:**
   - Powerful data binding (no manual UI updates)
   - Built-in animations (smooth, hardware-accelerated)
   - Flexible templating (same data, different views)
   - Virtual scrolling (handles large datasets)
   - Event-driven architecture (fits SuperTUI's EventBus)

4. **Avoid These Patterns:**
   - ❌ XAML (SuperTUI uses code-behind)
   - ❌ MVVM frameworks (overkill for this architecture)
   - ❌ CollectionViewSource for cross-widget sync (use EventBus)
   - ❌ Complex animations (keep it terminal-like)
   - ❌ Heavy graphics (monochrome, text-focused)

5. **Architecture Fit:**
   - All patterns compatible with SuperTUI's DI system
   - No breaking changes to existing widgets
   - Incremental adoption (widget by widget)
   - Backward compatible (old code still works)

### Next Steps

**Immediate Actions:**
1. Add live search to TaskManagementWidget
2. Replace manual color coding with DataTriggers
3. Add slide animations to detail panels

**Short-Term (2-4 weeks):**
1. Implement view mode switcher
2. Add rich tooltips to all list views
3. Create unified overlay manager

**Long-Term (1-3 months):**
1. Timeline/Calendar views for tasks
2. Drill-down TreeView for hierarchies
3. Performance profiling and optimization

---

## 10. REFERENCES

### WPF Documentation
- [Data Binding Overview](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/data/)
- [CollectionViewSource Class](https://docs.microsoft.com/en-us/dotnet/api/system.windows.data.collectionviewsource)
- [Animation Overview](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/animation-overview)
- [DataTrigger Class](https://docs.microsoft.com/en-us/dotnet/api/system.windows.datatrigger)
- [VirtualizingStackPanel](https://docs.microsoft.com/en-us/dotnet/api/system.windows.controls.virtualizingstackpanel)

### SuperTUI Codebase
- `/home/teej/supertui/WPF/Core/Components/WidgetBase.cs` - Base widget class
- `/home/teej/supertui/WPF/Core/Infrastructure/EventBus.cs` - Pub/sub system
- `/home/teej/supertui/WPF/Widgets/TaskManagementWidget.cs` - Master-detail example
- `/home/teej/supertui/WPF/Widgets/KanbanBoardWidget.cs` - Multi-mode view
- `/home/teej/supertui/WPF/Widgets/AgendaWidget.cs` - Progressive disclosure

### Design Patterns
- [Master-Detail Pattern](https://docs.microsoft.com/en-us/windows/apps/design/controls/master-details)
- [Responsive Design](https://docs.microsoft.com/en-us/windows/apps/design/layout/responsive-design)
- [Progressive Disclosure](https://www.nngroup.com/articles/progressive-disclosure/)

---

**Document Version:** 1.0  
**Last Updated:** 2025-10-27  
**Author:** Claude Code Research  
**Status:** Complete
