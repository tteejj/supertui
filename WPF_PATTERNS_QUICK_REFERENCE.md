# WPF Dynamic UI Patterns - Quick Reference
**For SuperTUI Development**

---

## 1. MASTER-DETAIL PATTERNS

### EventBus Coordination (Current Pattern - Keep This!)
```csharp
// Publish selection
EventBus.Publish(new TaskSelectedEvent { Task = task, SourceWidget = "KanbanBoard" });

// Subscribe in other widgets
EventBus.Subscribe<TaskSelectedEvent>(evt => {
    if (evt.SourceWidget != WidgetType) SelectTaskById(evt.Task.Id);
});
```

### CollectionViewSource (For Intra-Widget Regions)
```csharp
var view = CollectionViewSource.GetDefaultView(tasks);
masterList.ItemsSource = view;
detailPanel.DataContext = view;  // Auto-sync CurrentItem
```

---

## 2. CONTEXTUAL PANELS

### Slide-In Animation
```csharp
var slideIn = new DoubleAnimation {
    From = panel.ActualWidth, To = 0,
    Duration = TimeSpan.FromMilliseconds(200),
    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
};
var transform = new TranslateTransform();
panel.RenderTransform = transform;
transform.BeginAnimation(TranslateTransform.XProperty, slideIn);
```

### Fade Animation
```csharp
var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
element.BeginAnimation(UIElement.OpacityProperty, fade);
```

### Popup Flyout
```csharp
var popup = new Popup {
    PlacementTarget = button,
    Placement = PlacementMode.Bottom,
    StaysOpen = false,
    Child = menuContent
};
popup.IsOpen = true;
```

---

## 3. ADAPTIVE LAYOUTS

### Responsive Breakpoints
```csharp
this.SizeChanged += (s, e) => {
    if (e.NewSize.Width < 800) UseStackLayout();
    else if (e.NewSize.Width < 1200) UseTwoColumnLayout();
    else UseThreeColumnLayout();
};
```

### DataTrigger Styling
```csharp
var style = new Style(typeof(ListBoxItem));

var overdueTrigger = new DataTrigger {
    Binding = new Binding("IsOverdue"),
    Value = true
};
overdueTrigger.Setters.Add(new Setter(BackgroundProperty, 
    new SolidColorBrush(theme.Error)));
style.Triggers.Add(overdueTrigger);

listBox.ItemContainerStyle = style;
```

---

## 4. LIVE FILTERING & GROUPING

### Live Search
```csharp
var view = CollectionViewSource.GetDefaultView(tasks);
view.Filter = task => {
    var t = task as TaskItem;
    return string.IsNullOrEmpty(searchText) || 
           t.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase);
};

// Trigger refresh
searchBox.TextChanged += (s, e) => {
    searchText = searchBox.Text;
    view.Refresh();  // Instant update
};
```

### Dynamic Grouping
```csharp
var view = CollectionViewSource.GetDefaultView(tasks);
view.GroupDescriptions.Add(new PropertyGroupDescription("Status"));
view.SortDescriptions.Add(new SortDescription("Status", ListSortDirection.Ascending));

var groupStyle = new GroupStyle { HeaderTemplate = CreateGroupTemplate() };
listBox.GroupStyle.Add(groupStyle);
```

### Live Sorting
```csharp
var view = CollectionViewSource.GetDefaultView(tasks);
view.SortDescriptions.Clear();
view.SortDescriptions.Add(new SortDescription("Priority", ListSortDirection.Descending));
view.SortDescriptions.Add(new SortDescription("DueDate", ListSortDirection.Ascending));
view.Refresh();
```

---

## 5. PROGRESSIVE DISCLOSURE

### Animated Expander
```csharp
var expander = new Expander {
    Header = new TextBlock { Text = "OVERDUE (5)" },
    IsExpanded = true,
    Content = taskListBox
};

expander.Expanded += (s, e) => {
    var anim = new DoubleAnimation {
        From = 0, To = double.NaN,  // NaN = auto-size
        Duration = TimeSpan.FromMilliseconds(200)
    };
    taskListBox.BeginAnimation(HeightProperty, anim);
};
```

