# WPF Terminal-Style TUI Proof of Concept
# Arrow keys, Tab, Enter, Escape - NO VIM BINDINGS

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

# Sample task data
$script:tasks = [System.Collections.ObjectModel.ObservableCollection[object]]::new(@(
    [PSCustomObject]@{ ID = 1; Title = "Review project proposal"; Project = "Work"; DueDate = "2025-10-24"; Priority = "High" }
    [PSCustomObject]@{ ID = 2; Title = "Fix authentication bug"; Project = "Dev"; DueDate = "2025-10-23"; Priority = "Critical" }
    [PSCustomObject]@{ ID = 3; Title = "Update documentation"; Project = "Docs"; DueDate = "2025-10-25"; Priority = "Medium" }
    [PSCustomObject]@{ ID = 4; Title = "Team standup meeting"; Project = "Work"; DueDate = "2025-10-23"; Priority = "Low" }
    [PSCustomObject]@{ ID = 5; Title = "Code review for PR #234"; Project = "Dev"; DueDate = "2025-10-24"; Priority = "High" }
))

[xml]$xaml = @"
<Window
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="SuperTUI - WPF Spike"
    Width="1200"
    Height="800"
    WindowStyle="None"
    ResizeMode="CanResizeWithGrip"
    Background="#0C0C0C"
    AllowsTransparency="False">

    <Window.Resources>
        <!-- Terminal color palette -->
        <SolidColorBrush x:Key="TerminalBackground">#0C0C0C</SolidColorBrush>
        <SolidColorBrush x:Key="TerminalForeground">#CCCCCC</SolidColorBrush>
        <SolidColorBrush x:Key="TerminalBorder">#3A3A3A</SolidColorBrush>
        <SolidColorBrush x:Key="TerminalHighlight">#264F78</SolidColorBrush>
        <SolidColorBrush x:Key="TerminalAccent">#4EC9B0</SolidColorBrush>
        <SolidColorBrush x:Key="TerminalSelection">#1E1E1E</SolidColorBrush>

        <!-- Title bar style -->
        <Style x:Key="TitleBarStyle" TargetType="Border">
            <Setter Property="Background" Value="#1E1E1E"/>
            <Setter Property="BorderBrush" Value="{StaticResource TerminalBorder}"/>
            <Setter Property="BorderThickness" Value="0,0,0,1"/>
            <Setter Property="Padding" Value="10,5"/>
        </Style>

        <!-- Panel style (for widgets/containers) -->
        <Style x:Key="PanelStyle" TargetType="Border">
            <Setter Property="Background" Value="#1A1A1A"/>
            <Setter Property="BorderBrush" Value="{StaticResource TerminalBorder}"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Margin" Value="5"/>
            <Setter Property="Padding" Value="10"/>
        </Style>

        <!-- Label style -->
        <Style x:Key="LabelStyle" TargetType="TextBlock">
            <Setter Property="Foreground" Value="{StaticResource TerminalForeground}"/>
            <Setter Property="FontFamily" Value="Cascadia Mono, Consolas, Courier New"/>
            <Setter Property="FontSize" Value="13"/>
        </Style>

        <!-- Title style -->
        <Style x:Key="TitleStyle" TargetType="TextBlock" BasedOn="{StaticResource LabelStyle}">
            <Setter Property="FontWeight" Value="Bold"/>
            <Setter Property="FontSize" Value="14"/>
            <Setter Property="Foreground" Value="{StaticResource TerminalAccent}"/>
        </Style>

        <!-- ListBox style for task list -->
        <Style x:Key="TaskListStyle" TargetType="ListBox">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Foreground" Value="{StaticResource TerminalForeground}"/>
            <Setter Property="FontFamily" Value="Cascadia Mono, Consolas, Courier New"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Padding" Value="0"/>
        </Style>

        <!-- ListBoxItem style -->
        <Style x:Key="TaskItemStyle" TargetType="ListBoxItem">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="{StaticResource TerminalForeground}"/>
            <Setter Property="Padding" Value="8,4"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ListBoxItem">
                        <Border
                            x:Name="ItemBorder"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{StaticResource TerminalBorder}"
                            BorderThickness="0,0,0,1"
                            Padding="{TemplateBinding Padding}">
                            <ContentPresenter />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsSelected" Value="True">
                                <Setter TargetName="ItemBorder" Property="Background" Value="{StaticResource TerminalHighlight}"/>
                            </Trigger>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="ItemBorder" Property="Background" Value="{StaticResource TerminalSelection}"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Button style -->
        <Style x:Key="TerminalButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="#2D2D2D"/>
            <Setter Property="Foreground" Value="{StaticResource TerminalForeground}"/>
            <Setter Property="BorderBrush" Value="{StaticResource TerminalBorder}"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="15,5"/>
            <Setter Property="FontFamily" Value="Cascadia Mono, Consolas, Courier New"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border
                            x:Name="ButtonBorder"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="ButtonBorder" Property="Background" Value="#3D3D3D"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="ButtonBorder" Property="Background" Value="{StaticResource TerminalHighlight}"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Title Bar -->
        <Border Grid.Row="0" Style="{StaticResource TitleBarStyle}">
            <Grid>
                <TextBlock
                    Text="SuperTUI - Task Manager (WPF Proof of Concept)"
                    Style="{StaticResource TitleStyle}"
                    VerticalAlignment="Center"/>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                    <Button Content="─" Width="30" Height="25" Style="{StaticResource TerminalButtonStyle}" x:Name="MinimizeButton"/>
                    <Button Content="□" Width="30" Height="25" Style="{StaticResource TerminalButtonStyle}" x:Name="MaximizeButton" Margin="5,0,0,0"/>
                    <Button Content="✕" Width="30" Height="25" Style="{StaticResource TerminalButtonStyle}" x:Name="CloseButton" Margin="5,0,0,0"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- Main Content Area (i3-like tiling) -->
        <Grid Grid.Row="1" Margin="5">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="2*"/>
                <ColumnDefinition Width="5"/>
                <ColumnDefinition Width="1*"/>
            </Grid.ColumnDefinitions>

            <!-- Left Panel: Task List -->
            <Border Grid.Column="0" Style="{StaticResource PanelStyle}">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>

                    <TextBlock
                        Grid.Row="0"
                        Text="Tasks"
                        Style="{StaticResource TitleStyle}"
                        Margin="0,0,0,10"/>

                    <ListBox
                        x:Name="TaskList"
                        Grid.Row="1"
                        Style="{StaticResource TaskListStyle}"
                        ItemContainerStyle="{StaticResource TaskItemStyle}">
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="40"/>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="100"/>
                                        <ColumnDefinition Width="100"/>
                                        <ColumnDefinition Width="80"/>
                                    </Grid.ColumnDefinitions>

                                    <TextBlock Grid.Column="0" Text="{Binding ID}" Foreground="#666666"/>
                                    <TextBlock Grid.Column="1" Text="{Binding Title}" Margin="5,0"/>
                                    <TextBlock Grid.Column="2" Text="{Binding Project}" Foreground="#4EC9B0"/>
                                    <TextBlock Grid.Column="3" Text="{Binding DueDate}" Foreground="#CE9178"/>
                                    <TextBlock Grid.Column="4" Text="{Binding Priority}" Foreground="#569CD6" TextAlignment="Right"/>
                                </Grid>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                </Grid>
            </Border>

            <!-- Splitter -->
            <GridSplitter
                Grid.Column="1"
                Width="5"
                HorizontalAlignment="Stretch"
                Background="{StaticResource TerminalBorder}"/>

            <!-- Right Panel: Details -->
            <Border Grid.Column="2" Style="{StaticResource PanelStyle}">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>

                    <TextBlock
                        Grid.Row="0"
                        Text="Details"
                        Style="{StaticResource TitleStyle}"
                        Margin="0,0,0,10"/>

                    <StackPanel Grid.Row="1" x:Name="DetailsPanel">
                        <TextBlock Style="{StaticResource LabelStyle}" Text="Select a task to view details" Foreground="#666666"/>
                    </StackPanel>
                </Grid>
            </Border>
        </Grid>

        <!-- Status Bar -->
        <Border Grid.Row="2" Style="{StaticResource TitleBarStyle}" BorderThickness="0,1,0,0">
            <Grid>
                <TextBlock
                    x:Name="StatusText"
                    Text="Arrow Keys: Navigate | Enter: Select | Tab: Switch Panel | Escape: Close"
                    Style="{StaticResource LabelStyle}"
                    Foreground="#666666"
                    VerticalAlignment="Center"/>
                <TextBlock
                    x:Name="TaskCount"
                    HorizontalAlignment="Right"
                    Style="{StaticResource LabelStyle}"
                    Foreground="#4EC9B0"
                    VerticalAlignment="Center"/>
            </Grid>
        </Border>
    </Grid>
