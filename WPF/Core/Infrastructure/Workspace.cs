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

        public void HandleKeyDown(KeyEventArgs e)
        {
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
                    screen.Dispose();
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
