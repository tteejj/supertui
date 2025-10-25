using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Embedded PowerShell terminal widget.
    /// Executes commands in a persistent runspace with command history.
    /// </summary>
    public class TerminalWidget : WidgetBase, IThemeable
    {
        private Runspace runspace;
        private RichTextBox outputBox;
        private TextBox inputBox;
        private List<string> commandHistory;
        private int historyIndex;
        private Theme theme;
        private string currentDirectory;

        public TerminalWidget()
        {
            Name = "Terminal";
            commandHistory = new List<string>();
            historyIndex = -1;
        }

        public override void Initialize()
        {
            theme = ThemeManager.Instance.CurrentTheme;
            currentDirectory = Directory.GetCurrentDirectory();

            // Create PowerShell runspace
            InitializeRunspace();

            BuildUI();
            ShowPrompt();
        }

        private void InitializeRunspace()
        {
            try
            {
                runspace = RunspaceFactory.CreateRunspace();
                runspace.Open();

                // Set initial location
                using (var ps = PowerShell.Create())
                {
                    ps.Runspace = runspace;
                    ps.AddCommand("Set-Location").AddParameter("Path", currentDirectory);
                    ps.Invoke();
                }

                Logger.Instance.Info("Terminal", "PowerShell runspace initialized");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Terminal", $"Failed to initialize runspace: {ex.Message}");
            }
        }

        private void BuildUI()
        {
            var mainPanel = new DockPanel
            {
                Background = new SolidColorBrush(theme.Background),
                LastChildFill = true
            };

            // Title
            var title = new TextBlock
            {
                Text = "POWERSHELL TERMINAL",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Info),
                Margin = new Thickness(5, 5, 5, 10)
            };
            DockPanel.SetDock(title, Dock.Top);
            mainPanel.Children.Add(title);

            // Input box at bottom
            inputBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Surface),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderBrush = new SolidColorBrush(theme.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                Height = 30,
                Margin = new Thickness(5)
            };
            inputBox.KeyDown += InputBox_KeyDown;
            DockPanel.SetDock(inputBox, Dock.Bottom);
            mainPanel.Children.Add(inputBox);

            // Output box
            outputBox = new RichTextBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = new SolidColorBrush(theme.Background),
                Foreground = new SolidColorBrush(theme.Foreground),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5),
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var scrollViewer = new ScrollViewer
            {
                Content = outputBox,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(5, 0, 5, 5)
            };

            mainPanel.Children.Add(scrollViewer);
            Content = mainPanel;
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ExecuteCommand(inputBox.Text);
                inputBox.Clear();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                ShowPreviousCommand();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                ShowNextCommand();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                // Tab completion could be implemented here
                e.Handled = true;
            }
        }

        private void ShowPrompt()
        {
            var location = GetCurrentLocation();
            AppendText($"PS {location}> ", theme.Success);
        }

        private string GetCurrentLocation()
        {
            try
            {
                using (var ps = PowerShell.Create())
                {
                    ps.Runspace = runspace;
                    ps.AddCommand("Get-Location");
                    var results = ps.Invoke();
                    if (results != null && results.Count > 0)
                    {
                        currentDirectory = results[0].ToString();
                        return currentDirectory;
                    }
                }
            }
            catch { }
            return currentDirectory;
        }

        private async void ExecuteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                ShowPrompt();
                return;
            }

            // Add to history
            commandHistory.Add(command);
            historyIndex = commandHistory.Count;

            // Echo command
            AppendText($"{command}\n", theme.Foreground);

            // Publish command event
            EventBus.Instance.Publish(new CommandExecutedEvent
            {
                Command = command,
                WorkingDirectory = currentDirectory,
                ExecutedAt = DateTime.Now
            });

            try
            {
                // Execute in background
                var output = await Task.Run(() => ExecuteCommandInternal(command));

                // Display output
                if (!string.IsNullOrEmpty(output))
                {
                    AppendText(output + "\n", theme.Foreground);
                }

                // Check if working directory changed
                var newLocation = GetCurrentLocation();
                if (newLocation != currentDirectory)
                {
                    EventBus.Instance.Publish(new WorkingDirectoryChangedEvent
                    {
                        OldDirectory = currentDirectory,
                        NewDirectory = newLocation,
                        ChangedAt = DateTime.Now
                    });
                    currentDirectory = newLocation;
                }
            }
            catch (Exception ex)
            {
                AppendText($"Error: {ex.Message}\n", theme.Error);
                Logger.Instance.Error("Terminal", $"Command execution failed: {ex.Message}");
            }

            ShowPrompt();
            ScrollToEnd();
        }

        private string ExecuteCommandInternal(string command)
        {
            var output = new StringBuilder();

            try
            {
                using (var ps = PowerShell.Create())
                {
                    ps.Runspace = runspace;
                    ps.AddScript(command);

                    var results = ps.Invoke();

                    // Collect standard output
                    foreach (var result in results)
                    {
                        if (result != null)
                        {
                            output.AppendLine(result.ToString());
                        }
                    }

                    // Collect errors
                    if (ps.HadErrors)
                    {
                        foreach (var error in ps.Streams.Error)
                        {
                            output.AppendLine($"ERROR: {error}");
                        }
                    }

                    // Collect warnings
                    foreach (var warning in ps.Streams.Warning)
                    {
                        output.AppendLine($"WARNING: {warning}");
                    }

                    // Collect verbose
                    foreach (var verbose in ps.Streams.Verbose)
                    {
                        output.AppendLine($"VERBOSE: {verbose}");
                    }
                }
            }
            catch (Exception ex)
            {
                output.AppendLine($"Exception: {ex.Message}");
            }

            // Publish output event
            var outputText = output.ToString();
            if (!string.IsNullOrEmpty(outputText))
            {
                EventBus.Instance.Publish(new TerminalOutputEvent
                {
                    Output = outputText,
                    Command = command,
                    Timestamp = DateTime.Now
                });
            }

            return outputText.TrimEnd();
        }

        private void ShowPreviousCommand()
        {
            if (commandHistory.Count == 0) return;

            if (historyIndex > 0)
                historyIndex--;

            if (historyIndex >= 0 && historyIndex < commandHistory.Count)
            {
                inputBox.Text = commandHistory[historyIndex];
                inputBox.SelectionStart = inputBox.Text.Length;
            }
        }

        private void ShowNextCommand()
        {
            if (commandHistory.Count == 0) return;

            if (historyIndex < commandHistory.Count - 1)
            {
                historyIndex++;
                inputBox.Text = commandHistory[historyIndex];
            }
            else
            {
                historyIndex = commandHistory.Count;
                inputBox.Clear();
            }

            inputBox.SelectionStart = inputBox.Text.Length;
        }

        private void AppendText(string text, Color color)
        {
            Dispatcher.Invoke(() =>
            {
                var paragraph = new Paragraph();
                var run = new Run(text)
                {
                    Foreground = new SolidColorBrush(color)
                };
                paragraph.Inlines.Add(run);
                paragraph.Margin = new Thickness(0);
                outputBox.Document.Blocks.Add(paragraph);
            });
        }

        private void ScrollToEnd()
        {
            Dispatcher.Invoke(() =>
            {
                outputBox.ScrollToEnd();
            });
        }

        public void Clear()
        {
            outputBox.Document.Blocks.Clear();
            ShowPrompt();
        }

        public override Dictionary<string, object> SaveState()
        {
            return new Dictionary<string, object>
            {
                ["WorkingDirectory"] = currentDirectory,
                ["CommandHistory"] = commandHistory
            };
        }

        public override void LoadState(Dictionary<string, object> state)
        {
            if (state.TryGetValue("WorkingDirectory", out var dir))
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = runspace;
                        ps.AddCommand("Set-Location").AddParameter("Path", dir.ToString());
                        ps.Invoke();
                        currentDirectory = dir.ToString();
                    }
                }
                catch { }
            }

            if (state.TryGetValue("CommandHistory", out var history))
            {
                commandHistory = history as List<string> ?? new List<string>();
                historyIndex = commandHistory.Count;
            }
        }

        protected override void OnDispose()
        {
            inputBox.KeyDown -= InputBox_KeyDown;

            // Close runspace
            if (runspace != null)
            {
                try
                {
                    runspace.Close();
                    runspace.Dispose();
                }
                catch { }
                runspace = null;
            }

            base.OnDispose();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            if (outputBox != null)
            {
                outputBox.Background = new SolidColorBrush(theme.Background);
                outputBox.Foreground = new SolidColorBrush(theme.Foreground);
            }

            if (inputBox != null)
            {
                inputBox.Background = new SolidColorBrush(theme.Surface);
                inputBox.Foreground = new SolidColorBrush(theme.Foreground);
                inputBox.BorderBrush = new SolidColorBrush(theme.Border);
            }
        }
    }
}
