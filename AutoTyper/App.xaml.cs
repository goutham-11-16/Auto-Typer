using System.Configuration;
using System.Data;
using System.Windows;

namespace AutoTyper;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static void Log(string msg)
    {
        try
        {
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Auto Typer byGo", "startup_log.txt");
            System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] [App] {msg}\n");
        }
        catch { }
    }

    public App()
    {
        Log("App Constructor");
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Log($"UnobservedTaskException: {e.Exception}");
            e.SetObserved(); // Prevent crash from unobserved Task exceptions
        };
        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            Log($"ProcessExit triggered!\n{Environment.StackTrace}");
        };
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    private static System.Threading.Mutex? _singleInstanceMutex;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static void RegisterProtocolHandler()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\autotyper");
            if (key != null)
            {
                key.SetValue("", "URL:AutoTyper Protocol");
                key.SetValue("URL Protocol", "");
                using var defaultIcon = key.CreateSubKey("DefaultIcon");
                defaultIcon?.SetValue("", $"\"{exePath}\",1");
                using var shell = key.CreateSubKey(@"shell\open\command");
                shell?.SetValue("", $"\"{exePath}\" \"%1\"");
            }
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Log("OnStartup Begin");
        RegisterProtocolHandler();

        bool createdNew;
        _singleInstanceMutex = new System.Threading.Mutex(true, "Global\\AutoTyper_ByGo_SingleInstance_Mutex_Unique", out createdNew);
        if (!createdNew)
        {
            Log("Another instance of AutoTyper-byGo is already running.");
            try
            {
                var current = System.Diagnostics.Process.GetCurrentProcess();
                var existing = System.Diagnostics.Process.GetProcessesByName(current.ProcessName)
                    .FirstOrDefault(p => p.Id != current.Id);

                if (existing != null && existing.MainWindowHandle != IntPtr.Zero)
                {
                    Log($"Restoring and bringing existing window (PID {existing.Id}) to foreground.");
                    ShowWindow(existing.MainWindowHandle, 9); // SW_RESTORE
                    SetForegroundWindow(existing.MainWindowHandle);
                    Shutdown();
                    return;
                }
                else if (existing != null)
                {
                    Log($"Existing process PID {existing.Id} has no visible window (headless/zombie). Terminating it to launch UI.");
                    try 
                    { 
                        existing.Kill(); 
                        existing.WaitForExit(2000); 
                    } 
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log($"Error checking existing instance: {ex.Message}");
            }
        }

        base.OnStartup(e);

        try
        {
            // ACCESS CONTROL CHECK
            Log("Opening AccessWindow...");
            var accessWindow = new AutoTyper.Views.AccessWindow();
            bool? authorized = accessWindow.ShowDialog();
            Log($"AccessWindow returned authorized = {authorized}");

            if (authorized == true)
            {
                Log("Authorized! Creating MainWindow...");
                var mainWindow = new MainWindow();
                this.MainWindow = mainWindow;
                mainWindow.Closed += (s, args) => 
                {
                    Log("MainWindow Closed Event -> Calling Environment.Exit(0)");
                    try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
                    Environment.Exit(0);
                };
                mainWindow.Show();
                Log("MainWindow Show Called successfully");
            }
            else
            {
                Log($"Not authorized (result={authorized}) -> Calling Shutdown()");
                Shutdown();
            }
        }

        catch (Exception ex)
        {
            Log($"Fatal Startup Error: {ex}");
            System.Windows.MessageBox.Show($"Fatal Startup Error: {ex.Message}\n\n{ex.StackTrace}", "Auto Typer Crash", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try { System.IO.File.AppendAllText("startup_log.txt", $"DispatcherUnhandledException: {e.Exception}\n"); } catch { }
        System.Windows.MessageBox.Show($"Unhandled UI Exception: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Auto Typer Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        try { System.IO.File.AppendAllText("startup_log.txt", $"CurrentDomain_UnhandledException: {ex}\n"); } catch { }
        System.Windows.MessageBox.Show($"Critical Runtime Error: {ex?.Message ?? "Unknown Error"}\n\n{ex?.StackTrace}", "Auto Typer Fatal", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }
}

