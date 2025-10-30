using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    public class HelpPane : PaneBase
    {
        private TextBlock helpContent;
        private SolidColorBrush bgBrush;
        private SolidColorBrush fgBrush;
        private SolidColorBrush accentBrush;
        private SolidColorBrush dimBrush;

        public HelpPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext)
            : base(logger, themeManager, projectContext)
        {
            PaneName = "Help";
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        protected override UIElement BuildContent()
        {
            CacheThemeColors();

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            grid.Background = bgBrush;

            // Header
            var header = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Padding = new Thickness(16, 16, 16, 8),
                Text = "⌨️  Keyboard Shortcuts"
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Scrollable content
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(16)
            };

            helpContent = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 16,
                Foreground = fgBrush,
                TextWrapping = TextWrapping.Wrap,
                Text = @"WORKSPACES:
  Ctrl+1-9          Switch to workspace 1-9
  F12               Toggle move pane mode (use arrows to move panes)

PANE NAVIGATION:
  Ctrl+Shift+←→↑↓   Focus pane in direction
  Ctrl+Shift+T      Open Tasks pane
  Ctrl+Shift+N      Open Notes pane
  Ctrl+Shift+P      Open Projects pane
  Ctrl+Shift+E      Open Excel Import pane
  Ctrl+Shift+Q      Close focused pane

COMMAND PALETTE:
  : (Shift+;)       Open command palette
  ? (Shift+/)       Show this help

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

TASK PANE:
  A                 Add new task (form: Title | DueDate | Priority)
                    - Tab cycles between fields
                    - Priority: 1=High, 2=Medium, 3=Low
                    - Dates: 2d, tomorrow, next friday, 2025-12-31
  S                 Create subtask (max 2 levels: Parent → Child)
                    - S on child creates sibling, NOT grandchild
  E / Enter         Edit task title inline
  D                 Delete task
  Space             Toggle complete
  Shift+D           Edit due date inline
  Shift+T           Edit tags inline
  PageUp/Down       Reorder task (change sort position)
  Ctrl+1/2/3        Set priority (High/Medium/Low)

NOTES PANE:
  A                 New note
  E                 Edit note (focus editor)
  D                 Delete note
  S / F             Search/filter
  O                 Open external .txt file (via FileBrowser)
  Ctrl+S            Save note

PROJECTS PANE:
  A                 Add project (quick: Name | DateAssigned | ID2)
  D                 Delete project
  K                 Set project context (filters all panes)
  X                 Export T2020 text file to Desktop
  Click field       Edit any field inline

EXCEL IMPORT PANE:
  I                 Import from clipboard
                    1. Open Excel SVI-CAS form
                    2. Select cells W3:W130 (48 fields)
                    3. Copy (Ctrl+C)
                    4. Paste in textbox (system paste)
                    5. Press I to import

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Press Escape or Ctrl+Shift+Q to close this pane."
            };

            scrollViewer.Content = helpContent;
            Grid.SetRow(scrollViewer, 1);
            grid.Children.Add(scrollViewer);

            return grid;
        }

        private void CacheThemeColors()
        {
            var theme = themeManager.CurrentTheme;
            bgBrush = new SolidColorBrush(theme.Background);
            fgBrush = new SolidColorBrush(theme.Foreground);
            accentBrush = new SolidColorBrush(theme.Primary);
            dimBrush = new SolidColorBrush(theme.ForegroundSecondary);
        }

        protected override void OnDispose()
        {
            base.OnDispose();
        }
    }
}
