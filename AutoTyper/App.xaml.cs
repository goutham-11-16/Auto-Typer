using System.Configuration;
using System.Data;
using System.Windows;

namespace AutoTyper;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public App()
    {
        try { System.IO.File.AppendAllText("startup_log.txt", "App Constructor\n"); } catch { }
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        this.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try { System.IO.File.AppendAllText("startup_log.txt", "OnStartup Begin\n"); } catch { }
        base.OnStartup(e);

        try
        {
            var mainWindow = new MainWindow();
            this.MainWindow = mainWindow;
            mainWindow.Show();
            try { System.IO.File.AppendAllText("startup_log.txt", "MainWindow Show Called\n"); } catch { }
        }

        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Fatal Startup Error: {ex.Message}\n\n{ex.StackTrace}", "Auto Typer Crash", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        System.Windows.MessageBox.Show($"Unhandled UI Exception: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Auto Typer Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        System.Windows.MessageBox.Show($"Critical Runtime Error: {ex?.Message ?? "Unknown Error"}\n\n{ex?.StackTrace}", "Auto Typer Fatal", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }
}

