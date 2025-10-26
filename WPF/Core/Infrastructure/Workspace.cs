using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
    public class Workspace
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public LayoutEngine Layout { get; set; }
        public List<WidgetBase> Widgets { get; set; } = new List<WidgetBase>();
        public List<ScreenBase> Screens { get; set; } = new List<ScreenBase>();
        private List<ErrorBoundary> errorBoundaries = new List<ErrorBoundary>();

        private bool isActive = false;
        private UIElement focusedElement;
        private List<UIElement> focusableElements = new List<UIElement>();

        // Fullscreen state
        private bool isFullscreen = false;
        private WidgetBase fullscreenWidget = null;
        private ErrorBoundary fullscreenBoundary = null;
        private LayoutParams savedLayoutParams = null;
        private Grid fullscreenContainer = null;

        public Workspace(string name, int index, LayoutEngine layout)
        {
            Name = name;
            Index = index;
            Layout = layout;
        }

        public void AddWidget(WidgetBase widget, LayoutParams layoutParams)
        {
            Widgets.Add(widget);

            // Wrap widget in error boundary
            var errorBoundary = new ErrorBoundary(widget);
            errorBoundaries.Add(errorBoundary);

            // Add error boundary to layout instead of widget directly
            Layout.AddChild(errorBoundary, layoutParams);
            focusableElements.Add(widget); // Focus still goes to widget

            // Safely initialize
            errorBoundary.SafeInitialize();

            if (isActive)
                errorBoundary.SafeActivate();
        }

        public void AddScreen(ScreenBase screen, LayoutParams layoutParams)
        {
            Screens.Add(screen);
            Layout.AddChild(screen, layoutParams);
            focusableElements.Add(screen);

            if (isActive)
                screen.OnFocusReceived();
        }

        public void Activate()
        {
            isActive = true;

            // Safely activate widgets through error boundaries
            foreach (var errorBoundary in errorBoundaries)
            {
                errorBoundary.SafeActivate();
            }

            foreach (var screen in Screens)
            {
                try
                {
                    screen.OnFocusReceived();
                }
                catch (Exception ex)
                {
                    Logger.Instance?.Error("Workspace", $"Error activating screen: {ex.Message}", ex);
                }
            }

            // Focus first element if nothing focused
            if (focusedElement == null && focusableElements.Count > 0)
            {
                FocusElement(focusableElements[0]);
            }
        }

        public void Deactivate()
        {
            isActive = false;

            // Safely deactivate widgets through error boundaries
            foreach (var errorBoundary in errorBoundaries)
            {
                errorBoundary.SafeDeactivate();
            }

            foreach (var screen in Screens)
            {
                try
                {
                    screen.OnFocusLost();
                }
                catch (Exception ex)
                {
                    Logger.Instance?.Error("Workspace", $"Error deactivating screen: {ex.Message}", ex);
                }
            }

            // Don't clear focus - preserve it for when workspace reactivates
        }

        public void FocusNext()
        {
            if (focusableElements.Count == 0) return;

            int currentIndex = focusedElement != null
                ? focusableElements.IndexOf(focusedElement)
                : -1;

            int nextIndex = (currentIndex + 1) % focusableElements.Count;
            FocusElement(focusableElements[nextIndex]);
        }

        public void FocusPrevious()
        {
            if (focusableElements.Count == 0) return;

            int currentIndex = focusedElement != null
                ? focusableElements.IndexOf(focusedElement)
                : 0;

            int prevIndex = (currentIndex - 1 + focusableElements.Count) % focusableElements.Count;
            FocusElement(focusableElements[prevIndex]);
        }

        private void FocusElement(UIElement element)
        {
            // Clear previous focus
            if (focusedElement != null)
            {
                if (focusedElement is WidgetBase widget)
                    widget.HasFocus = false;
                else if (focusedElement is ScreenBase screen)
                    screen.HasFocus = false;
            }

            // Set new focus
            focusedElement = element;

            if (element is WidgetBase newWidget)
            {
                newWidget.HasFocus = true;
                newWidget.Focus();
            }
            else if (element is ScreenBase newScreen)
            {
                newScreen.HasFocus = true;
                newScreen.Focus();
            }
        }

        /// <summary>
        /// Move focus in a direction (spatial navigation with Alt+Arrow keys)
        /// </summary>
        public void FocusInDirection(FocusDirection direction)
        {
            if (focusableElements.Count <= 1)
            {
                // Only one element or none, nothing to do
                return;
            }

            // Get current focused element's position
            var currentPosition = GetElementPosition(focusedElement);
            if (!currentPosition.HasValue)
            {
                // No position info, fall back to cycling
                Logger.Instance?.Debug("Workspace", "No position info for focused element, falling back to cycle");
                if (direction == FocusDirection.Right || direction == FocusDirection.Down)
                    FocusNext();
                else
                    FocusPrevious();
                return;
            }

            var (currentRow, currentCol) = currentPosition.Value;

            // Find candidate elements in the target direction
            UIElement bestCandidate = null;
            double bestDistance = double.MaxValue;

            foreach (var element in focusableElements)
            {
                if (element == focusedElement)
                    continue;

                var position = GetElementPosition(element);
                if (!position.HasValue)
                    continue;

                var (row, col) = position.Value;

                // Check if element is in the target direction
                bool isInDirection = false;
                double distance = 0;

                switch (direction)
                {
                    case FocusDirection.Left:
                        if (col < currentCol)
                        {
                            isInDirection = true;
                            distance = (currentCol - col) + Math.Abs(currentRow - row) * 0.5;
                        }
                        break;

                    case FocusDirection.Right:
                        if (col > currentCol)
                        {
                            isInDirection = true;
                            distance = (col - currentCol) + Math.Abs(currentRow - row) * 0.5;
                        }
                        break;

                    case FocusDirection.Up:
                        if (row < currentRow)
                        {
                            isInDirection = true;
                            distance = (currentRow - row) + Math.Abs(currentCol - col) * 0.5;
                        }
                        break;

                    case FocusDirection.Down:
                        if (row > currentRow)
                        {
                            isInDirection = true;
                            distance = (row - currentRow) + Math.Abs(currentCol - col) * 0.5;
                        }
                        break;
                }

                if (isInDirection && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCandidate = element;
                }
            }

            // If no candidate found, wrap around to opposite edge
            if (bestCandidate == null)
            {
                bestCandidate = FindWrapAroundTarget(direction, currentRow, currentCol);
            }

            // Focus the best candidate
            if (bestCandidate != null)
            {
                FocusElement(bestCandidate);
                Logger.Instance?.Debug("Workspace", $"Focused element in direction {direction}");
            }
            else
            {
                Logger.Instance?.Debug("Workspace", $"No element found in direction {direction}");
            }
        }

        /// <summary>
        /// Get grid position (row, column) for an element
        /// </summary>
        private (int row, int col)? GetElementPosition(UIElement element)
        {
            if (element == null)
                return null;

            // Try to get position from layout params
            var layoutParams = Layout.GetLayoutParams(element);
            if (layoutParams != null)
            {
                if (layoutParams.Row.HasValue && layoutParams.Column.HasValue)
                {
                    return (layoutParams.Row.Value, layoutParams.Column.Value);
                }
            }

            // For DashboardLayoutEngine, try to find slot index
            if (Layout is DashboardLayoutEngine dashboard)
            {
                int slotIndex = dashboard.FindSlotIndex(element);
                if (slotIndex >= 0)
                {
                    // Map slot to (row, col): 0→(0,0), 1→(0,1), 2→(1,0), 3→(1,1)
                    int row = slotIndex / 2;
                    int col = slotIndex % 2;
                    return (row, col);
                }
            }

            // No position information available
            return null;
        }

        /// <summary>
        /// Find wrap-around target when no element exists in direction
        /// </summary>
        private UIElement FindWrapAroundTarget(FocusDirection direction, int currentRow, int currentCol)
        {
            UIElement target = null;
            double bestDistance = double.MaxValue;

            foreach (var element in focusableElements)
            {
                if (element == focusedElement)
                    continue;

                var position = GetElementPosition(element);
                if (!position.HasValue)
                    continue;

                var (row, col) = position.Value;
                double distance = 0;

                switch (direction)
                {
                    case FocusDirection.Left:
                        // Wrap to rightmost element in same or nearest row
                        distance = Math.Abs(currentRow - row) + (100 - col); // Prefer rightmost
                        break;

                    case FocusDirection.Right:
                        // Wrap to leftmost element in same or nearest row
                        distance = Math.Abs(currentRow - row) + col;
                        break;

                    case FocusDirection.Up:
                        // Wrap to bottom element in same or nearest column
                        distance = Math.Abs(currentCol - col) + (100 - row); // Prefer bottommost
                        break;

                    case FocusDirection.Down:
                        // Wrap to top element in same or nearest column
                        distance = Math.Abs(currentCol - col) + row;
                        break;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    target = element;
                }
            }

            return target;
        }

        public void HandleKeyDown(KeyEventArgs e)
        {
            // Check for directional focus using ARROW KEYS (Alt+Arrows)
            // and widget movement (Alt+Shift+Arrows)
            bool altPressed = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            bool shiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (altPressed && !e.Handled)
            {
                if (shiftPressed)
                {
                    // Widget movement: Alt+Shift+Arrow keys
                    switch (e.Key)
                    {
                        case Key.Left:
                            MoveWidgetLeft();
                            e.Handled = true;
                            return;

                        case Key.Down:
                            MoveWidgetDown();
                            e.Handled = true;
                            return;

                        case Key.Up:
                            MoveWidgetUp();
                            e.Handled = true;
                            return;

                        case Key.Right:
                            MoveWidgetRight();
                            e.Handled = true;
                            return;
                    }
                }
                else
                {
                    // Directional focus: Alt+Arrow keys
                    switch (e.Key)
                    {
                        case Key.Left:
                            FocusInDirection(FocusDirection.Left);
                            e.Handled = true;
                            return;

                        case Key.Down:
                            FocusInDirection(FocusDirection.Down);
                            e.Handled = true;
                            return;

                        case Key.Up:
                            FocusInDirection(FocusDirection.Up);
                            e.Handled = true;
                            return;

                        case Key.Right:
                            FocusInDirection(FocusDirection.Right);
                            e.Handled = true;
                            return;
                    }
                }
            }

            // Let focused element handle key first
            if (focusedElement is WidgetBase widget)
            {
                widget.OnWidgetKeyDown(e);
            }
            else if (focusedElement is ScreenBase screen)
            {
                screen.OnScreenKeyDown(e);
            }

            // Handle Tab for focus switching (if not handled by widget/screen)
            if (!e.Handled && e.Key == Key.Tab)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    FocusPrevious();
                else
                    FocusNext();

                e.Handled = true;
            }
        }

        public Panel GetContainer()
        {
            return Layout.Container;
        }

        public Dictionary<string, object> SaveState()
        {
            var state = new Dictionary<string, object>
            {
                ["Name"] = Name,
                ["Index"] = Index,
                ["Widgets"] = Widgets.Select(w => w.SaveState()).ToList(),
                ["Screens"] = Screens.Select(s => s.SaveState()).ToList()
            };
            return state;
        }

        /// <summary>
        /// Cycle focus to next widget
        /// </summary>
        public void CycleFocusForward()
        {
            if (focusableElements.Count == 0) return;

            int currentIndex = focusedElement != null ? focusableElements.IndexOf(focusedElement) : -1;
            int nextIndex = (currentIndex + 1) % focusableElements.Count;
            FocusElement(focusableElements[nextIndex]);
        }

        /// <summary>
        /// Cycle focus to previous widget
        /// </summary>
        public void CycleFocusBackward()
        {
            if (focusableElements.Count == 0) return;

            int currentIndex = focusedElement != null ? focusableElements.IndexOf(focusedElement) : -1;
            int prevIndex = (currentIndex - 1 + focusableElements.Count) % focusableElements.Count;
            FocusElement(focusableElements[prevIndex]);
        }

        /// <summary>
        /// Get currently focused widget (or null)
        /// </summary>
        public WidgetBase GetFocusedWidget()
        {
            return focusedElement as WidgetBase;
        }

        /// <summary>
        /// Remove the currently focused widget
        /// </summary>
        public void RemoveFocusedWidget()
        {
            var focused = GetFocusedWidget();
            if (focused == null) return;

            // Remove from widgets list
            Widgets.Remove(focused);

            // Remove from focusable elements
            focusableElements.Remove(focused);

            // Remove from layout
            Layout.RemoveChild(focused);

            // Find and remove error boundary
            var errorBoundary = errorBoundaries.FirstOrDefault(eb =>
                eb.Content == focused);
            if (errorBoundary != null)
            {
                errorBoundaries.Remove(errorBoundary);
                errorBoundary.SafeDispose();
            }

            // Clear focus
            focusedElement = null;

            // Focus next widget if available
            if (focusableElements.Count > 0)
            {
                FocusElement(focusableElements[0]);
            }

            Logger.Instance?.Info("Workspace", $"Removed widget: {focused.WidgetName}");
        }

        /// <summary>
        /// Move the currently focused widget in a direction
        /// Swaps positions with the widget in that direction
        /// </summary>
        public void MoveWidgetInDirection(FocusDirection direction)
        {
            var focusedWidget = GetFocusedWidget();
            if (focusedWidget == null)
            {
                Logger.Instance?.Debug("Workspace", "Cannot move widget: no widget focused");
                return;
            }

            // Find the error boundary that wraps this widget
            var focusedBoundary = errorBoundaries.FirstOrDefault(eb => eb.Content == focusedWidget);
            if (focusedBoundary == null)
            {
                Logger.Instance?.Warning("Workspace", "Cannot move widget: error boundary not found");
                return;
            }

            // Find target widget in the specified direction
            UIElement targetElement = null;

            if (Layout is GridLayoutEngine gridLayout)
            {
                targetElement = gridLayout.FindWidgetInDirection(focusedBoundary, direction);
            }
            else if (Layout is DashboardLayoutEngine dashboardLayout)
            {
                targetElement = dashboardLayout.FindWidgetInDirection(focusedBoundary, direction);
            }
            else
            {
                Logger.Instance?.Warning("Workspace", $"Widget movement not supported for layout type: {Layout?.GetType().Name}");
                return;
            }

            // If no target found, log and return
            if (targetElement == null)
            {
                Logger.Instance?.Debug("Workspace", $"No widget found in direction {direction} to swap with");
                return;
            }

            // Swap the positions
            if (Layout is GridLayoutEngine gridLayout2)
            {
                gridLayout2.SwapWidgets(focusedBoundary, targetElement);
            }
            else if (Layout is DashboardLayoutEngine dashboardLayout2)
            {
                dashboardLayout2.SwapWidgets(focusedBoundary, targetElement);
            }

            Logger.Instance?.Info("Workspace", $"Moved widget '{focusedWidget.WidgetName}' {direction}");
        }

        /// <summary>
        /// Move focused widget left (i3: $mod+Shift+h)
        /// </summary>
        public void MoveWidgetLeft() => MoveWidgetInDirection(FocusDirection.Left);

        /// <summary>
        /// Move focused widget down (i3: $mod+Shift+j)
        /// </summary>
        public void MoveWidgetDown() => MoveWidgetInDirection(FocusDirection.Down);

        /// <summary>
        /// Move focused widget up (i3: $mod+Shift+k)
        /// </summary>
        public void MoveWidgetUp() => MoveWidgetInDirection(FocusDirection.Up);

        /// <summary>
        /// Move focused widget right (i3: $mod+Shift+l)
        /// </summary>
        public void MoveWidgetRight() => MoveWidgetInDirection(FocusDirection.Right);

        /// <summary>
        /// Toggle fullscreen mode for the currently focused widget (i3: $mod+f)
        /// </summary>
        public void ToggleFullscreen()
        {
            var focused = GetFocusedWidget();

            if (!isFullscreen)
            {
                // Enter fullscreen mode
                if (focused == null)
                {
                    Logger.Instance?.Debug("Workspace", "No widget focused - cannot enter fullscreen");
                    return;
                }

                // Find the error boundary wrapping this widget
                var boundary = errorBoundaries.FirstOrDefault(eb => eb.GetWidget() == focused);
                if (boundary == null)
                {
                    Logger.Instance?.Error("Workspace", $"Could not find error boundary for widget: {focused.WidgetName}");
                    return;
                }

                // Save current layout params
                savedLayoutParams = Layout.GetLayoutParams(boundary);
                if (savedLayoutParams == null)
                {
                    Logger.Instance?.Warning("Workspace", $"No layout params found for widget: {focused.WidgetName}");
                }

                // Remove widget from current layout
                Layout.RemoveChild(boundary);

                // Create fullscreen container (single-cell grid)
                fullscreenContainer = new Grid();
                fullscreenContainer.Children.Add(boundary);

                // Add visual indicator border
                var theme = ThemeManager.Instance?.CurrentTheme ?? Theme.CreateDarkTheme();
                var border = new Border
                {
                    BorderBrush = new SolidColorBrush(theme.Primary),
                    BorderThickness = new Thickness(3),
                    Child = fullscreenContainer,
                    Margin = new Thickness(0)
                };

                // Replace layout container content
                Layout.Container.Children.Clear();
                Layout.Container.Children.Add(border);

                // Set fullscreen state
                isFullscreen = true;
                fullscreenWidget = focused;
                fullscreenBoundary = boundary;

                Logger.Instance?.Info("Workspace", $"Entered fullscreen mode for widget: {focused.WidgetName}");
            }
            else
            {
                // Exit fullscreen mode
                if (fullscreenWidget == null || fullscreenBoundary == null)
                {
                    Logger.Instance?.Error("Workspace", "Fullscreen state corrupted - resetting");
                    isFullscreen = false;
                    return;
                }

                // Remove border and fullscreen container
                Layout.Container.Children.Clear();

                // Restore all widgets to layout
                foreach (var boundary in errorBoundaries)
                {
                    var lp = Layout.GetLayoutParams(boundary);
                    if (lp != null)
                    {
                        // Re-add to layout with original params
                        if (Layout is GridLayoutEngine)
                        {
                            // For Grid, directly add to container with Grid attached properties
                            var grid = Layout.Container as Grid;
                            if (grid != null)
                            {
                                if (lp.Row.HasValue)
                                    Grid.SetRow(boundary, lp.Row.Value);
                                if (lp.Column.HasValue)
                                    Grid.SetColumn(boundary, lp.Column.Value);
                                if (lp.RowSpan.HasValue)
                                    Grid.SetRowSpan(boundary, lp.RowSpan.Value);
                                if (lp.ColumnSpan.HasValue)
                                    Grid.SetColumnSpan(boundary, lp.ColumnSpan.Value);

                                grid.Children.Add(boundary);
                            }
                        }
                        else
                        {
                            // For other layouts, use AddChild (which re-applies params)
                            Layout.Container.Children.Add(boundary);
                        }
                    }
                }

                // Clear fullscreen state
                isFullscreen = false;
                fullscreenWidget = null;
                fullscreenBoundary = null;
                savedLayoutParams = null;
                fullscreenContainer = null;

                // Restore focus to the widget
                if (focused != null)
                {
                    FocusElement(focused);
                }

                Logger.Instance?.Info("Workspace", "Exited fullscreen mode");
            }
        }

        /// <summary>
        /// Check if workspace is in fullscreen mode
        /// </summary>
        public bool IsFullscreen => isFullscreen;

        /// <summary>
        /// Exit fullscreen mode if currently in fullscreen (called on workspace switch)
        /// </summary>
        public void ExitFullscreen()
        {
            if (isFullscreen)
            {
                ToggleFullscreen();
            }
        }

        /// <summary>
        /// Set layout mode for TilingLayoutEngine (i3-style mode switching)
        /// </summary>
        /// <param name="mode">The tiling mode to set</param>
        public void SetLayoutMode(TilingMode mode)
        {
            if (Layout is TilingLayoutEngine tilingLayout)
            {
                tilingLayout.SetMode(mode);
                Logger.Instance?.Info("Workspace", $"Layout mode changed to: {mode}");
            }
            else
            {
                Logger.Instance?.Warning("Workspace", $"Cannot set layout mode: Layout is {Layout?.GetType().Name ?? "null"}, not TilingLayoutEngine");
            }
        }

        /// <summary>
        /// Get current layout mode (returns null if not using TilingLayoutEngine)
        /// </summary>
        /// <returns>Current TilingMode or null</returns>
        public TilingMode? GetLayoutMode()
        {
            if (Layout is TilingLayoutEngine tilingLayout)
            {
                return tilingLayout.Mode;
            }
            return null;
        }

        /// <summary>
        /// Focus a specific widget (used by QuickJumpOverlay and external focus management)
        /// </summary>
        /// <param name="widget">Widget to focus</param>
        public void FocusWidget(WidgetBase widget)
        {
            if (widget == null) return;
            FocusElement(widget);
        }

        /// <summary>
        /// Property to get currently focused widget (PowerShell-friendly)
        /// </summary>
        public WidgetBase FocusedWidget => GetFocusedWidget();

        /// <summary>
        /// Get all widgets in this workspace (used by QuickJumpOverlay)
        /// </summary>
        /// <returns>Read-only collection of widgets</returns>
        public IEnumerable<WidgetBase> GetAllWidgets()
        {
            return Widgets.AsReadOnly();
        }

        /// <summary>
        /// Dispose of workspace and all its widgets
        /// </summary>
        public void Dispose()
        {
            Logger.Instance?.Info("Workspace", $"Disposing workspace: {Name}");

            // Safely dispose all widgets through error boundaries
            foreach (var errorBoundary in errorBoundaries.ToList())
            {
                errorBoundary.SafeDispose();
            }

            // Dispose all screens
            foreach (var screen in Screens.ToList())
            {
                try
                {
                    if (screen is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance?.Error("Workspace", $"Error disposing screen: {ex.Message}", ex);
                }
            }

            // Clear collections
            Widgets.Clear();
            Screens.Clear();
            errorBoundaries.Clear();
            focusableElements.Clear();
            focusedElement = null;

            // Clear layout
            Layout?.Clear();
        }
    }
}
