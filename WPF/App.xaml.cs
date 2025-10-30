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
                // Initialize Logger with file sink FIRST (before any other services)
                string dataDir = SuperTUI.Extensions.PortableDataDirectory.GetSuperTUIDataDirectory();
                string logDir = System.IO.Path.Combine(dataDir, "logs");
                System.IO.Directory.CreateDirectory(logDir);

                var fileLogSink = new Infrastructure.FileLogSink(logDir, "supertui.log");
                Infrastructure.Logger.Instance.AddSink(fileLogSink);
                Infrastructure.Logger.Instance.Info("App", "SuperTUI starting - logger initialized");

                // Initialize service container with proper error handling
                string configPath = System.IO.Path.Combine(dataDir, "config.json");

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
                Infrastructure.Logger.Instance.Info("App", "SuperTUI shutting down");

                // Dispose service container (will dispose all singletons)
                serviceContainer?.Dispose();

                // Flush and close logger
                Infrastructure.Logger.Instance.Flush();
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
                Infrastructure.Logger.Instance.Error("App", $"Unhandled exception: {ex.Message}", ex);
                Infrastructure.Logger.Instance.Flush();

                MessageBox.Show(
                    $"A critical error occurred:\n\n{ex.Message}\n\nThe application will now close.",
                    "SuperTUI Critical Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Infrastructure.Logger.Instance.Error("App", $"Dispatcher exception: {e.Exception.Message}", e.Exception);

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
