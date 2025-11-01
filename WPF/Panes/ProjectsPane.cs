using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Globalization;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Panes
{
    /// <summary>
    /// Projects management pane with full CRUD, Excel integration, T2020 export
    /// Displays all ~50 project fields with list on left, detail panel on right
    /// 100% keyboard-driven terminal aesthetic
    /// </summary>
    public class ProjectsPane : PaneBase
    {
        // Services
        private readonly IProjectService projectService;
        private readonly IEventBus eventBus;
        private readonly IConfigurationManager configManager;

        // Event handlers (store references for proper unsubscription)
        private Action<Core.Events.TaskSelectedEvent> taskSelectedHandler;
        private Action<Core.Events.RefreshRequestedEvent> refreshRequestedHandler;

        // UI Components - Two column layout
        private Grid mainLayout;
        private ListBox projectListBox;           // Left column: project list
        private ScrollViewer detailScroll;        // Right column: project details
        private Grid detailPanel;                 // Detail form
        private TextBox quickAddBox;              // Quick add (Name, DateAssigned, ID2)
        private TextBox searchBox;                // Search box
        private TextBlock statusBar;
        private TextBlock filterLabel;

        // Inline editing
        private Dictionary<string, TextBox> fieldEditors = new Dictionary<string, TextBox>();
        private string editingFieldName = null;

        // State
        private List<Project> allProjects = new List<Project>();
        private Project selectedProject = null;
        private FilterMode currentFilter = FilterMode.Active;
        private bool isInternalCommand = false;
        private string searchQuery = string.Empty;
        private HashSet<string> modifiedFields = new HashSet<string>();

        // Theme colors (cached)
        private SolidColorBrush bgBrush;
        private SolidColorBrush fgBrush;
        private SolidColorBrush accentBrush;
        private SolidColorBrush borderBrush;
        private SolidColorBrush dimBrush;
        private SolidColorBrush surfaceBrush;
        private SolidColorBrush successBrush;
        private SolidColorBrush warningBrush;

        // Filter modes
        private enum FilterMode
        {
            All,
            Active,
            Planned,
            Completed,
            Overdue,
            HighPriority,
            Archived
        }

        public ProjectsPane(
            ILogger logger,
            IThemeManager themeManager,
            IProjectContextManager projectContext,
            IConfigurationManager configManager,
            IProjectService projectService,
            IEventBus eventBus)
            : base(logger, themeManager, projectContext)
        {
            this.configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            PaneName = "Projects";
        }

        public override void Initialize()
        {
            base.Initialize();

            // Register pane-specific shortcuts with ShortcutManager
            RegisterPaneShortcuts();

            // Subscribe to theme changes
            themeManager.ThemeChanged += OnThemeChanged;

            // Subscribe to TaskSelectedEvent for cross-pane communication
            taskSelectedHandler = OnTaskSelected;
            eventBus.Subscribe(taskSelectedHandler);

            // Subscribe to RefreshRequestedEvent for global refresh (Ctrl+R)
            refreshRequestedHandler = OnRefreshRequested;
            eventBus.Subscribe(refreshRequestedHandler);

            // Set initial focus to project list
            Dispatcher.BeginInvoke(new Action(() =>
            {
                projectListBox?.Focus();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void RegisterPaneShortcuts()
        {
            var shortcuts = ShortcutManager.Instance;
            shortcuts.RegisterForPane(PaneName, Key.F, ModifierKeys.Control, () => { searchBox?.Focus(); searchBox?.SelectAll(); }, "Focus search box");
            shortcuts.RegisterForPane(PaneName, Key.A, ModifierKeys.None, () => ShowQuickAdd(), "Show quick add");
            shortcuts.RegisterForPane(PaneName, Key.D, ModifierKeys.None, () => { if (selectedProject != null) DeleteCurrentProject(); }, "Delete selected project");
            shortcuts.RegisterForPane(PaneName, Key.F, ModifierKeys.None, () => CycleFilter(), "Cycle filter mode");
            shortcuts.RegisterForPane(PaneName, Key.K, ModifierKeys.None, () => { if (selectedProject != null) { projectContext.SetProject(selectedProject); logger.Log(LogLevel.Info, "ProjectsPane", $"Set project context: {selectedProject.Name}"); UpdateStatusBar(); } }, "Set project as context");
            shortcuts.RegisterForPane(PaneName, Key.X, ModifierKeys.None, () => { if (selectedProject != null) ExportT2020(selectedProject); }, "Export T2020");
        }

        protected override UIElement BuildContent()
        {
            CacheThemeColors();

            mainLayout = new Grid();
            mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); // Left: list
            mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) }); // Splitter
            mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Right: detail

            // Left panel: Project list
            var listPanel = BuildProjectListPanel();
            Grid.SetColumn(listPanel, 0);
            mainLayout.Children.Add(listPanel);

            // Splitter
            var splitter = new GridSplitter
            {
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ResizeDirection = GridResizeDirection.Columns,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                Background = borderBrush,
                Cursor = Cursors.SizeWE
            };
            Grid.SetColumn(splitter, 1);
            mainLayout.Children.Add(splitter);

            // Right panel: Project detail
            detailScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = bgBrush
            };
            detailPanel = BuildProjectDetailPanel();
            detailScroll.Content = detailPanel;
            Grid.SetColumn(detailScroll, 2);
            mainLayout.Children.Add(detailScroll);

            // Subscribe to project events
            SubscribeToProjectEvents();

            // Load projects
            RefreshProjectList();

            return mainLayout;
        }

        private void CacheThemeColors()
        {
            var theme = themeManager.CurrentTheme;
            bgBrush = new SolidColorBrush(theme.Background);
            fgBrush = new SolidColorBrush(theme.Foreground);
            accentBrush = new SolidColorBrush(theme.Primary);
            borderBrush = new SolidColorBrush(theme.Border);
            dimBrush = new SolidColorBrush(theme.ForegroundSecondary);
            surfaceBrush = new SolidColorBrush(theme.Surface);
            successBrush = new SolidColorBrush(theme.Success);
            warningBrush = new SolidColorBrush(theme.Warning);
        }

        private Grid BuildProjectListPanel()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search box
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Quick add
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Filter bar
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Project list
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status bar

            // Search box
            searchBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 16,
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Top,
                Text = "Search... (Ctrl+F)"
            };
            searchBox.GotFocus += (s, e) =>
            {
                if (searchBox.Text == "Search... (Ctrl+F)")
                    searchBox.Text = "";
            };
            searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(searchBox.Text))
                    searchBox.Text = "Search... (Ctrl+F)";
            };
            searchBox.TextChanged += OnSearchTextChanged;
            Grid.SetRow(searchBox, 0);
            grid.Children.Add(searchBox);

            // Quick add box (Name, DateAssigned, ID2)
            quickAddBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 18,
                Background = surfaceBrush,
                Foreground = fgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed
            };
            quickAddBox.KeyDown += QuickAddBox_KeyDown;
            Grid.SetRow(quickAddBox, 1);
            grid.Children.Add(quickAddBox);

            // Filter label
            filterLabel = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = accentBrush,
                Padding = new Thickness(8, 4, 8, 4),
                Background = surfaceBrush,
                Text = $"Filter: {currentFilter}"
            };
            Grid.SetRow(filterLabel, 2);
            grid.Children.Add(filterLabel);

            // Project list
            projectListBox = new ListBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 16,
                Background = bgBrush,
                Foreground = fgBrush,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            projectListBox.SelectionChanged += ProjectListBox_SelectionChanged;
            projectListBox.KeyDown += ProjectListBox_KeyDown;

            // Enable virtualization for large project lists
            VirtualizingPanel.SetIsVirtualizing(projectListBox, true);
            VirtualizingPanel.SetVirtualizationMode(projectListBox, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(projectListBox, ScrollUnit.Pixel);

            Grid.SetRow(projectListBox, 3);
            grid.Children.Add(projectListBox);

            // Status bar
            statusBar = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = dimBrush,
                Background = surfaceBrush,
                Padding = new Thickness(8, 4, 8, 4),
                Text = GetStatusBarText()
            };
            Grid.SetRow(statusBar, 4);
            grid.Children.Add(statusBar);

            return grid;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            searchQuery = searchBox.Text;

            // Skip placeholder text
            if (searchQuery == "Search... (Ctrl+F)")
            {
                searchQuery = string.Empty;
            }

            RefreshProjectList();
        }

        private Grid BuildProjectDetailPanel()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Fields

            // Header
            var header = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Padding = new Thickness(16, 12, 16, 12),
                Text = "Project Details"
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Fields panel
            var fieldsPanel = new StackPanel
            {
                Margin = new Thickness(16, 0, 16, 16)
            };
            Grid.SetRow(fieldsPanel, 1);
            grid.Children.Add(fieldsPanel);

            // Add all field rows (will be populated when project selected)
            // This is just the container, actual fields added in DisplayProjectDetails()

            return grid;
        }

        private void DisplayProjectDetails(Project project)
        {
            var detailGrid = (Grid)detailScroll.Content;
            var fieldsPanel = (StackPanel)detailGrid.Children[1];

            if (project == null)
            {
                // Clear detail panel
                fieldsPanel.Children.Clear();
                fieldEditors.Clear();
                modifiedFields.Clear();
                return;
            }
            fieldsPanel.Children.Clear();
            fieldEditors.Clear();
            modifiedFields.Clear();

            // Section 1: Core Identity (Expanded by default)
            var coreSection = CreateCollapsibleSection("Core Identity", true);
            var coreContent = (StackPanel)coreSection.Content;
            AddField(coreContent, "Name", project.Name ?? "");
            AddField(coreContent, "Nickname", project.Nickname ?? "");
            AddField(coreContent, "ID2 (CAS Case)", project.ID2 ?? "");
            AddField(coreContent, "Id1 (Audit Case)", project.Id1 ?? "");
            AddField(coreContent, "Full Project Name", project.FullProjectName ?? "");
            AddFieldReadOnly(coreContent, "Status", project.Status.ToString());
            AddFieldReadOnly(coreContent, "Priority", project.Priority.ToString());
            fieldsPanel.Children.Add(coreSection);

            // Section 2: Dates (Collapsed)
            var datesSection = CreateCollapsibleSection("Important Dates", false);
            var datesContent = (StackPanel)datesSection.Content;
            AddDateField(datesContent, "Date Assigned", project.DateAssigned);
            AddDateField(datesContent, "Request Date", project.RequestDate);
            AddDateField(datesContent, "Start Date", project.StartDate);
            AddDateField(datesContent, "End Date", project.EndDate);
            AddDateField(datesContent, "Audit Period From", project.AuditPeriodFrom);
            AddDateField(datesContent, "Audit Period To", project.AuditPeriodTo);
            fieldsPanel.Children.Add(datesSection);

            // Section 3: Client Info (Collapsed)
            var clientSection = CreateCollapsibleSection("Taxpayer/Client Information", false);
            var clientContent = (StackPanel)clientSection.Content;
            AddField(clientContent, "Client ID", project.ClientID ?? "");
            AddField(clientContent, "TaxID", project.TaxID ?? "");
            AddField(clientContent, "CAS Number", project.CASNumber ?? "");
            AddField(clientContent, "Address", project.Address ?? "");
            AddField(clientContent, "City", project.City ?? "");
            AddField(clientContent, "Province", project.Province ?? "");
            AddField(clientContent, "Postal Code", project.PostalCode ?? "");
            AddField(clientContent, "Country", project.Country ?? "");
            AddField(clientContent, "TP Email", project.TPEmailAddress ?? "");
            AddField(clientContent, "TP Phone", project.TPPhoneNumber ?? "");
            AddField(clientContent, "Ship To Address", project.ShipToAddress ?? "");
            fieldsPanel.Children.Add(clientSection);

            // Section 4: Project Details (Collapsed)
            var projectSection = CreateCollapsibleSection("Project Details", false);
            var projectContent = (StackPanel)projectSection.Content;
            AddField(projectContent, "Audit Type", project.AuditType ?? "");
            AddField(projectContent, "Audit Program", project.AuditProgram ?? "");
            AddField(projectContent, "Auditor Name", project.AuditorName ?? "");
            AddField(projectContent, "Description", project.Description ?? "");
            AddField(projectContent, "Comments", project.Comments ?? "");
            AddField(projectContent, "FX Info", project.FXInfo ?? "");
            AddField(projectContent, "Email Reference", project.EmailReference ?? "");
            fieldsPanel.Children.Add(projectSection);

            // Section 5: Contacts (Collapsed)
            var contactsSection = CreateCollapsibleSection("Contacts", false);
            var contactsContent = (StackPanel)contactsSection.Content;
            AddField(contactsContent, "Contact 1 Name", project.Contact1Name ?? "");
            AddField(contactsContent, "Contact 1 Title", project.Contact1Title ?? "");
            AddField(contactsContent, "Contact 1 Phone", project.Contact1Phone ?? "");
            AddField(contactsContent, "Contact 1 Ext", project.Contact1Ext ?? "");
            AddField(contactsContent, "Contact 1 Address", project.Contact1Address ?? "");
            AddField(contactsContent, "Contact 2 Name", project.Contact2Name ?? "");
            AddField(contactsContent, "Contact 2 Title", project.Contact2Title ?? "");
            AddField(contactsContent, "Contact 2 Phone", project.Contact2Phone ?? "");
            AddField(contactsContent, "Contact 2 Ext", project.Contact2Ext ?? "");
            AddField(contactsContent, "Contact 2 Address", project.Contact2Address ?? "");
            fieldsPanel.Children.Add(contactsSection);

            // Section 6: Software (Collapsed)
            var softwareSection = CreateCollapsibleSection("Accounting Software", false);
            var softwareContent = (StackPanel)softwareSection.Content;
            AddField(softwareContent, "Software 1", project.AccountingSoftware1 ?? "");
            AddField(softwareContent, "Software 1 Other", project.AccountingSoftware1Other ?? "");
            AddField(softwareContent, "Software 1 Type", project.AccountingSoftware1Type ?? "");
            AddField(softwareContent, "Software 2", project.AccountingSoftware2 ?? "");
            AddField(softwareContent, "Software 2 Other", project.AccountingSoftware2Other ?? "");
            AddField(softwareContent, "Software 2 Type", project.AccountingSoftware2Type ?? "");
            fieldsPanel.Children.Add(softwareSection);

            // Section 7: File Locations (Collapsed)
            var filesSection = CreateCollapsibleSection("File Locations", false);
            var filesContent = (StackPanel)filesSection.Content;
            AddField(filesContent, "Project Folder", project.CustomFields.ContainsKey("ProjectFolder") ? project.CustomFields["ProjectFolder"] : "");
            AddField(filesContent, "CAA File", project.CustomFields.ContainsKey("CAAFile") ? project.CustomFields["CAAFile"] : "");
            AddField(filesContent, "Request File", project.CustomFields.ContainsKey("RequestFile") ? project.CustomFields["RequestFile"] : "");
            AddField(filesContent, "T2020 File", project.CustomFields.ContainsKey("T2020File") ? project.CustomFields["T2020File"] : "");
            fieldsPanel.Children.Add(filesSection);

            // Section 8: Budget (Collapsed)
            var budgetSection = CreateCollapsibleSection("Budget", false);
            var budgetContent = (StackPanel)budgetSection.Content;
            AddField(budgetContent, "Budget Hours", project.BudgetHours?.ToString() ?? "");
            AddField(budgetContent, "Budget Amount", project.BudgetAmount?.ToString("C") ?? "");
            fieldsPanel.Children.Add(budgetSection);

            // Section 9: Metadata (Collapsed)
            var metadataSection = CreateCollapsibleSection("Metadata", false);
            var metadataContent = (StackPanel)metadataSection.Content;
            AddFieldReadOnly(metadataContent, "Created", project.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
            AddFieldReadOnly(metadataContent, "Updated", project.UpdatedAt.ToString("yyyy-MM-dd HH:mm"));
            AddFieldReadOnly(metadataContent, "Archived", project.Archived ? "Yes" : "No");
            fieldsPanel.Children.Add(metadataSection);
        }

        private Expander CreateCollapsibleSection(string title, bool isExpanded)
        {
            var theme = themeManager.CurrentTheme;
            return new Expander
            {
                Header = title,
                IsExpanded = isExpanded,
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Primary),
                Margin = new Thickness(0, 8, 0, 8),
                Content = new StackPanel { Margin = new Thickness(16, 4, 0, 0) }
            };
        }

        private void AddSectionHeader(StackPanel panel, string title)
        {
            var header = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = accentBrush,
                Text = title,
                Margin = new Thickness(0, 16, 0, 8)
            };
            panel.Children.Add(header);
        }

        private void AddField(StackPanel panel, string label, string value)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Margin = new Thickness(0, 4, 0, 4);

            var labelText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = dimBrush,
                Text = label + ":",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelText, 0);
            row.Children.Add(labelText);

            var valueText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = fgBrush,
                Text = value,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valueText, 1);
            row.Children.Add(valueText);

            // Store for inline editing
            valueText.Tag = label;
            valueText.MouseLeftButtonDown += (s, e) =>
            {
                if (selectedProject != null && !isInternalCommand)
                {
                    StartFieldEdit(label, value, valueText);
                }
            };

            panel.Children.Add(row);
        }

        private void AddFieldReadOnly(StackPanel panel, string label, string value)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Margin = new Thickness(0, 4, 0, 4);

            var labelText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = dimBrush,
                Text = label + ":",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelText, 0);
            row.Children.Add(labelText);

            var valueText = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = dimBrush,
                Text = value,
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valueText, 1);
            row.Children.Add(valueText);

            panel.Children.Add(row);
        }

        private void AddDateField(StackPanel panel, string label, DateTime? value)
        {
            AddField(panel, label, value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "");
        }

        private void StartFieldEdit(string fieldName, string currentValue, TextBlock targetText)
        {
            editingFieldName = fieldName;

            // Replace TextBlock with TextBox temporarily
            var parent = (Grid)targetText.Parent;
            var editBox = new TextBox
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = fgBrush,
                Background = surfaceBrush,
                BorderBrush = accentBrush,
                BorderThickness = new Thickness(1),
                Text = currentValue,
                VerticalAlignment = VerticalAlignment.Center
            };
            editBox.KeyDown += FieldEditBox_KeyDown;
            editBox.LostFocus += (s, e) => CancelFieldEdit(editBox, targetText);

            Grid.SetColumn(editBox, 1);
            parent.Children.Remove(targetText);
            parent.Children.Add(editBox);
            fieldEditors[fieldName] = editBox;

            editBox.Focus();
            editBox.SelectAll();
        }

        private void FieldEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            var editBox = sender as TextBox;
            if (editBox == null) return;

            if (e.Key == Key.Enter)
            {
                SaveFieldEdit(editBox);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelFieldEdit(editBox, null);
                e.Handled = true;
            }
        }

        private void SaveFieldEdit(TextBox editBox)
        {
            if (selectedProject == null || string.IsNullOrEmpty(editingFieldName)) return;

            string newValue = editBox.Text.Trim();

            // Map field name to project property and update
            UpdateProjectField(selectedProject, editingFieldName, newValue);

            // Track modification
            modifiedFields.Add(editingFieldName);

            // Save to service
            projectService.UpdateProject(selectedProject);
            logger.Log(LogLevel.Info, "ProjectsPane", $"Updated field '{editingFieldName}' for project {selectedProject.Name}");

            // Refresh display and status bar
            DisplayProjectDetails(selectedProject);
            RefreshProjectList();
            UpdateStatusBar();
            editingFieldName = null;
        }

        private void CancelFieldEdit(TextBox editBox, TextBlock originalText)
        {
            editingFieldName = null;
            if (originalText != null)
            {
                var parent = (Grid)editBox.Parent;
                parent.Children.Remove(editBox);
                parent.Children.Add(originalText);
            }
        }

        private (bool isValid, string errorMessage) ValidateField(string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (fieldName == "Name" || fieldName == "ID2 (CAS Case)")
                    return (false, $"{fieldName} is required");
                return (true, null);
            }

            switch (fieldName)
            {
                case "TP Email":
                    if (!value.Contains("@") || !value.Contains("."))
                        return (false, "Invalid email format");
                    break;

                case "TP Phone":
                case "Contact 1 Phone":
                case "Contact 2 Phone":
                    if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[\d\-\(\)\s\+]+$"))
                        return (false, "Phone should contain only digits, spaces, and ()-.+ characters");
                    break;

                case "Budget Hours":
                case "Budget Amount":
                    if (!decimal.TryParse(value.Replace("$", "").Replace(",", ""), out decimal numValue) || numValue < 0)
                        return (false, "Must be a positive number");
                    break;

                case "Date Assigned":
                case "Request Date":
                case "Start Date":
                case "End Date":
                case "Audit Period From":
                case "Audit Period To":
                    if (ParseDateInput(value) == null && value != "none" && !string.IsNullOrWhiteSpace(value))
                        return (false, "Invalid date format");
                    break;

                case "ID2 (CAS Case)":
                    if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{4}$"))
                        return (false, "ID2 must be 4 digits");
                    break;

                case "Postal Code":
                    if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Z]\d[A-Z]\s?\d[A-Z]\d$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        return (false, "Invalid Canadian postal code format (A1A 1A1)");
                    break;
            }

            return (true, null);
        }

        private void UpdateProjectField(Project project, string fieldName, string value)
        {
            var (isValid, errorMessage) = ValidateField(fieldName, value);
            if (!isValid)
            {
                UpdateStatusBar($"Validation error: {errorMessage}");
                logger.Log(LogLevel.Warning, "ProjectsPane",
                    $"Validation failed for {fieldName}: {errorMessage}");
                return;
            }

            switch (fieldName)
            {
                case "Name": project.Name = value; break;
                case "Nickname": project.Nickname = value; break;
                case "ID2 (CAS Case)": project.ID2 = value; break;
                case "Id1 (Audit Case)": project.Id1 = value; break;
                case "Full Project Name": project.FullProjectName = value; break;
                case "Client ID": project.ClientID = value; break;
                case "TaxID": project.TaxID = value; break;
                case "CAS Number": project.CASNumber = value; break;
                case "Address": project.Address = value; break;
                case "City": project.City = value; break;
                case "Province": project.Province = value; break;
                case "Postal Code": project.PostalCode = value; break;
                case "Country": project.Country = value; break;
                case "TP Email": project.TPEmailAddress = value; break;
                case "TP Phone": project.TPPhoneNumber = value; break;
                case "Ship To Address": project.ShipToAddress = value; break;
                case "Audit Type": project.AuditType = value; break;
                case "Audit Program": project.AuditProgram = value; break;
                case "Auditor Name": project.AuditorName = value; break;
                case "Description": project.Description = value; break;
                case "Comments": project.Comments = value; break;
                case "FX Info": project.FXInfo = value; break;
                case "Email Reference": project.EmailReference = value; break;
                case "Contact 1 Name": project.Contact1Name = value; break;
                case "Contact 1 Title": project.Contact1Title = value; break;
                case "Contact 1 Phone": project.Contact1Phone = value; break;
                case "Contact 1 Ext": project.Contact1Ext = value; break;
                case "Contact 1 Address": project.Contact1Address = value; break;
                case "Contact 2 Name": project.Contact2Name = value; break;
                case "Contact 2 Title": project.Contact2Title = value; break;
                case "Contact 2 Phone": project.Contact2Phone = value; break;
                case "Contact 2 Ext": project.Contact2Ext = value; break;
                case "Contact 2 Address": project.Contact2Address = value; break;
                case "Software 1": project.AccountingSoftware1 = value; break;
                case "Software 1 Other": project.AccountingSoftware1Other = value; break;
                case "Software 1 Type": project.AccountingSoftware1Type = value; break;
                case "Software 2": project.AccountingSoftware2 = value; break;
                case "Software 2 Other": project.AccountingSoftware2Other = value; break;
                case "Software 2 Type": project.AccountingSoftware2Type = value; break;
                case "Project Folder": project.CustomFields["ProjectFolder"] = value; break;
                case "CAA File": project.CustomFields["CAAFile"] = value; break;
                case "Request File": project.CustomFields["RequestFile"] = value; break;
                case "T2020 File": project.CustomFields["T2020File"] = value; break;
                case "Budget Hours":
                    if (decimal.TryParse(value, out decimal hours))
                        project.BudgetHours = hours;
                    break;
                case "Budget Amount":
                    if (decimal.TryParse(value.Replace("$", "").Replace(",", ""), out decimal amount))
                        project.BudgetAmount = amount;
                    break;
                // Date fields
                case "Date Assigned":
                    project.DateAssigned = ParseDateInput(value);
                    break;
                case "Request Date":
                    project.RequestDate = ParseDateInput(value);
                    break;
                case "Start Date":
                    project.StartDate = ParseDateInput(value);
                    break;
                case "End Date":
                    project.EndDate = ParseDateInput(value);
                    break;
                case "Audit Period From":
                    project.AuditPeriodFrom = ParseDateInput(value);
                    break;
                case "Audit Period To":
                    project.AuditPeriodTo = ParseDateInput(value);
                    break;
            }

            project.UpdatedAt = DateTime.Now;
        }

        private DateTime? ParseDateInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || input == "none") return null;

            // Try exact parse first
            if (DateTime.TryParse(input, out DateTime exactDate))
                return exactDate;

            var today = DateTime.Today;

            // Relative days: "2d", "5d"
            if (input.EndsWith("d") && int.TryParse(input.Substring(0, input.Length - 1), out int days))
                return today.AddDays(days);

            // Relative weeks: "2w", "3w"
            if (input.EndsWith("w") && int.TryParse(input.Substring(0, input.Length - 1), out int weeks))
                return today.AddDays(weeks * 7);

            // Relative months: "2m", "6m"
            if (input.EndsWith("m") && int.TryParse(input.Substring(0, input.Length - 1), out int months))
                return today.AddMonths(months);

            // Named shortcuts
            switch (input.ToLowerInvariant())
            {
                case "today": return today;
                case "tomorrow": case "tom": return today.AddDays(1);
                case "yesterday": return today.AddDays(-1);
            }

            return null;
        }

        private void RefreshProjectList()
        {
            allProjects = projectService.GetAllProjects()
                .Where(ApplyFilter)
                .Where(ApplySearch)
                .OrderBy(p => p.Name)
                .ToList();

            projectListBox.Items.Clear();
            foreach (var project in allProjects)
            {
                projectListBox.Items.Add(FormatProjectListItem(project));
            }

            UpdateStatusBar();
        }

        private bool ApplySearch(Project project)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return true;

            var query = searchQuery.ToLower();

            // Search across all major fields
            return (project.Name != null && project.Name.ToLower().Contains(query)) ||
                   (project.Nickname != null && project.Nickname.ToLower().Contains(query)) ||
                   (project.ID2 != null && project.ID2.ToLower().Contains(query)) ||
                   (project.Id1 != null && project.Id1.ToLower().Contains(query)) ||
                   (project.FullProjectName != null && project.FullProjectName.ToLower().Contains(query)) ||
                   (project.ClientID != null && project.ClientID.ToLower().Contains(query)) ||
                   (project.TaxID != null && project.TaxID.ToLower().Contains(query)) ||
                   (project.CASNumber != null && project.CASNumber.ToLower().Contains(query)) ||
                   (project.Address != null && project.Address.ToLower().Contains(query)) ||
                   (project.City != null && project.City.ToLower().Contains(query)) ||
                   (project.Province != null && project.Province.ToLower().Contains(query)) ||
                   (project.PostalCode != null && project.PostalCode.ToLower().Contains(query)) ||
                   (project.Country != null && project.Country.ToLower().Contains(query)) ||
                   (project.TPEmailAddress != null && project.TPEmailAddress.ToLower().Contains(query)) ||
                   (project.TPPhoneNumber != null && project.TPPhoneNumber.ToLower().Contains(query)) ||
                   (project.AuditType != null && project.AuditType.ToLower().Contains(query)) ||
                   (project.AuditProgram != null && project.AuditProgram.ToLower().Contains(query)) ||
                   (project.AuditorName != null && project.AuditorName.ToLower().Contains(query)) ||
                   (project.Description != null && project.Description.ToLower().Contains(query)) ||
                   (project.Comments != null && project.Comments.ToLower().Contains(query)) ||
                   (project.FXInfo != null && project.FXInfo.ToLower().Contains(query)) ||
                   (project.EmailReference != null && project.EmailReference.ToLower().Contains(query)) ||
                   (project.Contact1Name != null && project.Contact1Name.ToLower().Contains(query)) ||
                   (project.Contact2Name != null && project.Contact2Name.ToLower().Contains(query)) ||
                   (project.AccountingSoftware1 != null && project.AccountingSoftware1.ToLower().Contains(query)) ||
                   (project.AccountingSoftware2 != null && project.AccountingSoftware2.ToLower().Contains(query));
        }

        private bool ApplyFilter(Project project)
        {
            return currentFilter switch
            {
                FilterMode.All => !project.Deleted && !project.Archived,
                FilterMode.Active => !project.Deleted && !project.Archived && project.Status == ProjectStatus.Active,
                FilterMode.Planned => !project.Deleted && !project.Archived && project.Status == ProjectStatus.Planned,
                FilterMode.Completed => !project.Deleted && project.Status == ProjectStatus.Completed,
                FilterMode.Overdue => !project.Deleted && !project.Archived && project.IsOverdue,
                FilterMode.HighPriority => !project.Deleted && !project.Archived && project.Priority == TaskPriority.High,
                FilterMode.Archived => !project.Deleted && project.Archived,
                _ => true
            };
        }

        private TextBlock FormatProjectListItem(Project project)
        {
            var tb = new TextBlock
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas"),
                FontSize = 14,
                Foreground = fgBrush,
                Padding = new Thickness(8, 4, 8, 4),
                Tag = project
            };

            var run1 = new Run($"{project.StatusIcon} ");
            run1.Foreground = GetStatusColor(project.Status);

            var run2 = new Run(project.DisplayId);
            run2.Foreground = accentBrush;
            run2.FontWeight = FontWeights.Bold;

            tb.Inlines.Add(run1);
            tb.Inlines.Add(run2);
            tb.Inlines.Add(new Run(" "));

            // Use highlighting for project name
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                AddHighlightedText(tb, project.Name, searchQuery);
            }
            else
            {
                var run3 = new Run(project.Name);
                run3.Foreground = fgBrush;
                tb.Inlines.Add(run3);
            }

            return tb;
        }

        private void AddHighlightedText(TextBlock textBlock, string text, string searchQuery)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var theme = themeManager.CurrentTheme;
            var highlightBrush = new SolidColorBrush(theme.Success);
            var normalBrush = new SolidColorBrush(theme.Foreground);

            int queryIndex = 0;
            for (int i = 0; i < text.Length && queryIndex < searchQuery.Length; i++)
            {
                bool isMatch = char.ToLower(text[i]) == char.ToLower(searchQuery[queryIndex]);

                var run = new Run(text[i].ToString())
                {
                    Foreground = isMatch ? highlightBrush : normalBrush,
                    FontWeight = isMatch ? FontWeights.Bold : FontWeights.Normal
                };

                if (isMatch) queryIndex++;
                textBlock.Inlines.Add(run);
            }
        }

        private SolidColorBrush GetStatusColor(ProjectStatus status)
        {
            return status switch
            {
                ProjectStatus.Active => successBrush,
                ProjectStatus.Completed => successBrush,
                ProjectStatus.OnHold => warningBrush,
                ProjectStatus.Cancelled => dimBrush,
                _ => fgBrush
            };
        }

        private void ProjectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (projectListBox.SelectedItem is TextBlock tb && tb.Tag is Project project)
            {
                selectedProject = project;
                DisplayProjectDetails(project);

                // Publish project selection event
                eventBus.Publish(new Core.Events.ProjectSelectedEvent
                {
                    Project = project,
                    SourceWidget = "ProjectsPane"
                });
            }
        }

        private void ProjectListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None && Keyboard.FocusedElement is TextBox)
                return;
            var shortcuts = ShortcutManager.Instance;
            if (shortcuts.HandleKeyPress(e.Key, e.KeyboardDevice.Modifiers, null, PaneName))
                e.Handled = true;
        }

        private void QuickAddBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CreateQuickProject();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                HideQuickAdd();
                e.Handled = true;
            }
        }

        private void ShowQuickAdd()
        {
            quickAddBox.Text = "";
            quickAddBox.Visibility = Visibility.Visible;
            quickAddBox.Focus();
            UpdateStatusBar("Quick Add: Name | DateAssigned (optional) | ID2 (optional)");
        }

        private void HideQuickAdd()
        {
            quickAddBox.Visibility = Visibility.Collapsed;
            quickAddBox.Text = "";
            projectListBox.Focus();
            UpdateStatusBar();
        }

        private void CreateQuickProject()
        {
            string input = quickAddBox.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                HideQuickAdd();
                return;
            }

            // Parse: Name | DateAssigned | ID2
            var parts = input.Split('|').Select(p => p.Trim()).ToArray();

            var project = new Project
            {
                Name = parts[0],
                DateAssigned = parts.Length > 1 ? ParseDateInput(parts[1]) : DateTime.Now,
                ID2 = parts.Length > 2 ? parts[2] : "",
                Status = ProjectStatus.Active,
                Priority = TaskPriority.Medium
            };

            projectService.AddProject(project);
            logger.Log(LogLevel.Info, "ProjectsPane", $"Created project: {project.Name}");

            HideQuickAdd();
            RefreshProjectList();

            // Select the new project
            for (int i = 0; i < projectListBox.Items.Count; i++)
            {
                if (projectListBox.Items[i] is TextBlock tb && tb.Tag is Project p && p.Id == project.Id)
                {
                    projectListBox.SelectedIndex = i;
                    projectListBox.ScrollIntoView(projectListBox.SelectedItem);
                    break;
                }
            }
        }

        private void DeleteCurrentProject()
        {
            if (selectedProject == null) return;

            var result = MessageBox.Show(
                $"Delete project '{selectedProject.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                projectService.DeleteProject(selectedProject.Id);
                logger.Log(LogLevel.Info, "ProjectsPane", $"Deleted project: {selectedProject.Name}");
                selectedProject = null;
                DisplayProjectDetails(null);
                RefreshProjectList();
            }
        }

        private void CycleFilter()
        {
            currentFilter = currentFilter switch
            {
                FilterMode.All => FilterMode.Active,
                FilterMode.Active => FilterMode.Planned,
                FilterMode.Planned => FilterMode.Completed,
                FilterMode.Completed => FilterMode.Overdue,
                FilterMode.Overdue => FilterMode.HighPriority,
                FilterMode.HighPriority => FilterMode.Archived,
                FilterMode.Archived => FilterMode.All,
                _ => FilterMode.All
            };

            filterLabel.Text = $"Filter: {currentFilter}";
            RefreshProjectList();
        }

        private void ExportT2020(Project project)
        {
            try
            {
                string fileName = $"t2020_{SanitizeFileName(project.Name)}.txt";
                string filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

                var content = GenerateT2020Content(project);
                System.IO.File.WriteAllText(filePath, content);

                logger.Log(LogLevel.Info, "ProjectsPane", $"Exported T2020: {filePath}");
                UpdateStatusBar($"Exported T2020 to {fileName}");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "ProjectsPane", $"Failed to export T2020: {ex.Message}");
                UpdateStatusBar($"ERROR: {ex.Message}");
            }
        }

        private string GenerateT2020Content(Project project)
        {
            // T2020 format (customize as needed)
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("T2020 PROJECT SUMMARY");
            sb.AppendLine("====================");
            sb.AppendLine();
            sb.AppendLine($"Project: {project.Name}");
            sb.AppendLine($"ID2 (CAS Case): {project.ID2}");
            sb.AppendLine($"Date Assigned: {project.DateAssigned?.ToString("yyyy-MM-dd") ?? "N/A"}");
            sb.AppendLine($"Status: {project.Status}");
            sb.AppendLine();
            sb.AppendLine("CLIENT INFORMATION");
            sb.AppendLine("------------------");
            sb.AppendLine($"Full Name: {project.FullProjectName}");
            sb.AppendLine($"Client ID: {project.ClientID}");
            sb.AppendLine($"Tax ID: {project.TaxID}");
            sb.AppendLine($"Address: {project.Address}");
            sb.AppendLine($"City: {project.City}, {project.Province} {project.PostalCode}");
            sb.AppendLine($"Country: {project.Country}");
            sb.AppendLine($"Email: {project.TPEmailAddress}");
            sb.AppendLine($"Phone: {project.TPPhoneNumber}");
            sb.AppendLine();
            sb.AppendLine("PROJECT DETAILS");
            sb.AppendLine("---------------");
            sb.AppendLine($"Audit Type: {project.AuditType}");
            sb.AppendLine($"Audit Program: {project.AuditProgram}");
            sb.AppendLine($"Auditor: {project.AuditorName}");
            sb.AppendLine($"Period: {project.AuditPeriodFrom?.ToString("yyyy-MM-dd")} to {project.AuditPeriodTo?.ToString("yyyy-MM-dd")}");
            sb.AppendLine();
            sb.AppendLine($"Comments: {project.Comments}");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }

        private string SanitizeFileName(string fileName)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        private void SubscribeToProjectEvents()
        {
            projectService.ProjectAdded += OnProjectAdded;
            projectService.ProjectUpdated += OnProjectUpdated;
            projectService.ProjectDeleted += OnProjectDeleted;
            projectContext.ProjectContextChanged += OnProjectContextChanged;
        }

        private void OnProjectAdded(Project project)
        {
            // Guard against cross-thread access in tests
            if (!CheckAccess())
            {
                logger?.Log(LogLevel.Debug, PaneName ?? "ProjectsPane",
                    "OnProjectAdded called on non-UI thread - skipping UI updates (test mode)");
                return;
            }

            RefreshProjectList();
        }

        private void OnProjectUpdated(Project project)
        {
            // Guard against cross-thread access in tests
            if (!CheckAccess())
            {
                logger?.Log(LogLevel.Debug, PaneName ?? "ProjectsPane",
                    "OnProjectUpdated called on non-UI thread - skipping UI updates (test mode)");
                return;
            }

            RefreshProjectList();
            if (selectedProject?.Id == project.Id)
            {
                selectedProject = project;
                DisplayProjectDetails(project);
            }
        }

        private void OnProjectDeleted(Guid projectId)
        {
            // Guard against cross-thread access in tests
            if (!CheckAccess())
            {
                logger?.Log(LogLevel.Debug, PaneName ?? "ProjectsPane",
                    "OnProjectDeleted called on non-UI thread - skipping UI updates (test mode)");
                return;
            }

            if (selectedProject?.Id == projectId)
            {
                selectedProject = null;
                DisplayProjectDetails(null);
            }
            RefreshProjectList();
        }

        private void OnProjectContextChanged(object sender, EventArgs e)
        {
            // Guard against cross-thread access in tests
            if (!CheckAccess())
            {
                logger?.Log(LogLevel.Debug, PaneName ?? "ProjectsPane",
                    "OnProjectContextChanged called on non-UI thread - skipping UI updates (test mode)");
                return;
            }

            UpdateStatusBar();
        }

        /// <summary>
        /// Handle TaskSelectedEvent - highlight the parent project of the selected task
        /// </summary>
        private void OnTaskSelected(Core.Events.TaskSelectedEvent evt)
        {
            if (evt?.Task == null) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Find the parent project of the selected task
                if (evt.ProjectId.HasValue)
                {
                    var project = projectService.GetProject(evt.ProjectId.Value);
                    if (project != null)
                    {
                        // Select the project in the list
                        for (int i = 0; i < projectListBox.Items.Count; i++)
                        {
                            if (projectListBox.Items[i] is TextBlock tb && tb.Tag is Project p && p.Id == project.Id)
                            {
                                projectListBox.SelectedIndex = i;
                                projectListBox.ScrollIntoView(projectListBox.SelectedItem);
                                logger.Log(LogLevel.Info, "ProjectsPane",
                                    $"Highlighted project '{project.Name}' for task '{evt.Task.Title}'");
                                break;
                            }
                        }
                    }
                    else
                    {
                        logger.Log(LogLevel.Warning, "ProjectsPane",
                            $"Task '{evt.Task.Title}' has ProjectId but project not found");
                    }
                }
                else
                {
                    logger.Log(LogLevel.Debug, "ProjectsPane",
                        $"Task '{evt.Task.Title}' has no associated project");
                }
            });
        }

        private void UpdateStatusBar(string customMessage = null)
        {
            if (customMessage != null)
            {
                statusBar.Text = customMessage;
                return;
            }

            statusBar.Text = GetStatusBarText();
        }

        private string GetStatusBarText()
        {
            var contextInfo = projectContext.CurrentProject != null
                ? $" | Context: {projectContext.CurrentProject.Name}"
                : "";

            var modIndicator = modifiedFields.Count > 0 ? $" | {modifiedFields.Count} modified" : "";

            return $"{allProjects.Count} projects{modIndicator} | A:Add D:Delete F:Filter K:SetContext X:ExportT2020 Click:Edit{contextInfo}";
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ApplyTheme();
            });
        }

        private void ApplyTheme()
        {
            CacheThemeColors();

            // Update all controls
            if (searchBox != null)
            {
                searchBox.Background = surfaceBrush;
                searchBox.Foreground = fgBrush;
                searchBox.BorderBrush = borderBrush;
            }

            if (quickAddBox != null)
            {
                quickAddBox.Background = surfaceBrush;
                quickAddBox.Foreground = fgBrush;
                quickAddBox.BorderBrush = borderBrush;
            }

            if (filterLabel != null)
            {
                filterLabel.Foreground = accentBrush;
                filterLabel.Background = surfaceBrush;
            }

            if (projectListBox != null)
            {
                projectListBox.Background = bgBrush;
                projectListBox.Foreground = fgBrush;
            }

            if (detailScroll != null)
            {
                detailScroll.Background = bgBrush;
            }

            if (statusBar != null)
            {
                statusBar.Foreground = dimBrush;
                statusBar.Background = surfaceBrush;
            }

            // Refresh project list and details to update colors
            RefreshProjectList();
            if (selectedProject != null)
            {
                DisplayProjectDetails(selectedProject);
            }

            this.InvalidateVisual();
        }

        protected override void OnDispose()
        {
            // Unsubscribe from EventBus to prevent memory leaks
            if (taskSelectedHandler != null)
            {
                eventBus.Unsubscribe(taskSelectedHandler);
                taskSelectedHandler = null;
            }

            if (refreshRequestedHandler != null)
            {
                eventBus.Unsubscribe(refreshRequestedHandler);
                refreshRequestedHandler = null;
            }

            // Unsubscribe from service events
            projectService.ProjectAdded -= OnProjectAdded;
            projectService.ProjectUpdated -= OnProjectUpdated;
            projectService.ProjectDeleted -= OnProjectDeleted;
            projectContext.ProjectContextChanged -= OnProjectContextChanged;
            themeManager.ThemeChanged -= OnThemeChanged;

            base.OnDispose();
        }

        /// <summary>
        /// Handle RefreshRequestedEvent - reload projects from service
        /// </summary>
        private void OnRefreshRequested(Core.Events.RefreshRequestedEvent evt)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                RefreshProjectList();
                Log("ProjectsPane refreshed (RefreshRequestedEvent)");
            });
        }
    }
}
