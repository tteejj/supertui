using System;
using System.Windows;
using SuperTUI.Core;
using SuperTUI.DI;
using SuperTUI.Infrastructure;

namespace SuperTUI
{
    public partial class MainWindow : Window
    {
        private readonly DI.ServiceContainer serviceContainer;
        private readonly WorkspaceManager workspaceManager;

        public MainWindow(DI.ServiceContainer container)
        {
            serviceContainer = container ?? throw new ArgumentNullException(nameof(container));

            InitializeComponent();

            // Create ContentControl for workspace manager
            var workspaceContainer = new System.Windows.Controls.ContentControl();
            RootContainer.Children.Add(workspaceContainer);

            // Initialize workspace manager
            workspaceManager = new WorkspaceManager(workspaceContainer);

            // Create default workspace with proper constructor
            var logger = serviceContainer.GetRequiredService<ILogger>();
            var themeManager = serviceContainer.GetRequiredService<IThemeManager>();
            var defaultWorkspace = new Workspace("Main", 0, null, logger, themeManager);

            workspaceManager.AddWorkspace(defaultWorkspace);
            workspaceManager.SwitchToWorkspace(0);

            // Set up window close handler
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Save state before closing
                var statePersistence = serviceContainer.GetService<IStatePersistenceManager>();
                if (statePersistence != null)
                {
                    // Use async version but with proper ConfigureAwait
                    var saveTask = statePersistence.SaveStateAsync(null, false);

                    // Show a brief progress dialog if save takes too long
                    if (!saveTask.Wait(TimeSpan.FromSeconds(5)))
                    {
                        var result = MessageBox.Show(
                            "Saving state is taking longer than expected.\n\nWait for save to complete?",
                            "SuperTUI",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            saveTask.Wait(); // Wait indefinitely
                        }
                        else
                        {
                            // Cancel close and let background save finish
                            e.Cancel = true;
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = serviceContainer.GetService<ILogger>();
                logger?.Error("MainWindow", $"Error saving state on close: {ex.Message}", ex);

                var result = MessageBox.Show(
                    $"Failed to save application state:\n\n{ex.Message}\n\nExit anyway?",
                    "SuperTUI Save Error",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
