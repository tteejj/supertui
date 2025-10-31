using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Manages focus history across the application for perfect focus restoration
    /// Tracks what control had focus in each pane and maintains a history stack
    /// </summary>
    public class FocusHistoryManager
    {
        private readonly ILogger logger;
        private readonly Stack<FocusRecord> focusHistory = new Stack<FocusRecord>();
        private readonly Dictionary<string, FocusRecord> paneFocusMap = new Dictionary<string, FocusRecord>();
        private readonly int maxHistorySize = 50;
        private bool isTrackingEnabled = true;
        private FocusRecord currentFocus;

        public event EventHandler<FocusChangedEventArgs> FocusChanged;

        public FocusHistoryManager(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Hook into global focus changes
            EventManager.RegisterClassHandler(typeof(UIElement),
                UIElement.GotFocusEvent,
                new RoutedEventHandler(OnElementGotFocus));

            EventManager.RegisterClassHandler(typeof(UIElement),
                UIElement.LostFocusEvent,
                new RoutedEventHandler(OnElementLostFocus));
        }

        /// <summary>
        /// Record that a control has gained focus
        /// </summary>
        public void RecordFocus(UIElement element, string paneId = null)
        {
            if (!isTrackingEnabled || element == null) return;

            var record = new FocusRecord
            {
                Element = new WeakReference(element),
                ElementType = element.GetType().Name,
                PaneId = paneId ?? GetPaneId(element),
                Timestamp = DateTime.Now,
                ControlState = CaptureControlState(element)
            };

            // Update current focus
            currentFocus = record;

            // Add to history
            if (focusHistory.Count >= maxHistorySize)
            {
                // Remove oldest entries
                var temp = focusHistory.ToArray().Take(maxHistorySize - 1).Reverse();
                focusHistory.Clear();
                foreach (var item in temp)
                    focusHistory.Push(item);
            }
            focusHistory.Push(record);

            // Update pane-specific focus map
            if (!string.IsNullOrEmpty(record.PaneId))
            {
                paneFocusMap[record.PaneId] = record;
            }

            logger.Log(LogLevel.Debug, "FocusHistory",
                $"Focus recorded: {record.ElementType} in {record.PaneId}");

            FocusChanged?.Invoke(this, new FocusChangedEventArgs(record.ElementType, record.PaneId, record.Timestamp));
        }

        /// <summary>
        /// Get the last focused control for a specific pane
        /// </summary>
        public UIElement GetLastFocusedControl(string paneId)
        {
            if (string.IsNullOrEmpty(paneId)) return null;

            if (paneFocusMap.TryGetValue(paneId, out var record))
            {
                if (record.Element.IsAlive)
                {
                    return record.Element.Target as UIElement;
                }
            }

            return null;
        }

        /// <summary>
        /// Restore focus to the last focused control in a pane
        /// </summary>
        public bool RestorePaneFocus(string paneId)
        {
            var element = GetLastFocusedControl(paneId);
            if (element == null) return false;

            try
            {
                element.Focus();
                Keyboard.Focus(element);

                // Restore control state if possible
                if (paneFocusMap.TryGetValue(paneId, out var record))
                {
                    RestoreControlState(element, record.ControlState);
                }

                logger.Log(LogLevel.Debug, "FocusHistory",
                    $"Focus restored to {element.GetType().Name} in {paneId}");
                return true;
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, "FocusHistory",
                    $"Failed to restore focus: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Go back in focus history
        /// </summary>
        public bool NavigateBack()
        {
            if (focusHistory.Count <= 1) return false;

            // Pop current focus
            focusHistory.Pop();

            // Get previous focus
            if (focusHistory.TryPeek(out var previous))
            {
                if (previous.Element.IsAlive)
                {
                    var element = previous.Element.Target as UIElement;
                    element?.Focus();
                    Keyboard.Focus(element);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Clear focus history for a specific pane
        /// </summary>
        public void ClearPaneHistory(string paneId)
        {
            if (string.IsNullOrEmpty(paneId)) return;

            paneFocusMap.Remove(paneId);

            // Remove from history stack
            var newHistory = focusHistory.Where(r => r.PaneId != paneId).Reverse();
            focusHistory.Clear();
            foreach (var item in newHistory)
                focusHistory.Push(item);
        }

        /// <summary>
        /// Save focus state for workspace switching
        /// </summary>
        public Dictionary<string, object> SaveWorkspaceState()
        {
            var state = new Dictionary<string, object>();

            foreach (var kvp in paneFocusMap)
            {
                if (kvp.Value.Element.IsAlive)
                {
                    state[$"Focus_{kvp.Key}"] = new
                    {
                        ElementType = kvp.Value.ElementType,
                        ControlState = kvp.Value.ControlState,
                        Timestamp = kvp.Value.Timestamp
                    };
                }
            }

            state["CurrentFocus"] = currentFocus?.PaneId;
            state["HistoryCount"] = focusHistory.Count;

            return state;
        }

        /// <summary>
        /// Restore focus state after workspace switch
        /// </summary>
        public void RestoreWorkspaceState(Dictionary<string, object> state)
        {
            if (state == null) return;

            // Clear current state
            focusHistory.Clear();
            paneFocusMap.Clear();

            // Restore current focus pane
            if (state.TryGetValue("CurrentFocus", out var currentPaneId))
            {
                // This will be restored when the pane is ready
                logger.Log(LogLevel.Debug, "FocusHistory",
                    $"Workspace focus will be restored to pane: {currentPaneId}");
            }
        }

        /// <summary>
        /// Temporarily disable focus tracking (useful during bulk UI updates)
        /// </summary>
        public IDisposable SuspendTracking()
        {
            isTrackingEnabled = false;
            return new TrackingResumer(() => isTrackingEnabled = true);
        }

        #region Private Methods

        private void OnElementGotFocus(object sender, RoutedEventArgs e)
        {
            if (!isTrackingEnabled) return;

            var element = e.OriginalSource as UIElement;
            if (element != null && ShouldTrackElement(element))
            {
                RecordFocus(element);
            }
        }

        private void OnElementLostFocus(object sender, RoutedEventArgs e)
        {
            // Can be used to save state before focus leaves
        }

        private bool ShouldTrackElement(UIElement element)
        {
            // Only track meaningful controls
            return element is TextBox ||
                   element is ListBox ||
                   element is ListBoxItem ||
                   element is TreeView ||
                   element is TreeViewItem ||
                   element is ComboBox ||
                   element is Button ||
                   element is CheckBox ||
                   element is RadioButton;
        }

        private string GetPaneId(UIElement element)
        {
            // Walk up the visual tree to find the containing pane
            DependencyObject current = element;
            while (current != null)
            {
                if (current is Components.PaneBase pane)
                {
                    return pane.PaneName;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return "Unknown";
        }

        private ControlState CaptureControlState(UIElement element)
        {
            var state = new ControlState();

            switch (element)
            {
                case TextBox textBox:
                    state.Text = textBox.Text;
                    state.CaretIndex = textBox.CaretIndex;
                    state.SelectionStart = textBox.SelectionStart;
                    state.SelectionLength = textBox.SelectionLength;
                    break;

                case ListBox listBox:
                    state.SelectedIndex = listBox.SelectedIndex;
                    state.ScrollPosition = GetScrollPosition(listBox);
                    break;

                case TreeView treeView:
                    state.SelectedItemPath = GetTreeViewPath(treeView.SelectedItem);
                    break;

                case ComboBox comboBox:
                    state.SelectedIndex = comboBox.SelectedIndex;
                    state.Text = comboBox.Text;
                    break;
            }

            return state;
        }

        private void RestoreControlState(UIElement element, ControlState state)
        {
            if (state == null) return;

            switch (element)
            {
                case TextBox textBox:
                    if (state.Text != null)
                        textBox.Text = state.Text;
                    if (state.CaretIndex.HasValue)
                        textBox.CaretIndex = state.CaretIndex.Value;
                    if (state.SelectionStart.HasValue)
                        textBox.SelectionStart = state.SelectionStart.Value;
                    if (state.SelectionLength.HasValue)
                        textBox.SelectionLength = state.SelectionLength.Value;
                    break;

                case ListBox listBox:
                    if (state.SelectedIndex.HasValue && state.SelectedIndex.Value < listBox.Items.Count)
                        listBox.SelectedIndex = state.SelectedIndex.Value;
                    if (state.ScrollPosition.HasValue)
                        SetScrollPosition(listBox, state.ScrollPosition.Value);
                    break;

                case ComboBox comboBox:
                    if (state.SelectedIndex.HasValue && state.SelectedIndex.Value < comboBox.Items.Count)
                        comboBox.SelectedIndex = state.SelectedIndex.Value;
                    break;
            }
        }

        private double? GetScrollPosition(ListBox listBox)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(listBox);
            return scrollViewer?.VerticalOffset;
        }

        private void SetScrollPosition(ListBox listBox, double position)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(listBox);
            scrollViewer?.ScrollToVerticalOffset(position);
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private string GetTreeViewPath(object selectedItem)
        {
            // Implementation depends on tree structure
            return selectedItem?.ToString();
        }

        #endregion

        #region Nested Types

        private class FocusRecord
        {
            public WeakReference Element { get; set; }
            public string ElementType { get; set; }
            public string PaneId { get; set; }
            public DateTime Timestamp { get; set; }
            public ControlState ControlState { get; set; }
        }

        private class ControlState
        {
            public string Text { get; set; }
            public int? CaretIndex { get; set; }
            public int? SelectionStart { get; set; }
            public int? SelectionLength { get; set; }
            public int? SelectedIndex { get; set; }
            public double? ScrollPosition { get; set; }
            public string SelectedItemPath { get; set; }
        }

        public class FocusChangedEventArgs : EventArgs
        {
            public string ElementType { get; }
            public string PaneId { get; }
            public DateTime Timestamp { get; }

            public FocusChangedEventArgs(string elementType, string paneId, DateTime timestamp)
            {
                ElementType = elementType;
                PaneId = paneId;
                Timestamp = timestamp;
            }
        }

        private class TrackingResumer : IDisposable
        {
            private readonly Action resumeAction;

            public TrackingResumer(Action resumeAction)
            {
                this.resumeAction = resumeAction;
            }

            public void Dispose()
            {
                resumeAction?.Invoke();
            }
        }

        #endregion
    }
}