</Window>
"@

# Load XAML
$reader = [System.Xml.XmlNodeReader]::new($xaml)
$window = [Windows.Markup.XamlReader]::Load($reader)

# Get controls
$taskList = $window.FindName("TaskList")
$detailsPanel = $window.FindName("DetailsPanel")
$statusText = $window.FindName("StatusText")
$taskCount = $window.FindName("TaskCount")
$closeButton = $window.FindName("CloseButton")
$minimizeButton = $window.FindName("MinimizeButton")
$maximizeButton = $window.FindName("MaximizeButton")

# Set data source
$taskList.ItemsSource = $script:tasks
$taskCount.Text = "Tasks: $($script:tasks.Count)"

# Window chrome buttons
$closeButton.Add_Click({ $window.Close() })
$minimizeButton.Add_Click({ $window.WindowState = 'Minimized' })
$maximizeButton.Add_Click({
    if ($window.WindowState -eq 'Maximized') {
        $window.WindowState = 'Normal'
    } else {
        $window.WindowState = 'Maximized'
    }
})

# Make title bar draggable
$window.Add_MouseLeftButtonDown({
    $window.DragMove()
})

# Task selection handler
$taskList.Add_SelectionChanged({
    $selected = $taskList.SelectedItem
    if ($selected) {
        $detailsPanel.Children.Clear()

        # Create detail view
        $details = @(
            [PSCustomObject]@{ Label = "ID"; Value = $selected.ID; Color = "#666666" }
            [PSCustomObject]@{ Label = "Title"; Value = $selected.Title; Color = "#CCCCCC" }
            [PSCustomObject]@{ Label = "Project"; Value = $selected.Project; Color = "#4EC9B0" }
            [PSCustomObject]@{ Label = "Due Date"; Value = $selected.DueDate; Color = "#CE9178" }
            [PSCustomObject]@{ Label = "Priority"; Value = $selected.Priority; Color = "#569CD6" }
        )

        foreach ($detail in $details) {
            $stackPanel = New-Object Windows.Controls.StackPanel
            $stackPanel.Margin = "0,0,0,15"

            $label = New-Object Windows.Controls.TextBlock
            $label.Text = $detail.Label
            $label.Foreground = "#666666"
            $label.FontFamily = "Cascadia Mono, Consolas"
            $label.FontSize = 11
            $label.Margin = "0,0,0,2"

            $value = New-Object Windows.Controls.TextBlock
            $value.Text = $detail.Value
            $value.Foreground = $detail.Color
            $value.FontFamily = "Cascadia Mono, Consolas"
            $value.FontSize = 13
            $value.FontWeight = "Bold"

            $stackPanel.Children.Add($label)
            $stackPanel.Children.Add($value)
            $detailsPanel.Children.Add($stackPanel)
        }

        $statusText.Text = "Selected: $($selected.Title)"
    }
})

