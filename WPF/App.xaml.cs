using System;
using System.Windows;
using SuperTUI.DI;
using SuperTUI.Infrastructure;

namespace SuperTUI
{
    public partial class App : Application
    {
        private ServiceContainer serviceContainer;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Set up global exception handling
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            try
            {
                // Initialize service container with proper error handling
                string configPath = System.IO.Path.Combine(
                    SuperTUI.Extensions.PortableDataDirectory.GetSuperTUIDataDirectory(),
                    "config.json");

                serviceContainer = ServiceRegistration.RegisterAllServices(configPath);

                // Create and show main window
                var mainWindow = new MainWindow(serviceContainer);
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to start SuperTUI:\n\n{ex.Message}\n\nPlease check that you have write access to your AppData directory.",
                    "SuperTUI Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            try
            {
                // Dispose service container (will dispose all singletons)
                serviceContainer?.Dispose();
            }
            catch (Exception ex)
            {
                // Log but don't crash on exit
                System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex.Message}");
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    $"A critical error occurred:\n\n{ex.Message}\n\nThe application will now close.",
                    "SuperTUI Critical Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nYou can try to continue, but the application may be unstable.",
                "SuperTUI Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            // Mark as handled to prevent crash
            e.Handled = true;
        }
    }
}
