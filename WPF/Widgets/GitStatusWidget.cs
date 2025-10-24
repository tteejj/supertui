using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Core;
using SuperTUI.Core.Components;
using SuperTUI.Core.Events;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Widgets
{
    /// <summary>
    /// Widget that displays Git repository status information.
    /// Shows branch, commit, and file status (modified, staged, untracked).
    /// </summary>
    public class GitStatusWidget : WidgetBase, IThemeable
    {
        private DispatcherTimer refreshTimer;
        private string repositoryPath;

        private TextBlock repoPathLabel;
        private TextBlock branchLabel;
        private TextBlock commitLabel;
        private TextBlock statusLabel;
        private TextBlock modifiedLabel;
        private TextBlock stagedLabel;
        private TextBlock untrackedLabel;

        private string currentBranch;
        private string lastCommit;
        private int modifiedCount;
        private int stagedCount;
        private int untrackedCount;

        private const int REFRESH_INTERVAL_MS = 5000; // 5 seconds

        public GitStatusWidget(string repoPath = null)
        {
            Name = "Git Status";
            repositoryPath = repoPath ?? Directory.GetCurrentDirectory();
        }

        public override void Initialize()
        {
            BuildUI();

            // Initial status check
            UpdateGitStatus();

            // Start refresh timer
            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(REFRESH_INTERVAL_MS)
            };
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
        }

        private void BuildUI()
        {
            var theme = ThemeManager.Instance.CurrentTheme;

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(10),
                Background = new SolidColorBrush(theme.Background)
            };

            // Title
            var title = new TextBlock
            {
                Text = "GIT REPOSITORY",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(theme.Foreground),
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(title);

            // Repository Path
            repoPathLabel = new TextBlock
            {
                Text = $"Path: {ShortenPath(repositoryPath)}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(repoPathLabel);

            // Branch
            AddInfoItem(stackPanel, "Branch:", "???", out branchLabel, theme.Info, theme);

            // Last Commit
            AddInfoItem(stackPanel, "Commit:", "???", out commitLabel, theme.Foreground, theme);

            // Status
            AddInfoItem(stackPanel, "Status:", "Checking...", out statusLabel, theme.Foreground, theme);

            // Separator
            var separator = new System.Windows.Shapes.Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(theme.Border),
                Margin = new Thickness(0, 10, 0, 10)
            };
            stackPanel.Children.Add(separator);

            // File Counts
            var countsPanel = new StackPanel();

            AddCountItem(countsPanel, "Modified:  ", "0", out modifiedLabel, theme.Warning, theme);
            AddCountItem(countsPanel, "Staged:    ", "0", out stagedLabel, theme.Success, theme);
            AddCountItem(countsPanel, "Untracked: ", "0", out untrackedLabel, theme.ForegroundSecondary, theme);

            stackPanel.Children.Add(countsPanel);

            // Legend
            var legend = new TextBlock
            {
                Text = "Updates every 5 seconds",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Foreground = new SolidColorBrush(theme.ForegroundSecondary),
                Margin = new Thickness(0, 15, 0, 0),
                Opacity = 0.7
            };
            stackPanel.Children.Add(legend);

            Content = stackPanel;
        }

        private void AddInfoItem(StackPanel parent, string label, string value,
            out TextBlock valueBlock, Color valueColor, Theme theme)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var labelBlock = new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground),
                Width = 70
            };
            panel.Children.Add(labelBlock);

            valueBlock = new TextBlock
            {
                Text = value,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(valueColor),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(valueBlock);

            parent.Children.Add(panel);
        }

        private void AddCountItem(StackPanel parent, string label, string value,
            out TextBlock valueBlock, Color valueColor, Theme theme)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2)
            };

            var labelBlock = new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(theme.Foreground)
            };
            panel.Children.Add(labelBlock);

            valueBlock = new TextBlock
            {
                Text = value,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(valueColor)
            };
            panel.Children.Add(valueBlock);

            parent.Children.Add(panel);
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            UpdateGitStatus();
        }

        private void UpdateGitStatus()
        {
            try
            {
                var theme = ThemeManager.Instance.CurrentTheme;

                // Check if this is a git repository
                if (!IsGitRepository())
                {
                    statusLabel.Text = "Not a git repository";
                    statusLabel.Foreground = new SolidColorBrush(theme.Error);
                    branchLabel.Text = "N/A";
                    commitLabel.Text = "N/A";
                    modifiedLabel.Text = "0";
                    stagedLabel.Text = "0";
                    untrackedLabel.Text = "0";
                    return;
                }

                // Get current branch
                var oldBranch = currentBranch;
                currentBranch = GetCurrentBranch();
                branchLabel.Text = currentBranch ?? "???";

                // Get last commit
                lastCommit = GetLastCommit();
                commitLabel.Text = lastCommit ?? "No commits";

                // Get file status
                GetFileStatus(out modifiedCount, out stagedCount, out untrackedCount);

                modifiedLabel.Text = modifiedCount.ToString();
                stagedLabel.Text = stagedCount.ToString();
                untrackedLabel.Text = untrackedCount.ToString();

                // Update status text
                if (modifiedCount == 0 && stagedCount == 0 && untrackedCount == 0)
                {
                    statusLabel.Text = "Clean";
                    statusLabel.Foreground = new SolidColorBrush(theme.Success);
                }
                else if (modifiedCount > 0 || untrackedCount > 0)
                {
                    statusLabel.Text = "Changes present";
                    statusLabel.Foreground = new SolidColorBrush(theme.Warning);
                }
                else
                {
                    statusLabel.Text = "Changes staged";
                    statusLabel.Foreground = new SolidColorBrush(theme.Info);
                }

                // Publish branch changed event if branch changed
                if (oldBranch != null && oldBranch != currentBranch)
                {
                    EventBus.Instance.Publish(new BranchChangedEvent
                    {
                        Repository = repositoryPath,
                        OldBranch = oldBranch,
                        NewBranch = currentBranch,
                        ChangedAt = DateTime.Now
                    });
                }

                // Publish repository status event
                EventBus.Instance.Publish(new RepositoryStatusChangedEvent
                {
                    Repository = repositoryPath,
                    Branch = currentBranch,
                    ModifiedFiles = modifiedCount,
                    StagedFiles = stagedCount,
                    UntrackedFiles = untrackedCount,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("GitStatus", $"Failed to update git status: {ex.Message}");
                statusLabel.Text = "Error reading status";
                statusLabel.Foreground = new SolidColorBrush(ThemeManager.Instance.CurrentTheme.Error);
            }
        }

        private bool IsGitRepository()
        {
            try
            {
                var gitDir = FindGitDirectory(repositoryPath);
                return gitDir != null;
            }
            catch
            {
                return false;
            }
        }

        private string FindGitDirectory(string startPath)
        {
            var current = new DirectoryInfo(startPath);
            while (current != null)
            {
                var gitPath = Path.Combine(current.FullName, ".git");
                if (Directory.Exists(gitPath))
                    return current.FullName;
                current = current.Parent;
            }
            return null;
        }

        private string GetCurrentBranch()
        {
            try
            {
                var result = RunGitCommand("rev-parse --abbrev-ref HEAD");
                return result?.Trim();
            }
            catch
            {
                return null;
            }
        }

        private string GetLastCommit()
        {
            try
            {
                var result = RunGitCommand("log -1 --pretty=format:\"%h %s\" HEAD");
                return result?.Trim().Trim('"');
            }
            catch
            {
                return null;
            }
        }

        private void GetFileStatus(out int modified, out int staged, out int untracked)
        {
            modified = 0;
            staged = 0;
            untracked = 0;

            try
            {
                var result = RunGitCommand("status --porcelain");
                if (string.IsNullOrEmpty(result))
                    return;

                var lines = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Length < 2) continue;

                    char x = line[0]; // Staged
                    char y = line[1]; // Unstaged

                    if (x != ' ' && x != '?')
                        staged++;

                    if (y != ' ')
                    {
                        if (y == '?')
                            untracked++;
                        else
                            modified++;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("GitStatus", $"Failed to get file status: {ex.Message}");
            }
        }

        private string RunGitCommand(string arguments)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = arguments,
                        WorkingDirectory = repositoryPath,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                    return output;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private string ShortenPath(string path)
        {
            if (path.Length <= 50)
                return path;

            // Show first 20 and last 25 characters
            return path.Substring(0, 20) + "..." + path.Substring(path.Length - 25);
        }

        protected override void OnDispose()
        {
            // Stop timer
            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer.Tick -= RefreshTimer_Tick;
                refreshTimer = null;
            }

            base.OnDispose();
        }

        public override void OnActivated()
        {
            base.OnActivated();
            refreshTimer?.Start();
            UpdateGitStatus(); // Immediate update on activation
        }

        public override void OnDeactivated()
        {
            base.OnDeactivated();
            refreshTimer?.Stop();
        }

        /// <summary>
        /// Apply current theme to all UI elements
        /// </summary>
        public void ApplyTheme()
        {
            // Rebuild UI with current theme
            BuildUI();
            // Update git status to refresh colors
            UpdateGitStatus();
        }
    }
}