# KEYBOARD HANDLING - ARROW KEYS, TAB, ENTER, ESCAPE
$window.Add_KeyDown({
    param($sender, $e)

    switch ($e.Key) {
        # ARROW KEYS for navigation
        "Up" {
            if ($taskList.SelectedIndex -gt 0) {
                $taskList.SelectedIndex--
            }
            $e.Handled = $true
        }
        "Down" {
            if ($taskList.SelectedIndex -lt ($taskList.Items.Count - 1)) {
                $taskList.SelectedIndex++
            }
            $e.Handled = $true
        }

        # ENTER to "activate" selected item
        "Return" {
            $selected = $taskList.SelectedItem
            if ($selected) {
                $statusText.Text = "Activated: $($selected.Title)"
                # You could open an edit dialog, mark complete, etc.
            }
            $e.Handled = $true
        }

        # TAB to switch focus between panels
        "Tab" {
            # Cycle focus between task list and details
            if ($taskList.IsFocused) {
                $detailsPanel.Focus()
                $statusText.Text = "Focus: Details Panel"
            } else {
                $taskList.Focus()
                $statusText.Text = "Focus: Task List"
            }
            $e.Handled = $true
        }

        # ESCAPE to close window
        "Escape" {
            $window.Close()
            $e.Handled = $true
        }

        # F5 to refresh (example)
        "F5" {
            $statusText.Text = "Refreshed task list"
            $e.Handled = $true
        }

        # Ctrl+N for new task (example)
        "N" {
            if ($e.KeyboardDevice.Modifiers -eq 'Control') {
                $statusText.Text = "New task dialog would open here"
                $e.Handled = $true
            }
        }

        # DELETE to remove task (example)
        "Delete" {
            $selected = $taskList.SelectedItem
            if ($selected) {
                $script:tasks.Remove($selected)
                $taskCount.Text = "Tasks: $($script:tasks.Count)"
                $statusText.Text = "Deleted task: $($selected.Title)"
            }
            $e.Handled = $true
        }
    }
})

# Set initial focus and selection
$taskList.SelectedIndex = 0
$taskList.Focus()

# Show window
$window.ShowDialog() | Out-Null
