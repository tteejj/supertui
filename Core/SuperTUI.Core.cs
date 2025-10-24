// SuperTUI.Core.cs - Core C# Engine for PowerShell TUI Framework
// Compile via: Add-Type -TypeDefinition (Get-Content Core/SuperTUI.Core.cs -Raw) -Language CSharp
//
// Design Philosophy:
// - Infrastructure in C#, Logic in PowerShell
// - Declarative over Imperative
// - Convention over Configuration

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace SuperTUI
{
    #region Core Enums and Structs

    /// <summary>
    /// Represents an RGB color with 24-bit depth
    /// </summary>
    public struct Color
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public Color(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static Color FromRgb(byte r, byte g, byte b) => new Color(r, g, b);

        // Common colors
        public static Color Black => new Color(0, 0, 0);
        public static Color White => new Color(255, 255, 255);
        public static Color Red => new Color(255, 0, 0);
        public static Color Green => new Color(0, 255, 0);
        public static Color Blue => new Color(0, 0, 255);
        public static Color Yellow => new Color(255, 255, 0);
        public static Color Cyan => new Color(0, 255, 255);
        public static Color Magenta => new Color(255, 0, 255);
        public static Color Gray => new Color(128, 128, 128);
        public static Color DarkGray => new Color(64, 64, 64);
        public static Color LightGray => new Color(192, 192, 192);
    }

    /// <summary>
    /// Layout orientation for stack layouts
    /// </summary>
    public enum Orientation
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Dock position for dock layouts
    /// </summary>
    public enum Dock
    {
        Top,
        Bottom,
        Left,
        Right,
        Fill
    }

    /// <summary>
    /// Text alignment options
    /// </summary>
    public enum TextAlignment
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// Defines row height in grid layout
    /// </summary>
    public class RowDefinition
    {
        public string Height { get; set; } // "Auto", "*", or pixel value
        public int CalculatedHeight { get; set; }

        public RowDefinition(string height)
        {
            Height = height;
        }
    }

    /// <summary>
    /// Defines column width in grid layout
    /// </summary>
    public class ColumnDefinition
    {
        public string Width { get; set; } // "Auto", "*", or pixel value
        public int CalculatedWidth { get; set; }

        public ColumnDefinition(string width)
        {
            Width = width;
        }
    }

    /// <summary>
    /// Rendering context with theme and dimensions
    /// </summary>
    public class RenderContext
    {
        public Theme Theme { get; set; }
        public int TerminalWidth { get; set; }
        public int TerminalHeight { get; set; }
        public bool IsFocused { get; set; }

        public RenderContext(Theme theme, int width, int height)
        {
            Theme = theme;
            TerminalWidth = width;
            TerminalHeight = height;
        }
    }

    /// <summary>
    /// Rectangle for bounds calculations
    /// </summary>
    public struct Rectangle
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Rectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    #endregion

    #region Base Classes

    /// <summary>
    /// Base class for all UI elements in SuperTUI
    /// Implements INotifyPropertyChanged for data binding support
    /// </summary>
    public abstract class UIElement : INotifyPropertyChanged
    {
        private int _x;
        private int _y;
        private int _width;
        private int _height;
        private bool _visible = true;
        private bool _isFocused;
        private bool _isDirty = true;

        // Position and Size
        public int X
        {
            get => _x;
            set { _x = value; OnPropertyChanged(nameof(X)); }
        }

        public int Y
        {
            get => _y;
            set { _y = value; OnPropertyChanged(nameof(Y)); }
        }

        public int Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(nameof(Width)); Invalidate(); }
        }

        public int Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(nameof(Height)); Invalidate(); }
        }

        // Visibility and Focus
        public bool Visible
        {
            get => _visible;
            set { _visible = value; OnPropertyChanged(nameof(Visible)); Invalidate(); }
        }

        public bool CanFocus { get; set; }

        public bool IsFocused
        {
            get => _isFocused;
            set
            {
                if (_isFocused != value)
                {
                    _isFocused = value;
                    OnPropertyChanged(nameof(IsFocused));
                    Invalidate();
                }
            }
        }

        // Rendering state
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                _isDirty = value;
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        // Parent relationship
        public UIElement Parent { get; set; }

        /// <summary>
        /// Mark this element as needing re-render
        /// </summary>
        public void Invalidate()
        {
            IsDirty = true;
            Parent?.Invalidate();
        }

        /// <summary>
        /// Clear the dirty flag after rendering
        /// </summary>
        public void ClearDirty()
        {
            IsDirty = false;
        }

        /// <summary>
        /// Render this element to a string
        /// </summary>
        public abstract string Render(RenderContext context);

        /// <summary>
        /// Handle keyboard input
        /// </summary>
        public virtual bool HandleKey(ConsoleKeyInfo key)
        {
            return false;
        }

        /// <summary>
        /// Measure the desired size of this element
        /// </summary>
        public virtual Rectangle Measure(int availableWidth, int availableHeight)
        {
            return new Rectangle(0, 0, Width, Height);
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Base class for interactive components
    /// </summary>
    public abstract class Component : UIElement
    {
        public Component()
        {
            CanFocus = false;
        }
    }

    /// <summary>
    /// Base class for full-screen views
    /// </summary>
    public abstract class Screen : UIElement
    {
        private string _title;

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
                Invalidate();
            }
        }

        public List<UIElement> Children { get; }
        public Dictionary<string, Action<object>> KeyBindings { get; }

        public Screen()
        {
            Children = new List<UIElement>();
            KeyBindings = new Dictionary<string, Action<object>>();
            CanFocus = false;
        }

        /// <summary>
        /// Called when screen becomes active
        /// </summary>
        public virtual void OnActivate() { }

        /// <summary>
        /// Called when screen becomes inactive
        /// </summary>
        public virtual void OnDeactivate() { }

        /// <summary>
        /// Called when terminal is resized
        /// </summary>
        public virtual void OnResize(int width, int height) { }

        /// <summary>
        /// Register a key binding
        /// </summary>
        public void RegisterKey(string keyString, Action<object> action)
        {
            KeyBindings[keyString] = action;
        }

        /// <summary>
        /// Handle keyboard input
        /// </summary>
        public override bool HandleKey(ConsoleKeyInfo key)
        {
            string keyStr = KeyToString(key);
            if (KeyBindings.ContainsKey(keyStr))
            {
                KeyBindings[keyStr]?.Invoke(null);
                return true;
            }

            // Pass to focused child
            var focusedChild = FindFocusedChild();
            if (focusedChild != null && focusedChild.HandleKey(key))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Render screen and all children
        /// </summary>
        public override string Render(RenderContext context)
        {
            var sb = new StringBuilder();

            foreach (var child in Children)
            {
                if (child.Visible)
                {
                    sb.Append(child.Render(context));
                    child.ClearDirty();
                }
            }

            ClearDirty();
            return sb.ToString();
        }

        /// <summary>
        /// Find the currently focused child element
        /// </summary>
        private UIElement FindFocusedChild()
        {
            foreach (var child in Children)
            {
                if (child.IsFocused)
                    return child;

                // Recursively search layouts
                if (child is GridLayout grid)
                {
                    var focused = grid.FindFocusedElement();
                    if (focused != null) return focused;
                }
                else if (child is StackLayout stack)
                {
                    var focused = stack.FindFocusedElement();
                    if (focused != null) return focused;
                }
            }
            return null;
        }

        /// <summary>
        /// Convert ConsoleKeyInfo to string representation
        /// </summary>
        private string KeyToString(ConsoleKeyInfo key)
        {
            var sb = new StringBuilder();

            if ((key.Modifiers & ConsoleModifiers.Control) != 0)
                sb.Append("Ctrl+");
            if ((key.Modifiers & ConsoleModifiers.Alt) != 0)
                sb.Append("Alt+");
            if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
                sb.Append("Shift+");

            sb.Append(key.Key.ToString());

            return sb.ToString();
        }
    }

    /// <summary>
    /// Dialog screen that can be shown modally
    /// </summary>
    public class DialogScreen : Screen
    {
        public bool ShowBorder { get; set; } = true;
        public string BorderTitle { get; set; }

        public DialogScreen()
        {
            BorderTitle = Title;
        }
    }

    #endregion

    #region Layout Classes

    /// <summary>
    /// CSS Grid-inspired layout with rows and columns
    /// </summary>
    public class GridLayout : UIElement
    {
        private class GridChild
        {
            public UIElement Element { get; set; }
            public int Row { get; set; }
            public int Column { get; set; }
            public int RowSpan { get; set; }
            public int ColumnSpan { get; set; }
        }

        public List<RowDefinition> Rows { get; }
        public List<ColumnDefinition> Columns { get; }
        private List<GridChild> _children;

        public GridLayout()
        {
            Rows = new List<RowDefinition>();
            Columns = new List<ColumnDefinition>();
            _children = new List<GridChild>();
        }

        /// <summary>
        /// Add a child element to a specific grid cell
        /// </summary>
        public void AddChild(UIElement element, int row, int col, int rowSpan = 1, int colSpan = 1)
        {
            _children.Add(new GridChild
            {
                Element = element,
                Row = row,
                Column = col,
                RowSpan = rowSpan,
                ColumnSpan = colSpan
            });

            element.Parent = this;
            element.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsDirty) && element.IsDirty)
                {
                    Invalidate();
                }
            };

            Invalidate();
        }

        /// <summary>
        /// Calculate layout and position children
        /// </summary>
        private void CalculateLayout()
        {
            if (Rows.Count == 0 || Columns.Count == 0) return;

            // Calculate row heights
            int[] rowHeights = new int[Rows.Count];
            int fixedHeight = 0;
            int autoRows = 0;
            int starRows = 0;

            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Height == "*")
                {
                    starRows++;
                }
                else if (Rows[i].Height == "Auto")
                {
                    autoRows++;
                    rowHeights[i] = MeasureRowHeight(i);
                    fixedHeight += rowHeights[i];
                }
                else if (int.TryParse(Rows[i].Height, out int height))
                {
                    rowHeights[i] = height;
                    fixedHeight += height;
                }
            }

            // Distribute remaining height to star rows
            int remainingHeight = Math.Max(0, Height - fixedHeight);
            int starHeight = starRows > 0 ? remainingHeight / starRows : 0;
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Height == "*")
                {
                    rowHeights[i] = starHeight;
                }
                Rows[i].CalculatedHeight = rowHeights[i];
            }

            // Calculate column widths (same logic)
            int[] colWidths = new int[Columns.Count];
            int fixedWidth = 0;
            int autoColumns = 0;
            int starColumns = 0;

            for (int i = 0; i < Columns.Count; i++)
            {
                if (Columns[i].Width == "*")
                {
                    starColumns++;
                }
                else if (Columns[i].Width == "Auto")
                {
                    autoColumns++;
                    colWidths[i] = MeasureColumnWidth(i);
                    fixedWidth += colWidths[i];
                }
                else if (int.TryParse(Columns[i].Width, out int width))
                {
                    colWidths[i] = width;
                    fixedWidth += width;
                }
            }

            int remainingWidth = Math.Max(0, Width - fixedWidth);
            int starWidth = starColumns > 0 ? remainingWidth / starColumns : 0;
            for (int i = 0; i < Columns.Count; i++)
            {
                if (Columns[i].Width == "*")
                {
                    colWidths[i] = starWidth;
                }
                Columns[i].CalculatedWidth = colWidths[i];
            }

            // Position all children
            foreach (var child in _children)
            {
                var bounds = GetCellBounds(child.Row, child.Column, child.RowSpan, child.ColumnSpan, rowHeights, colWidths);
                child.Element.X = X + bounds.X;
                child.Element.Y = Y + bounds.Y;
                child.Element.Width = bounds.Width;
                child.Element.Height = bounds.Height;
            }
        }

        /// <summary>
        /// Get bounds for a cell span
        /// </summary>
        private Rectangle GetCellBounds(int row, int col, int rowSpan, int colSpan, int[] rowHeights, int[] colWidths)
        {
            int x = 0;
            for (int i = 0; i < col && i < colWidths.Length; i++)
            {
                x += colWidths[i];
            }

            int y = 0;
            for (int i = 0; i < row && i < rowHeights.Length; i++)
            {
                y += rowHeights[i];
            }

            int width = 0;
            for (int i = col; i < col + colSpan && i < colWidths.Length; i++)
            {
                width += colWidths[i];
            }

            int height = 0;
            for (int i = row; i < row + rowSpan && i < rowHeights.Length; i++)
            {
                height += rowHeights[i];
            }

            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// Measure content height for Auto row
        /// </summary>
        private int MeasureRowHeight(int row)
        {
            int maxHeight = 1;
            foreach (var child in _children.Where(c => c.Row == row))
            {
                var measured = child.Element.Measure(Width, Height);
                maxHeight = Math.Max(maxHeight, measured.Height);
            }
            return maxHeight;
        }

        /// <summary>
        /// Measure content width for Auto column
        /// </summary>
        private int MeasureColumnWidth(int col)
        {
            int maxWidth = 1;
            foreach (var child in _children.Where(c => c.Column == col))
            {
                var measured = child.Element.Measure(Width, Height);
                maxWidth = Math.Max(maxWidth, measured.Width);
            }
            return maxWidth;
        }

        /// <summary>
        /// Find focused element in this layout
        /// </summary>
        public UIElement FindFocusedElement()
        {
            foreach (var child in _children)
            {
                if (child.Element.IsFocused)
                    return child.Element;

                // Recursively search nested layouts
                if (child.Element is GridLayout grid)
                {
                    var focused = grid.FindFocusedElement();
                    if (focused != null) return focused;
                }
                else if (child.Element is StackLayout stack)
                {
                    var focused = stack.FindFocusedElement();
                    if (focused != null) return focused;
                }
            }
            return null;
        }

        public override string Render(RenderContext context)
        {
            CalculateLayout();

            var sb = new StringBuilder();
            foreach (var child in _children)
            {
                if (child.Element.Visible)
                {
                    sb.Append(child.Element.Render(context));
                    child.Element.ClearDirty();
                }
            }

            ClearDirty();
            return sb.ToString();
        }
    }

    /// <summary>
    /// Stack layout - arranges children horizontally or vertically
    /// </summary>
    public class StackLayout : UIElement
    {
        public Orientation Orientation { get; set; }
        public int Spacing { get; set; }
        public List<UIElement> Children { get; }

        public StackLayout()
        {
            Children = new List<UIElement>();
            Orientation = Orientation.Vertical;
            Spacing = 0;
        }

        public void AddChild(UIElement element)
        {
            Children.Add(element);
            element.Parent = this;
            element.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsDirty) && element.IsDirty)
                {
                    Invalidate();
                }
            };
            Invalidate();
        }

        private void CalculateLayout()
        {
            if (Orientation == Orientation.Vertical)
            {
                int currentY = Y;
                foreach (var child in Children.Where(c => c.Visible))
                {
                    child.X = X;
                    child.Y = currentY;
                    child.Width = Width;
                    // Keep child's height or measure it
                    currentY += child.Height + Spacing;
                }
            }
            else // Horizontal
            {
                int currentX = X;
                foreach (var child in Children.Where(c => c.Visible))
                {
                    child.X = currentX;
                    child.Y = Y;
                    child.Height = Height;
                    // Keep child's width or measure it
                    currentX += child.Width + Spacing;
                }
            }
        }

        public UIElement FindFocusedElement()
        {
            foreach (var child in Children)
            {
                if (child.IsFocused)
                    return child;

                if (child is GridLayout grid)
                {
                    var focused = grid.FindFocusedElement();
                    if (focused != null) return focused;
                }
                else if (child is StackLayout stack)
                {
                    var focused = stack.FindFocusedElement();
                    if (focused != null) return focused;
                }
            }
            return null;
        }

        public override string Render(RenderContext context)
        {
            CalculateLayout();

            var sb = new StringBuilder();
            foreach (var child in Children)
            {
                if (child.Visible)
                {
                    sb.Append(child.Render(context));
                    child.ClearDirty();
                }
            }

            ClearDirty();
            return sb.ToString();
        }
    }

    /// <summary>
    /// Dock layout - docks children to edges
    /// </summary>
    public class DockLayout : UIElement
    {
        private class DockChild
        {
            public UIElement Element { get; set; }
            public Dock Position { get; set; }
        }

        private List<DockChild> _children;

        public DockLayout()
        {
            _children = new List<DockChild>();
        }

        public void AddChild(UIElement element, Dock position)
        {
            _children.Add(new DockChild { Element = element, Position = position });
            element.Parent = this;
            element.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsDirty) && element.IsDirty)
                {
                    Invalidate();
                }
            };
            Invalidate();
        }

        private void CalculateLayout()
        {
            int left = X;
            int top = Y;
            int right = X + Width;
            int bottom = Y + Height;

            foreach (var child in _children.Where(c => c.Element.Visible))
            {
                switch (child.Position)
                {
                    case Dock.Top:
                        child.Element.X = left;
                        child.Element.Y = top;
                        child.Element.Width = right - left;
                        top += child.Element.Height;
                        break;

                    case Dock.Bottom:
                        bottom -= child.Element.Height;
                        child.Element.X = left;
                        child.Element.Y = bottom;
                        child.Element.Width = right - left;
                        break;

                    case Dock.Left:
                        child.Element.X = left;
                        child.Element.Y = top;
                        child.Element.Height = bottom - top;
                        left += child.Element.Width;
                        break;

                    case Dock.Right:
                        right -= child.Element.Width;
                        child.Element.X = right;
                        child.Element.Y = top;
                        child.Element.Height = bottom - top;
                        break;

                    case Dock.Fill:
                        child.Element.X = left;
                        child.Element.Y = top;
                        child.Element.Width = right - left;
                        child.Element.Height = bottom - top;
                        break;
                }
            }
        }

        public override string Render(RenderContext context)
        {
            CalculateLayout();

            var sb = new StringBuilder();
            foreach (var child in _children)
            {
                if (child.Element.Visible)
                {
                    sb.Append(child.Element.Render(context));
                    child.Element.ClearDirty();
                }
            }

            ClearDirty();
            return sb.ToString();
        }
    }

    #endregion

    #region Components

    /// <summary>
    /// Label component for displaying text
    /// </summary>
    public class Label : Component
    {
        private string _text;
        private TextAlignment _textAlignment;
        private string _style;

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged(nameof(Text));
                Invalidate();
            }
        }

        public TextAlignment Alignment
        {
            get => _textAlignment;
            set
            {
                _textAlignment = value;
                OnPropertyChanged(nameof(Alignment));
                Invalidate();
            }
        }

        public string Style
        {
            get => _style;
            set
            {
                _style = value;
                OnPropertyChanged(nameof(Style));
                Invalidate();
            }
        }

        public Label()
        {
            Alignment = TextAlignment.Left;
            Height = 1;
        }

        public override Rectangle Measure(int availableWidth, int availableHeight)
        {
            int width = string.IsNullOrEmpty(Text) ? 0 : Text.Length;
            return new Rectangle(0, 0, width, 1);
        }

        public override string Render(RenderContext context)
        {
            if (string.IsNullOrEmpty(Text) || Width == 0)
                return string.Empty;

            var sb = new StringBuilder();
            Color fg = context.Theme.Foreground;
            Color bg = context.Theme.Background;

            // Apply style if specified
            if (!string.IsNullOrEmpty(Style))
            {
                var styleColors = context.Theme.GetStyle(Style);
                if (styleColors != null)
                {
                    fg = styleColors.Item1;
                    bg = styleColors.Item2;
                }
            }

            string displayText = Text ?? string.Empty;
            if (displayText.Length > Width)
            {
                displayText = displayText.Substring(0, Width);
            }

            // Apply alignment
            string alignedText = AlignText(displayText, Width, Alignment);

            sb.Append(VT.MoveTo(X, Y));
            sb.Append(VT.RGB(fg.R, fg.G, fg.B));
            sb.Append(alignedText);
            sb.Append(VT.Reset());

            return sb.ToString();
        }

        private string AlignText(string text, int width, TextAlignment alignment)
        {
            if (text.Length >= width)
                return text;

            switch (alignment)
            {
                case TextAlignment.Left:
                    return text.PadRight(width);
                case TextAlignment.Right:
                    return text.PadLeft(width);
                case TextAlignment.Center:
                    int leftPadding = (width - text.Length) / 2;
                    return text.PadLeft(leftPadding + text.Length).PadRight(width);
                default:
                    return text;
            }
        }
    }

    /// <summary>
    /// Button component for user interaction
    /// </summary>
    public class Button : Component
    {
        private string _label;
        private bool _isDefault;

        public string Label
        {
            get => _label;
            set
            {
                _label = value;
                OnPropertyChanged(nameof(Label));
                Invalidate();
            }
        }

        public bool IsDefault
        {
            get => _isDefault;
            set
            {
                _isDefault = value;
                OnPropertyChanged(nameof(IsDefault));
                Invalidate();
            }
        }

        public event EventHandler Click;

        public Button()
        {
            CanFocus = true;
            Height = 1;
        }

        public override Rectangle Measure(int availableWidth, int availableHeight)
        {
            int width = string.IsNullOrEmpty(Label) ? 0 : Label.Length + 4; // Add padding
            return new Rectangle(0, 0, width, 1);
        }

        public override bool HandleKey(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Spacebar)
            {
                Click?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        public override string Render(RenderContext context)
        {
            var sb = new StringBuilder();
            Color fg = IsFocused ? context.Theme.Focus : context.Theme.Foreground;
            Color bg = context.Theme.Background;

            if (IsDefault)
            {
                fg = context.Theme.Primary;
            }

            string displayText = $"[ {Label ?? string.Empty} ]";
            if (displayText.Length > Width)
            {
                displayText = displayText.Substring(0, Width);
            }

            sb.Append(VT.MoveTo(X, Y));
            if (IsFocused)
            {
                sb.Append(VT.Bold());
            }
            sb.Append(VT.RGB(fg.R, fg.G, fg.B));
            sb.Append(displayText);
            sb.Append(VT.Reset());

            return sb.ToString();
        }
    }

    /// <summary>
    /// TextBox component for single-line text input
    /// </summary>
    public class TextBox : Component
    {
        private string _value;
        private string _placeholder;
        private int _maxLength;
        private bool _isReadOnly;
        private int _cursorPosition;

        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
                Invalidate();
            }
        }

        public string Placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value;
                OnPropertyChanged(nameof(Placeholder));
                Invalidate();
            }
        }

        public int MaxLength
        {
            get => _maxLength;
            set
            {
                _maxLength = value;
                OnPropertyChanged(nameof(MaxLength));
            }
        }

        public bool IsReadOnly
        {
            get => _isReadOnly;
            set
            {
                _isReadOnly = value;
                OnPropertyChanged(nameof(IsReadOnly));
                Invalidate();
            }
        }

        public TextBox()
        {
            CanFocus = true;
            Height = 1;
            Value = string.Empty;
            MaxLength = 0;
            _cursorPosition = 0;
        }

        public override bool HandleKey(ConsoleKeyInfo key)
        {
            if (IsReadOnly)
                return false;

            string currentValue = Value ?? string.Empty;

            switch (key.Key)
            {
                case ConsoleKey.Backspace:
                    if (_cursorPosition > 0)
                    {
                        Value = currentValue.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                    }
                    return true;

                case ConsoleKey.Delete:
                    if (_cursorPosition < currentValue.Length)
                    {
                        Value = currentValue.Remove(_cursorPosition, 1);
                    }
                    return true;

                case ConsoleKey.LeftArrow:
                    if (_cursorPosition > 0)
                        _cursorPosition--;
                    return true;

                case ConsoleKey.RightArrow:
                    if (_cursorPosition < currentValue.Length)
                        _cursorPosition++;
                    return true;

                case ConsoleKey.Home:
                    _cursorPosition = 0;
                    return true;

                case ConsoleKey.End:
                    _cursorPosition = currentValue.Length;
                    return true;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        if (MaxLength == 0 || currentValue.Length < MaxLength)
                        {
                            Value = currentValue.Insert(_cursorPosition, key.KeyChar.ToString());
                            _cursorPosition++;
                        }
                        return true;
                    }
                    break;
            }

            return false;
        }

        public override string Render(RenderContext context)
        {
            var sb = new StringBuilder();
            Color fg = IsFocused ? context.Theme.Focus : context.Theme.Foreground;
            Color bg = context.Theme.Background;
            Color borderColor = IsFocused ? context.Theme.Focus : context.Theme.Border;

            string displayText = string.IsNullOrEmpty(Value) ? (Placeholder ?? string.Empty) : Value;
            if (displayText.Length > Width - 2)
            {
                displayText = displayText.Substring(0, Width - 2);
            }

            sb.Append(VT.MoveTo(X, Y));
            sb.Append(VT.RGB(borderColor.R, borderColor.G, borderColor.B));
            sb.Append("[");

            if (string.IsNullOrEmpty(Value) && !string.IsNullOrEmpty(Placeholder))
            {
                sb.Append(VT.RGB(context.Theme.Border.R, context.Theme.Border.G, context.Theme.Border.B));
                sb.Append(VT.Dim());
            }
            else
            {
                sb.Append(VT.RGB(fg.R, fg.G, fg.B));
            }

            sb.Append(displayText.PadRight(Width - 2));
            sb.Append(VT.RGB(borderColor.R, borderColor.G, borderColor.B));
            sb.Append("]");
            sb.Append(VT.Reset());

            // Show cursor if focused
            if (IsFocused && !IsReadOnly)
            {
                sb.Append(VT.MoveTo(X + 1 + _cursorPosition, Y));
                sb.Append(VT.ShowCursor());
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Grid column definition
    /// </summary>
    public class GridColumn
    {
        public string Header { get; set; }
        public string Property { get; set; }
        public string Width { get; set; }
        public int CalculatedWidth { get; set; }

        public GridColumn() { }

        public GridColumn(string header, string property, string width)
        {
            Header = header;
            Property = property;
            Width = width;
        }
    }

    /// <summary>
    /// DataGrid component for displaying tabular data
    /// </summary>
    public class DataGrid : Component
    {
        private IEnumerable _itemsSource;
        private int _selectedIndex;

        public IEnumerable ItemsSource
        {
            get => _itemsSource;
            set
            {
                // Unsubscribe from old collection
                if (_itemsSource is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= OnCollectionChanged;
                }

                _itemsSource = value;

                // Subscribe to new collection
                if (_itemsSource is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += OnCollectionChanged;
                }

                OnPropertyChanged(nameof(ItemsSource));
                Invalidate();
            }
        }

        public List<GridColumn> Columns { get; }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex = value;
                OnPropertyChanged(nameof(SelectedIndex));
                Invalidate();
            }
        }

        public event EventHandler ItemSelected;

        public DataGrid()
        {
            CanFocus = true;
            Columns = new List<GridColumn>();
            SelectedIndex = -1;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Invalidate();
        }

        public override bool HandleKey(ConsoleKeyInfo key)
        {
            if (ItemsSource == null)
                return false;

            var items = ItemsSource.Cast<object>().ToList();
            if (items.Count == 0)
                return false;

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    if (SelectedIndex > 0)
                    {
                        SelectedIndex--;
                        ItemSelected?.Invoke(this, EventArgs.Empty);
                    }
                    return true;

                case ConsoleKey.DownArrow:
                    if (SelectedIndex < items.Count - 1)
                    {
                        SelectedIndex++;
                        ItemSelected?.Invoke(this, EventArgs.Empty);
                    }
                    return true;

                case ConsoleKey.Home:
                    SelectedIndex = 0;
                    ItemSelected?.Invoke(this, EventArgs.Empty);
                    return true;

                case ConsoleKey.End:
                    SelectedIndex = items.Count - 1;
                    ItemSelected?.Invoke(this, EventArgs.Empty);
                    return true;

                case ConsoleKey.PageUp:
                    SelectedIndex = Math.Max(0, SelectedIndex - 10);
                    ItemSelected?.Invoke(this, EventArgs.Empty);
                    return true;

                case ConsoleKey.PageDown:
                    SelectedIndex = Math.Min(items.Count - 1, SelectedIndex + 10);
                    ItemSelected?.Invoke(this, EventArgs.Empty);
                    return true;
            }

            return false;
        }

        private void CalculateColumnWidths()
        {
            if (Columns.Count == 0) return;

            int totalFixed = 0;
            int starColumns = 0;

            // First pass: calculate fixed and auto widths
            foreach (var col in Columns)
            {
                if (col.Width == "*")
                {
                    starColumns++;
                }
                else if (col.Width == "Auto")
                {
                    col.CalculatedWidth = Math.Max(col.Header.Length, 10); // Min 10 chars
                    totalFixed += col.CalculatedWidth;
                }
                else if (int.TryParse(col.Width, out int fixedWidth))
                {
                    col.CalculatedWidth = fixedWidth;
                    totalFixed += fixedWidth;
                }
                else
                {
                    col.CalculatedWidth = 10; // Default
                    totalFixed += 10;
                }
            }

            // Second pass: distribute remaining width to star columns
            int remaining = Math.Max(0, Width - totalFixed - (Columns.Count + 1)); // Account for borders
            int starWidth = starColumns > 0 ? remaining / starColumns : 0;

            foreach (var col in Columns)
            {
                if (col.Width == "*")
                {
                    col.CalculatedWidth = Math.Max(starWidth, 5); // Min 5 chars
                }
            }
        }

        public override string Render(RenderContext context)
        {
            var sb = new StringBuilder();

            if (Columns.Count == 0 || ItemsSource == null)
                return string.Empty;

            CalculateColumnWidths(); // Calculate widths before rendering

            var items = ItemsSource.Cast<object>().ToList();
            Color headerFg = context.Theme.Primary;
            Color borderColor = IsFocused ? context.Theme.Focus : context.Theme.Border;
            Color selectionBg = context.Theme.Selection;

            // Render header
            sb.Append(VT.MoveTo(X, Y));
            sb.Append(VT.RGB(borderColor.R, borderColor.G, borderColor.B));
            sb.Append("┌");

            for (int i = 0; i < Columns.Count; i++)
            {
                sb.Append(new string('─', Columns[i].CalculatedWidth));
                if (i < Columns.Count - 1)
                    sb.Append("┬");
            }
            sb.Append("┐");

            // Column headers
            sb.Append(VT.MoveTo(X, Y + 1));
            sb.Append(VT.RGB(borderColor.R, borderColor.G, borderColor.B));
            sb.Append("│");

            foreach (var col in Columns)
            {
                sb.Append(VT.RGB(headerFg.R, headerFg.G, headerFg.B));
                sb.Append(VT.Bold());
                sb.Append(col.Header.PadRight(col.CalculatedWidth).Substring(0, Math.Min(col.Header.Length, col.CalculatedWidth)));
                sb.Append(VT.Reset());
                sb.Append(VT.RGB(borderColor.R, borderColor.G, borderColor.B));
                sb.Append("│");
            }

            // Header separator
            sb.Append(VT.MoveTo(X, Y + 2));
            sb.Append("├");
            for (int i = 0; i < Columns.Count; i++)
            {
                sb.Append(new string('─', Columns[i].CalculatedWidth));
                if (i < Columns.Count - 1)
                    sb.Append("┼");
            }
            sb.Append("┤");

            // Render rows
            int maxRows = Height - 4; // Account for header and borders
            int startIndex = Math.Max(0, SelectedIndex - maxRows / 2);
            int endIndex = Math.Min(items.Count, startIndex + maxRows);

            for (int i = startIndex; i < endIndex; i++)
            {
                int rowY = Y + 3 + (i - startIndex);
                sb.Append(VT.MoveTo(X, rowY));

                bool isSelected = i == SelectedIndex;
                if (isSelected)
                {
                    sb.Append(VT.RGB(context.Theme.Background.R, context.Theme.Background.G, context.Theme.Background.B, true));
                    sb.Append(VT.RGB(selectionBg.R, selectionBg.G, selectionBg.B));
                }

                sb.Append(VT.RGB(borderColor.R, borderColor.G, borderColor.B));
                sb.Append("│");

                var item = items[i];
                foreach (var col in Columns)
                {
                    string cellValue = GetPropertyValue(item, col.Property);
                    if (cellValue.Length > col.CalculatedWidth)
                        cellValue = cellValue.Substring(0, col.CalculatedWidth);

                    sb.Append(cellValue.PadRight(col.CalculatedWidth));
                    sb.Append(VT.RGB(borderColor.R, borderColor.G, borderColor.B));
                    sb.Append("│");
                }

                if (isSelected)
                    sb.Append(VT.Reset());
            }

            // Bottom border
            sb.Append(VT.MoveTo(X, Y + 3 + (endIndex - startIndex)));
            sb.Append(VT.RGB(borderColor.R, borderColor.G, borderColor.B));
            sb.Append("└");
            for (int i = 0; i < Columns.Count; i++)
            {
                sb.Append(new string('─', Columns[i].CalculatedWidth));
                if (i < Columns.Count - 1)
                    sb.Append("┴");
            }
            sb.Append("┘");
            sb.Append(VT.Reset());

            return sb.ToString();
        }

        private string GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null)
                return string.Empty;

            var prop = obj.GetType().GetProperty(propertyName);
            if (prop == null)
                return string.Empty;

            var value = prop.GetValue(obj);
            return value?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// ListView component for simple list display
    /// </summary>
    public class ListView : Component
    {
        private IEnumerable _itemsSource;
        private int _selectedIndex;
        private string _displayProperty;

        public IEnumerable ItemsSource
        {
            get => _itemsSource;
            set
            {
                if (_itemsSource is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= OnCollectionChanged;
                }

                _itemsSource = value;

                if (_itemsSource is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += OnCollectionChanged;
                }

                OnPropertyChanged(nameof(ItemsSource));
                Invalidate();
            }
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex = value;
                OnPropertyChanged(nameof(SelectedIndex));
                Invalidate();
            }
        }

        public string DisplayProperty
        {
            get => _displayProperty;
            set
            {
                _displayProperty = value;
                OnPropertyChanged(nameof(DisplayProperty));
                Invalidate();
            }
        }

        public event EventHandler ItemSelected;

        public ListView()
        {
            CanFocus = true;
            SelectedIndex = -1;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Invalidate();
        }

        public override bool HandleKey(ConsoleKeyInfo key)
        {
            if (ItemsSource == null)
                return false;

            var items = ItemsSource.Cast<object>().ToList();
            if (items.Count == 0)
                return false;

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    if (SelectedIndex > 0)
                    {
                        SelectedIndex--;
                        ItemSelected?.Invoke(this, EventArgs.Empty);
                    }
                    return true;

                case ConsoleKey.DownArrow:
                    if (SelectedIndex < items.Count - 1)
                    {
                        SelectedIndex++;
                        ItemSelected?.Invoke(this, EventArgs.Empty);
                    }
                    return true;

                case ConsoleKey.Home:
                    SelectedIndex = 0;
                    ItemSelected?.Invoke(this, EventArgs.Empty);
                    return true;

                case ConsoleKey.End:
                    SelectedIndex = items.Count - 1;
                    ItemSelected?.Invoke(this, EventArgs.Empty);
                    return true;
            }

            return false;
        }

        public override string Render(RenderContext context)
        {
            var sb = new StringBuilder();

            if (ItemsSource == null)
                return string.Empty;

            var items = ItemsSource.Cast<object>().ToList();
            Color selectionBg = context.Theme.Selection;
            Color fg = context.Theme.Foreground;

            int maxRows = Height;
            int startIndex = Math.Max(0, SelectedIndex - maxRows / 2);
            int endIndex = Math.Min(items.Count, startIndex + maxRows);

            for (int i = startIndex; i < endIndex; i++)
            {
                int rowY = Y + (i - startIndex);
                sb.Append(VT.MoveTo(X, rowY));

                bool isSelected = i == SelectedIndex && IsFocused;
                if (isSelected)
                {
                    sb.Append(VT.RGB(selectionBg.R, selectionBg.G, selectionBg.B, true));
                }

                sb.Append(VT.RGB(fg.R, fg.G, fg.B));

                string displayText = GetDisplayText(items[i]);
                if (displayText.Length > Width)
                    displayText = displayText.Substring(0, Width);

                sb.Append(displayText.PadRight(Width));
                sb.Append(VT.Reset());
            }

            return sb.ToString();
        }

        private string GetDisplayText(object item)
        {
            if (item == null)
                return string.Empty;

            if (string.IsNullOrEmpty(DisplayProperty))
                return item.ToString();

            var prop = item.GetType().GetProperty(DisplayProperty);
            if (prop == null)
                return item.ToString();

            var value = prop.GetValue(item);
            return value?.ToString() ?? string.Empty;
        }
    }

    #endregion

    #region VT100 Rendering

    /// <summary>
    /// VT100 escape sequence helper
    /// </summary>
    public static class VT
    {
        /// <summary>
        /// Move cursor to position (1-based coordinates)
        /// </summary>
        public static string MoveTo(int x, int y)
        {
            return $"\x1b[{y + 1};{x + 1}H";
        }

        /// <summary>
        /// Set foreground color using 24-bit RGB
        /// </summary>
        public static string RGB(byte r, byte g, byte b, bool background = false)
        {
            if (background)
                return $"\x1b[48;2;{r};{g};{b}m";
            return $"\x1b[38;2;{r};{g};{b}m";
        }

        /// <summary>
        /// Clear the entire screen
        /// </summary>
        public static string Clear()
        {
            return "\x1b[2J";
        }

        /// <summary>
        /// Hide the cursor
        /// </summary>
        public static string HideCursor()
        {
            return "\x1b[?25l";
        }

        /// <summary>
        /// Show the cursor
        /// </summary>
        public static string ShowCursor()
        {
            return "\x1b[?25h";
        }

        /// <summary>
        /// Reset all text attributes
        /// </summary>
        public static string Reset()
        {
            return "\x1b[0m";
        }

        /// <summary>
        /// Bold text
        /// </summary>
        public static string Bold()
        {
            return "\x1b[1m";
        }

        /// <summary>
        /// Dim text
        /// </summary>
        public static string Dim()
        {
            return "\x1b[2m";
        }

        /// <summary>
        /// Underline text
        /// </summary>
        public static string Underline()
        {
            return "\x1b[4m";
        }

        /// <summary>
        /// Save cursor position
        /// </summary>
        public static string SavePosition()
        {
            return "\x1b[s";
        }

        /// <summary>
        /// Restore cursor position
        /// </summary>
        public static string RestorePosition()
        {
            return "\x1b[u";
        }
    }

    #endregion

    #region Terminal

    /// <summary>
    /// Terminal abstraction for direct console access
    /// </summary>
    public class Terminal
    {
        private static Terminal _instance;
        private StringBuilder _buffer;
        private bool _buffering;

        public static Terminal Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Terminal();
                return _instance;
            }
        }

        public int Width { get; private set; }
        public int Height { get; private set; }

        private Terminal()
        {
            UpdateDimensions();
            _buffer = new StringBuilder();
            _buffering = false;
        }

        /// <summary>
        /// Update terminal dimensions from console
        /// </summary>
        public void UpdateDimensions()
        {
            Width = Console.WindowWidth;
            Height = Console.WindowHeight;
        }

        /// <summary>
        /// Clear the screen
        /// </summary>
        public void Clear()
        {
            Write(VT.Clear());
            Write(VT.MoveTo(0, 0));
        }

        /// <summary>
        /// Move cursor to position
        /// </summary>
        public void MoveTo(int x, int y)
        {
            Write(VT.MoveTo(x, y));
        }

        /// <summary>
        /// Write text to terminal
        /// </summary>
        public void Write(string text)
        {
            if (_buffering)
            {
                _buffer.Append(text);
            }
            else
            {
                Console.Write(text);
            }
        }

        /// <summary>
        /// Begin buffered rendering
        /// </summary>
        public void BeginFrame()
        {
            _buffering = true;
            _buffer.Clear();
        }

        /// <summary>
        /// End buffered rendering and flush to console
        /// </summary>
        public void EndFrame()
        {
            if (_buffering)
            {
                Console.Write(_buffer.ToString());
                _buffer.Clear();
                _buffering = false;
            }
        }
    }

    #endregion

    #region ScreenManager

    /// <summary>
    /// Manages screen navigation and rendering loop
    /// </summary>
    public class ScreenManager
    {
        private static ScreenManager _instance;
        private Stack<Screen> _screenStack;
        private bool _running;
        private RenderContext _renderContext;

        public static ScreenManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ScreenManager();
                return _instance;
            }
        }

        public Screen Current => _screenStack.Count > 0 ? _screenStack.Peek() : null;

        private ScreenManager()
        {
            _screenStack = new Stack<Screen>();
            var terminal = Terminal.Instance;
            _renderContext = new RenderContext(Theme.Default, terminal.Width, terminal.Height);
        }

        /// <summary>
        /// Push a new screen onto the stack
        /// </summary>
        public void Push(Screen screen)
        {
            if (Current != null)
            {
                Current.OnDeactivate();
            }

            _screenStack.Push(screen);
            screen.Width = Terminal.Instance.Width;
            screen.Height = Terminal.Instance.Height;
            screen.OnActivate();
            screen.Invalidate();
        }

        /// <summary>
        /// Pop the current screen from the stack
        /// </summary>
        public void Pop()
        {
            if (_screenStack.Count > 0)
            {
                var screen = _screenStack.Pop();
                screen.OnDeactivate();

                if (Current != null)
                {
                    Current.OnActivate();
                    Current.Invalidate();
                }
            }
        }

        /// <summary>
        /// Replace the current screen
        /// </summary>
        public void Replace(Screen screen)
        {
            if (_screenStack.Count > 0)
            {
                Pop();
            }
            Push(screen);
        }

        /// <summary>
        /// Run the main rendering loop
        /// </summary>
        public void Run()
        {
            if (_screenStack.Count == 0)
            {
                throw new InvalidOperationException("No screen to display. Push a screen before calling Run().");
            }

            _running = true;
            var terminal = Terminal.Instance;

            Console.CursorVisible = false;
            terminal.Clear();

            while (_running && Current != null)
            {
                // Handle resize
                terminal.UpdateDimensions();
                if (_renderContext.TerminalWidth != terminal.Width || _renderContext.TerminalHeight != terminal.Height)
                {
                    _renderContext.TerminalWidth = terminal.Width;
                    _renderContext.TerminalHeight = terminal.Height;
                    Current.Width = terminal.Width;
                    Current.Height = terminal.Height;
                    Current.OnResize(terminal.Width, terminal.Height);
                    Current.Invalidate();
                }

                // Render if dirty
                if (Current.IsDirty)
                {
                    terminal.BeginFrame();
                    terminal.Write(VT.HideCursor());
                    terminal.Write(Current.Render(_renderContext));
                    terminal.EndFrame();
                }

                // Handle input
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);

                    // Check for exit key (Escape by default)
                    if (key.Key == ConsoleKey.Escape && key.Modifiers == 0)
                    {
                        Stop();
                        break;
                    }

                    Current.HandleKey(key);
                }

                // Small sleep to prevent CPU spinning
                System.Threading.Thread.Sleep(16); // ~60 FPS
            }

            terminal.Clear();
            Console.CursorVisible = true;
        }

        /// <summary>
        /// Stop the rendering loop
        /// </summary>
        public void Stop()
        {
            _running = false;
        }
    }

    #endregion

    #region EventBus

    /// <summary>
    /// Event arguments for data events
    /// </summary>
    public class DataEventArgs : EventArgs
    {
        public object Data { get; set; }

        public DataEventArgs(object data)
        {
            Data = data;
        }
    }

    /// <summary>
    /// Application-wide event bus for decoupled communication
    /// </summary>
    public class EventBus
    {
        private static EventBus _instance;

        public static EventBus Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new EventBus();
                return _instance;
            }
        }

        // Typed events
        public event EventHandler<DataEventArgs> TaskCreated;
        public event EventHandler<DataEventArgs> TaskUpdated;
        public event EventHandler<DataEventArgs> TaskDeleted;
        public event EventHandler<DataEventArgs> ProjectCreated;
        public event EventHandler<DataEventArgs> ProjectUpdated;

        private EventBus() { }

        /// <summary>
        /// Publish TaskCreated event
        /// </summary>
        public void PublishTaskCreated(object task)
        {
            TaskCreated?.Invoke(this, new DataEventArgs(task));
        }

        /// <summary>
        /// Publish TaskUpdated event
        /// </summary>
        public void PublishTaskUpdated(object task)
        {
            TaskUpdated?.Invoke(this, new DataEventArgs(task));
        }

        /// <summary>
        /// Publish TaskDeleted event
        /// </summary>
        public void PublishTaskDeleted(object task)
        {
            TaskDeleted?.Invoke(this, new DataEventArgs(task));
        }

        /// <summary>
        /// Publish ProjectCreated event
        /// </summary>
        public void PublishProjectCreated(object project)
        {
            ProjectCreated?.Invoke(this, new DataEventArgs(project));
        }

        /// <summary>
        /// Publish ProjectUpdated event
        /// </summary>
        public void PublishProjectUpdated(object project)
        {
            ProjectUpdated?.Invoke(this, new DataEventArgs(project));
        }
    }

    #endregion

    #region ServiceContainer

    /// <summary>
    /// Simple dependency injection container
    /// </summary>
    public class ServiceContainer
    {
        private static ServiceContainer _instance;
        private Dictionary<string, Func<object>> _factories;
        private Dictionary<string, object> _singletons;

        public static ServiceContainer Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ServiceContainer();
                return _instance;
            }
        }

        private ServiceContainer()
        {
            _factories = new Dictionary<string, Func<object>>();
            _singletons = new Dictionary<string, object>();
        }

        /// <summary>
        /// Register a service factory
        /// </summary>
        public void Register(string name, Func<object> factory)
        {
            _factories[name] = factory;
        }

        /// <summary>
        /// Register a singleton service
        /// </summary>
        public void RegisterSingleton(string name, object instance)
        {
            _singletons[name] = instance;
        }

        /// <summary>
        /// Get a service instance
        /// </summary>
        public object Get(string name)
        {
            // Check singletons first
            if (_singletons.ContainsKey(name))
            {
                return _singletons[name];
            }

            // Then check factories
            if (_factories.ContainsKey(name))
            {
                return _factories[name]();
            }

            throw new InvalidOperationException($"Service '{name}' not registered.");
        }

        /// <summary>
        /// Get a typed service instance
        /// </summary>
        public T Get<T>(string name)
        {
            return (T)Get(name);
        }
    }

    #endregion

    #region Theme

    /// <summary>
    /// Theme definition for consistent styling
    /// </summary>
    public class Theme
    {
        public Color Primary { get; set; }
        public Color Secondary { get; set; }
        public Color Success { get; set; }
        public Color Warning { get; set; }
        public Color Error { get; set; }
        public Color Background { get; set; }
        public Color Foreground { get; set; }
        public Color Border { get; set; }
        public Color Focus { get; set; }
        public Color Selection { get; set; }

        private Dictionary<string, Tuple<Color, Color>> _styles;

        public Theme()
        {
            _styles = new Dictionary<string, Tuple<Color, Color>>();
        }

        /// <summary>
        /// Get style colors by name (foreground, background)
        /// </summary>
        public Tuple<Color, Color> GetStyle(string name)
        {
            if (_styles.ContainsKey(name))
            {
                return _styles[name];
            }

            // Return default if not found
            return Tuple.Create(Foreground, Background);
        }

        /// <summary>
        /// Register a named style
        /// </summary>
        public void RegisterStyle(string name, Color foreground, Color background)
        {
            _styles[name] = Tuple.Create(foreground, background);
        }

        /// <summary>
        /// Default dark theme
        /// </summary>
        public static Theme Default
        {
            get
            {
                var theme = new Theme
                {
                    Primary = Color.FromRgb(88, 166, 255),      // Blue
                    Secondary = Color.FromRgb(138, 138, 138),   // Gray
                    Success = Color.FromRgb(73, 209, 73),       // Green
                    Warning = Color.FromRgb(255, 193, 7),       // Yellow
                    Error = Color.FromRgb(244, 67, 54),         // Red
                    Background = Color.FromRgb(30, 30, 30),     // Dark gray
                    Foreground = Color.FromRgb(220, 220, 220),  // Light gray
                    Border = Color.FromRgb(60, 60, 60),         // Medium gray
                    Focus = Color.FromRgb(88, 166, 255),        // Blue
                    Selection = Color.FromRgb(70, 130, 180)     // Steel blue
                };

                // Register some common styles
                theme.RegisterStyle("Title", Color.FromRgb(88, 166, 255), theme.Background);
                theme.RegisterStyle("Subtitle", Color.FromRgb(138, 138, 138), theme.Background);
                theme.RegisterStyle("Success", theme.Success, theme.Background);
                theme.RegisterStyle("Warning", theme.Warning, theme.Background);
                theme.RegisterStyle("Error", theme.Error, theme.Background);

                return theme;
            }
        }
    }

    #endregion
}