### Breadcrumb Navigation
```csharp
public class NavigationManager {
    private Stack<NavigationState> backStack = new Stack<NavigationState>();
    
    public void NavigateTo(string location) {
        backStack.Push(CurrentState);
        CurrentState = new NavigationState { Location = location };
    }
    
    public void GoBack() {
        if (backStack.Count > 0) CurrentState = backStack.Pop();
    }
}
```

---

## 6. MULTI-MODE VIEWS

### View Mode Switcher
```csharp
private void SetViewMode(ViewMode mode) {
    SaveViewState(currentMode);
    
    listView.Visibility = Visibility.Collapsed;
    gridView.Visibility = Visibility.Collapsed;
    kanbanView.Visibility = Visibility.Collapsed;
    
    switch (mode) {
        case ViewMode.List: listView.Visibility = Visibility.Visible; break;
        case ViewMode.Grid: gridView.Visibility = Visibility.Visible; break;
        case ViewMode.Kanban: kanbanView.Visibility = Visibility.Visible; break;
    }
    
    RestoreViewState(mode);
}
```

### State Preservation
```csharp
private void SaveViewState(ViewMode mode) {
    viewStates[mode] = new ViewState {
        SelectedItemId = (listView.SelectedItem as TaskItem)?.Id,
        ScrollOffset = GetScrollOffset(listView),
        SearchText = searchBox.Text
    };
}

private void RestoreViewState(ViewMode mode) {
    if (!viewStates.ContainsKey(mode)) return;
    var state = viewStates[mode];
    
    if (state.SelectedItemId.HasValue) {
        var item = tasks.FirstOrDefault(t => t.Id == state.SelectedItemId.Value);
        listView.SelectedItem = item;
        listView.ScrollIntoView(item);
    }
    
    searchBox.Text = state.SearchText;
}
```

---

## IMPLEMENTATION PRIORITIES

### Phase 1: Quick Wins
1. Live search (CollectionViewSource filtering)
2. DataTrigger styling (replace manual color updates)
3. Rich tooltips (detail-on-demand)
4. Fade animations (polish)

### Phase 2: Architectural
1. Unified overlay manager
2. Responsive breakpoints
3. View mode switcher
4. Breadcrumb navigation

### Phase 3: Advanced
1. Dynamic grouping/sorting
2. Timeline/calendar views
3. Drill-down TreeView
4. Advanced animations

---

## BEST PRACTICES

### DO
- Use EventBus for cross-widget coordination
- Use CollectionViewSource for intra-widget filtering/sorting
- Keep animations fast (100-250ms)
- Use DataTriggers for automatic styling
- Profile performance with large datasets
- Dispose resources in OnDispose()

### DON'T
- Don't use XAML (SuperTUI is code-behind)
- Don't use MVVM frameworks (overkill)
- Don't use CollectionViewSource for cross-widget sync
- Don't use complex/bouncy animations
- Don't hardcode colors (use ThemeManager)
- Don't call .Instance in Initialize() (use DI)

---

## QUICK CODE SNIPPETS

### Debounced Search
```csharp
DispatcherTimer searchTimer = null;
searchBox.TextChanged += (s, e) => {
    searchTimer?.Stop();
    searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
    searchTimer.Tick += (_, __) => { searchTimer.Stop(); OnSearch(searchBox.Text); };
    searchTimer.Start();
};
```

### Smooth Show/Hide
```csharp
private void FadeIn(UIElement element) {
    element.Visibility = Visibility.Visible;
    element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
}

private void FadeOut(UIElement element) {
    var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
    anim.Completed += (s, e) => element.Visibility = Visibility.Collapsed;
    element.BeginAnimation(OpacityProperty, anim);
}
```

### Virtual Scrolling
```csharp
var listBox = new ListBox {
    VirtualizingPanel.IsVirtualizing = true,
    VirtualizingPanel.VirtualizationMode = VirtualizationMode.Recycling,
    VirtualizingPanel.CacheLength = new VirtualizationCacheLength(5, 5)
};
```

---

**See Full Document:** WPF_DYNAMIC_UI_PATTERNS_RESEARCH.md (1726 lines, 53KB)
